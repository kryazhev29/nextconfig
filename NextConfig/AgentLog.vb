Imports System.IO
Imports System.Text
Imports System.Runtime.CompilerServices

' Журнал работы программы: по строке JSON на событие в
' %LOCALAPPDATA%\NextConfig\logs\ГГГГММДД.jsonl. По нему видно весь сеанс с платой:
' кадры в плату и из неё, окна, нажатия, тексты оператору, падения.
' ПИН и пароли в журнал не попадают: их байты заменяются на **.
Public Module AgentLog

    Private ReadOnly gate As New Object
    Private writer As StreamWriter
    Private fileDay As String = ""
    Private Const KeepDays As Integer = 30

    ' Опрос состояния идёт раз в 1–2 с: пишем его не чаще раза в PollEvery,
    ' неудачи и смену состояния пишем всегда.
    Private ReadOnly PollEvery As TimeSpan = TimeSpan.FromSeconds(30)
    Private ReadOnly lastPoll As New Dictionary(Of String, DateTime)

    Public ReadOnly Property Folder As String
        Get
            Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NextConfig")
        End Get
    End Property

    Public ReadOnly Property LogFolder As String
        Get
            Return Path.Combine(Folder, "logs")
        End Get
    End Property

    Public ReadOnly Property CurrentFile As String
        Get
            Return Path.Combine(LogFolder, DateTime.Now.ToString("yyyyMMdd") & ".jsonl")
        End Get
    End Property

    Public Sub Start()
        Try
            Directory.CreateDirectory(LogFolder)
            For Each f In Directory.GetFiles(LogFolder, "*.jsonl")
                If File.GetLastWriteTime(f) < DateTime.Now.AddDays(-KeepDays) Then File.Delete(f)
            Next
        Catch
        End Try

        Write("start", "version", My.Application.Info.Version.ToString(),
              "args", String.Join(" ", Environment.GetCommandLineArgs().Skip(1)),
              "demo", Demo.IsOn)
    End Sub

    ' Одна запись: пары «имя, значение».
    Public Sub Write(kind As String, ParamArray pairs As Object())
        Dim sb As New StringBuilder()
        sb.Append("{""t"":").Append(Json.Str(DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff")))
        sb.Append(",""k"":").Append(Json.Str(kind))
        For i = 0 To pairs.Length - 2 Step 2
            sb.Append(","c).Append(Json.Str(CStr(pairs(i)))).Append(":"c).Append(Json.Value(pairs(i + 1)))
        Next
        sb.Append("}"c)
        Append(sb.ToString())
    End Sub

    Private Sub Append(line As String)
        SyncLock gate
            Try
                Dim day = DateTime.Now.ToString("yyyyMMdd")
                If writer Is Nothing OrElse day <> fileDay Then
                    If writer IsNot Nothing Then writer.Dispose()
                    Directory.CreateDirectory(LogFolder)
                    Dim fs As New FileStream(CurrentFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite Or FileShare.Delete)
                    writer = New StreamWriter(fs, New UTF8Encoding(False)) With {.AutoFlush = True}
                    fileDay = day
                End If
                writer.WriteLine(line)
            Catch
                ' журнал не должен ронять работу с платой
            End Try
        End SyncLock
    End Sub

    ' Последние n строк сегодняшнего журнала.
    Public Function Tail(n As Integer) As List(Of String)
        Dim result As New List(Of String)
        Try
            Using fs As New FileStream(CurrentFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite Or FileShare.Delete),
                  r As New StreamReader(fs, Encoding.UTF8)
                Dim q As New Queue(Of String)
                Do
                    Dim l = r.ReadLine()
                    If l Is Nothing Then Exit Do
                    q.Enqueue(l)
                    If q.Count > n Then q.Dequeue()
                Loop
                result.AddRange(q)
            End Using
        Catch
        End Try
        Return result
    End Function

    ' ******************** обмен с платой ********************

    ' Кадр SLDN (прошивка NEXT): SLDN, длина 2, FF, номер, команда, данные, CRC 2.
    Public Sub SldnExchange(port As String, tx As Byte(), rx As Byte(), ms As Integer)
        Dim cmd As Integer = If(tx IsNot Nothing AndAlso tx.Length > 8, tx(8), -1)
        If cmd = clOpros.eCmd.CMD_STATUS AndAlso rx IsNot Nothing AndAlso Not PollDue("sldn101") Then Return
        ' поиск платы перебирает порты без конца: молчащий порт пишем изредка
        If cmd = clOpros.eCmd.CMD_IDENT AndAlso Not PollDue("ident " & port & If(rx Is Nothing, " -", " +")) Then Return

        Dim answer As Object = Nothing
        If SldnStream.IsAnswer(rx, CByte(cmd)) Then answer = CInt(SldnStream.AnswerCode(rx))

        Write("usb", "port", port, "proto", "SLDN", "cmd", cmd,
              "tx", SldnHex(tx, True), "rx", If(rx Is Nothing, Nothing, SldnHex(rx, False)),
              "ok", rx IsNot Nothing, "answer11", answer, "ms", ms)
    End Sub

    Private Function SldnHex(b As Byte(), outgoing As Boolean) As String
        If b Is Nothing Then Return Nothing
        Dim masked As New HashSet(Of Integer)
        If b.Length > 8 Then
            Select Case b(8)
                Case clOpros.eCmd.CMD_AUTORIZE ' ПИН в запросе
                    If outgoing Then AddRange(masked, 9, 4)
                Case clOpros.eCmd.CMD_SETCONFIG ' ПИН в пакете настроек, байты 4–7
                    If outgoing Then
                        AddRange(masked, 13, 4)
                        AddRange(masked, 113, 32)
                    End If
                Case clOpros.eCmd.CMD_GETCONFIG
                    If Not outgoing Then
                        AddRange(masked, 13, 4)
                        AddRange(masked, 113, 32)
                    End If
            End Select
        End If
        Return Hex(b, masked)
    End Function

    ' Кадр SLGT (загрузчик и старая прошивка v5): тело [AA|55, команда, ...].
    Public Sub SlgtExchange(port As String, cmd As Byte, tx As Byte(), rx As Byte(), ms As Integer, err As String)
        If cmd = &H22 AndAlso rx IsNot Nothing AndAlso Not PollDue("slgt22") Then Return
        If cmd = 0 AndAlso Not PollDue("slgt0 " & port & If(rx Is Nothing, " -", " +")) Then Return
        Dim txMasked As New HashSet(Of Integer)
        Dim rxMasked As New HashSet(Of Integer)
        Select Case cmd
            Case &H25 ' вход по паролю старой прошивки: пароль в данных запроса
                AddRange(txMasked, 8, If(tx Is Nothing, 0, tx.Length - 10))
            Case &H20 ' настройки в ответе с байта 6 тела [55, 20, итог, блок, длина 2]
                MaskOldConfig(rxMasked, 6)
            Case &H21 ' настройки в запросе с байта 11 кадра [SLGT, длина 2, AA, 21, блок, длина 2]
                MaskOldConfig(txMasked, 11)
            Case &H22 ' DEVICE содержит номер звонящего и рабочие буферы; в журнале только заголовок.
                AddRange(rxMasked, 5, If(rx Is Nothing, 0, rx.Length - 5))
        End Select

        ' База номеров - телефоны и имена жильцов: только заголовок кадра и длина.
        Dim baseCmd = (cmd >= &H2A AndAlso cmd <= &H2E) OrElse cmd = &H29
        Write("usb", "port", port, "proto", "SLGT", "cmd", CInt(cmd),
              "tx", If(baseCmd, HexHead(tx, If(cmd = &H2E, 9, If(cmd = &H29, 8, 12))), Hex(tx, txMasked)),
              "rx", If(rx Is Nothing, Nothing, If(baseCmd, HexHead(rx, If(cmd = &H29, 3, 7)), Hex(rx, rxMasked))),
              "ok", rx IsNot Nothing, "err", If(String.IsNullOrEmpty(err), Nothing, err), "ms", ms)
    End Sub

    ' Блок настроек старой прошивки (ConfStr, 512 байт): пароли администратора и
    ' управляющего - байты 9–12, PIN SIM-карты - 55–59.
    Private Sub MaskOldConfig(masked As HashSet(Of Integer), start As Integer)
        AddRange(masked, start + 9, 4)
        AddRange(masked, start + 15, 40) ' APN, логин, пароль
        AddRange(masked, start + 55, 5)
        AddRange(masked, start + 118, 16) ' номер SIM
    End Sub

    Private Function HexHead(b As Byte(), count As Integer) As String
        If b Is Nothing Then Return Nothing
        If b.Length <= count Then Return Hex(b, Nothing)
        Return Hex(b.Take(count).ToArray(), Nothing) & " … (" & b.Length & " байт, номера не пишутся)"
    End Function

    ' Кадры фото старой прошивки (SLGT): запрос целиком, из ответов - вид и длина,
    ' сами данные JPEG в журнал не идут.
    Public Sub OldPhotoFrame(port As String, outgoing As Boolean, frame As Byte())
        If frame Is Nothing Then Return
        If outgoing Then
            Write("photo", "port", port, "dir", "tx", "proto", "SLGT", "hex", Hex(frame, Nothing))
            Return
        End If
        Dim kind As String
        If frame.Length = 1 Then
            kind = If(frame(0) = 10, "готово (10)", "код " & frame(0))
        ElseIf frame.Length = 5 AndAlso frame(0) = 0 Then
            kind = "размер " & BitConverter.ToUInt32(frame, 1)
        ElseIf frame.Length >= 3 AndAlso frame(0) = 1 Then
            kind = "данные " & BitConverter.ToUInt16(frame, 1)
        Else
            kind = "неизвестно: " & Hex(frame.Take(8).ToArray(), Nothing)
        End If
        Write("photo", "port", port, "dir", "rx", "proto", "SLGT", "frame", kind)
    End Sub

    ' Кадры потока фото: пишутся без прореживания, их немного.
    Public Sub PhotoFrame(port As String, outgoing As Boolean, frame As Byte())
        Write("photo", "port", port, "dir", If(outgoing, "tx", "rx"),
              "cmd", If(frame IsNot Nothing AndAlso frame.Length > 8, CInt(frame(8)), -1),
              "len", If(frame Is Nothing, 0, frame.Length),
              "head", If(frame Is Nothing, Nothing, Hex(frame.Take(24).ToArray(), Nothing)))
    End Sub

    Private Function PollDue(key As String) As Boolean
        SyncLock lastPoll
            Dim last As DateTime
            If lastPoll.TryGetValue(key, last) AndAlso DateTime.Now - last < PollEvery Then Return False
            lastPoll(key) = DateTime.Now
            Return True
        End SyncLock
    End Function

    ' Состояние платы NEXT словами - только когда оно меняется.
    Private lastStatusText As String = ""

    Public Sub Status(s As clOpros.s_status)
        Dim text = String.Format("fw={0} build={1} boot=0x{2:X2} hw={3} module={4} gsm={5} gprs={6} server={7} net={8} in={9}{10} out={11}{12} update={13} mem={14}",
                                 s.fw_version, s.fw_build, s.boot_version, s.hw_version, CInt(s.moduleType),
                                 s.gsm, s.gprs, s.connectToServer, CInt(s.netType),
                                 If(s.in1, 1, 0), If(s.in2, 1, 0), If(s.out1, 1, 0), If(s.out2, 1, 0),
                                 If(s.readyToUpdate, 1, 0), s.usedMem)
        If text = lastStatusText Then Return
        lastStatusText = text
        Write("status", "state", text, "signal", CInt(s.signal))
    End Sub

    Private Sub AddRange(set1 As HashSet(Of Integer), start As Integer, count As Integer)
        For i = start To start + count - 1
            set1.Add(i)
        Next
    End Sub

    Private Function Hex(b As Byte(), masked As HashSet(Of Integer)) As String
        If b Is Nothing Then Return Nothing
        Dim sb As New StringBuilder(b.Length * 3)
        For i = 0 To b.Length - 1
            If i > 0 Then sb.Append(" "c)
            If masked IsNot Nothing AndAlso masked.Contains(i) Then
                sb.Append("**")
            Else
                sb.Append(b(i).ToString("X2"))
            End If
        Next
        Return sb.ToString()
    End Function

    ' ******************** окна и оператор ********************

    Private ReadOnly watched As New ConditionalWeakTable(Of Control, Object)

    ' Вызывается из Theme.StyleForm: через него проходят все окна программы.
    Public Sub WatchForm(f As Form)
        AddHandler f.Shown, Sub()
                                Write("window", "event", "open", "form", f.GetType().Name, "title", f.Text)
                                WatchTree(f, f)
                            End Sub
        AddHandler f.FormClosed, Sub(s, e) Write("window", "event", "close", "form", f.GetType().Name,
                                                  "title", f.Text, "result", f.DialogResult.ToString())
    End Sub

    Private Sub WatchTree(f As Form, c As Control)
        Dim dummy As Object = Nothing
        If watched.TryGetValue(c, dummy) Then Return
        watched.Add(c, Nothing)

        If TypeOf c Is Button OrElse TypeOf c Is LinkLabel OrElse (TypeOf c Is Label AndAlso c.Cursor Is Cursors.Hand) Then
            AddHandler c.Click, Sub() Write("click", "form", f.GetType().Name, "control", c.Name, "text", c.Text)
        ElseIf TypeOf c Is CheckBox Then
            Dim cb = DirectCast(c, CheckBox)
            AddHandler cb.CheckedChanged, Sub() Write("check", "form", f.GetType().Name, "control", c.Name, "text", c.Text, "checked", cb.Checked)
        ElseIf TypeOf c Is TabControl Then
            Dim tc = DirectCast(c, TabControl)
            AddHandler tc.SelectedIndexChanged, Sub() Write("tab", "form", f.GetType().Name,
                                                            "tab", If(tc.SelectedTab Is Nothing, "", tc.SelectedTab.Text))
        End If

        AddHandler c.ControlAdded, Sub(s, e) WatchTree(f, e.Control)
        For Each child As Control In c.Controls
            WatchTree(f, child)
        Next
    End Sub

    ' Строка, которую видит оператор: итог операции, состояние, ошибка.
    Public Sub WatchText(l As Control, name As String)
        AddHandler l.TextChanged, Sub()
                                      If l.Text <> "" Then Write("text", "where", name, "text", l.Text)
                                  End Sub
    End Sub

    Public Sub Message(title As String, text As String, isQuestion As Boolean)
        Write("message", "title", title, "text", text, "question", isQuestion)
    End Sub

    Public Sub Failure(where As String, ex As Exception)
        Write("error", "where", where, "type", ex.GetType().FullName, "text", ex.Message, "stack", ex.ToString())
    End Sub
End Module

' Минимальная запись JSON: строки, числа, логические, Nothing, списки и словари.
Public Module Json
    Public Function Str(s As String) As String
        If s Is Nothing Then Return "null"
        Dim sb As New StringBuilder(s.Length + 2)
        sb.Append(""""c)
        For Each ch In s
            Select Case ch
                Case """"c : sb.Append("\""")
                Case "\"c : sb.Append("\\")
                Case ControlChars.Cr : sb.Append("\r")
                Case ControlChars.Lf : sb.Append("\n")
                Case ControlChars.Tab : sb.Append("\t")
                Case Else
                    If AscW(ch) < 32 Then
                        sb.Append("\u").Append(AscW(ch).ToString("x4"))
                    Else
                        sb.Append(ch)
                    End If
            End Select
        Next
        sb.Append(""""c)
        Return sb.ToString()
    End Function

    Public Function Value(v As Object) As String
        If v Is Nothing Then Return "null"
        If TypeOf v Is String Then Return Str(DirectCast(v, String))
        If TypeOf v Is Boolean Then Return If(DirectCast(v, Boolean), "true", "false")
        If TypeOf v Is IDictionary(Of String, Object) Then
            Return "{" & String.Join(",", DirectCast(v, IDictionary(Of String, Object)).Select(Function(p) Str(p.Key) & ":" & Value(p.Value))) & "}"
        End If
        If TypeOf v Is IEnumerable AndAlso Not TypeOf v Is String Then
            Dim items As New List(Of String)
            For Each x In DirectCast(v, IEnumerable)
                items.Add(Value(x))
            Next
            Return "[" & String.Join(",", items) & "]"
        End If
        If TypeOf v Is IFormattable Then Return DirectCast(v, IFormattable).ToString(Nothing, Globalization.CultureInfo.InvariantCulture)
        Return Str(v.ToString())
    End Function
End Module
