Imports System.IO
Imports System.Net
Imports System.Net.Sockets
Imports System.Text
Imports System.Threading

' Канал для помощника (ключ --agent): программа слушает только 127.0.0.1 и
' отвечает на команды из tools\nc_agent.ps1 — какие окна открыты, что в полях,
' снимок окна, нажать кнопку, открыть вкладку, ввести текст, хвост журнала.
' Всё, что пишет в плату, через канал не проходит: это делает оператор.
'
' Запрос - одна строка UTF-8: токен, команда, аргументы через Tab.
' Ответ - одна строка JSON. Порт и токен - в %LOCALAPPDATA%\NextConfig\agent.json.
Public Module AgentChannel

    Private Const FirstPort As Integer = 47300
    Private listener As TcpListener
    Private invoker As Control
    Private token As String

    ' Кнопки, которые пишут в плату или запускают действие на ней.
    Private ReadOnly BlockedNames As New HashSet(Of String) From {
        "btnSave", "btnDefault", "btnFactory", "btnClear", "btnUpdateFW", "btnUpdateBoot",
        "btnTestOUT1", "btnTestOUT2", "btnTranspMode", "btnUnConnect", "btnImport",
        "Label16", "Label19", "Label20"}
    Private ReadOnly BlockedTexts As New HashSet(Of String) From {
        "Перевести на новую систему", "Обновить прошивку", "Сделать снимок", "Забрать фото"}

    Public Sub Start()
        ' создаётся в потоке окна: через него команды попадают в этот поток
        invoker = New Control()
        invoker.CreateControl()
        Dim unused = invoker.Handle

        token = Guid.NewGuid().ToString("N")
        For p = FirstPort To FirstPort + 9
            Try
                listener = New TcpListener(IPAddress.Loopback, p)
                listener.Start()
                Exit For
            Catch
                listener = Nothing
            End Try
        Next
        If listener Is Nothing Then
            AgentLog.Write("agent", "event", "no free port")
            Return
        End If

        Dim port = DirectCast(listener.LocalEndpoint, IPEndPoint).Port
        Try
            Directory.CreateDirectory(AgentLog.Folder)
            File.WriteAllText(Path.Combine(AgentLog.Folder, "agent.json"),
                              "{""port"":" & port & ",""token"":" & Json.Str(token) & ",""pid"":" & Process.GetCurrentProcess().Id & "}")
        Catch
        End Try
        AgentLog.Write("agent", "event", "listen", "port", port)

        Dim t As New Thread(AddressOf AcceptLoop) With {.IsBackground = True, .Name = "agent"}
        t.Start()
    End Sub

    Public Sub StopChannel()
        Try
            If listener IsNot Nothing Then listener.Stop()
            File.Delete(Path.Combine(AgentLog.Folder, "agent.json"))
        Catch
        End Try
    End Sub

    Private Sub AcceptLoop()
        Do
            Dim client As TcpClient
            Try
                client = listener.AcceptTcpClient()
            Catch
                Return ' канал остановлен
            End Try
            Try
                Using client
                    client.ReceiveTimeout = 5000
                    Dim stream = client.GetStream()
                    Dim reader As New StreamReader(stream, New UTF8Encoding(False))
                    Dim line = reader.ReadLine()
                    Dim reply = Handle(If(line, ""))
                    Dim bytes = New UTF8Encoding(False).GetBytes(reply & vbLf)
                    stream.Write(bytes, 0, bytes.Length)
                End Using
            Catch ex As Exception
                AgentLog.Failure("agent", ex)
            End Try
        Loop
    End Sub

    Private Function Handle(line As String) As String
        Dim parts = line.Split(ControlChars.Tab)
        If parts.Length < 2 OrElse parts(0) <> token Then Return Fail("неверный токен")
        Dim cmd = parts(1).Trim().ToLowerInvariant()
        Dim args = parts.Skip(2).ToArray()
        If cmd <> "log" Then AgentLog.Write("agent", "cmd", cmd, "args", String.Join(" | ", args))

        Try
            Select Case cmd
                Case "ping" : Return Ok("pong", True)
                Case "log" : Return Ok("lines", AgentLog.Tail(If(args.Length > 0, CInt(args(0)), 50)))
                Case "windows" : Return OnUi(AddressOf Windows)
                Case "tree" : Return OnUi(Function() Tree(Arg(args, 0), Arg(args, 1) = "all"))
                Case "snap" : Return OnUi(Function() Snap(Arg(args, 0), Arg(args, 1)))
                Case "click" : Return OnUi(Function() Click(Arg(args, 0), Arg(args, 1)))
                Case "type" : Return OnUi(Function() TypeText(Arg(args, 0), Arg(args, 1), Arg(args, 2)))
                Case "select" : Return OnUi(Function() SelectItem(Arg(args, 0), Arg(args, 1), Arg(args, 2)))
                Case "close" : Return OnUi(Function() CloseForm(Arg(args, 0)))
                Case Else
                    Return Fail("команды: ping, windows, tree [окно] [all], snap [окно] [файл], click <текст|имя> [окно], " &
                                "type <имя> <текст> [окно], select <имя> <пункт> [окно], close [окно], log [n]")
            End Select
        Catch ex As Exception
            Return Fail(ex.Message)
        End Try
    End Function

    Private Function Arg(a As String(), i As Integer) As String
        Return If(i < a.Length, a(i), "")
    End Function

    ' Выполнить в потоке окна. Окно может быть занято (модальный диалог ждёт
    ' ответа платы) - тогда отвечаем «занято», а не висим.
    Private Function OnUi(work As Func(Of String)) As String
        Dim ar = invoker.BeginInvoke(work)
        If Not ar.AsyncWaitHandle.WaitOne(5000) Then Return Fail("окно программы занято больше 5 с")
        Return CStr(invoker.EndInvoke(ar))
    End Function

    Private Function Ok(ParamArray pairs As Object()) As String
        Dim d As New Dictionary(Of String, Object) From {{"ok", True}}
        For i = 0 To pairs.Length - 2 Step 2
            d(CStr(pairs(i))) = pairs(i + 1)
        Next
        Return Json.Value(d)
    End Function

    Private Function Fail(text As String) As String
        Return Json.Value(New Dictionary(Of String, Object) From {{"ok", False}, {"error", text}})
    End Function

    ' ******************** окна ********************

    Private Function OpenForms() As List(Of Form)
        Return Application.OpenForms.Cast(Of Form)().ToList()
    End Function

    Private Function Windows() As String
        Dim list As New List(Of Object)
        Dim forms = OpenForms()
        For i = 0 To forms.Count - 1
            Dim f = forms(i)
            list.Add(New Dictionary(Of String, Object) From {
                {"i", i}, {"form", f.GetType().Name}, {"title", f.Text},
                {"visible", f.Visible}, {"active", f Is Form.ActiveForm}, {"modal", f.Modal}})
        Next
        Return Ok("windows", list)
    End Function

    ' Окно по номеру, имени класса или части заголовка; пусто - верхнее видимое.
    Private Function FindForm(key As String) As Form
        Dim forms = OpenForms().Where(Function(f) f.Visible).ToList()
        If forms.Count = 0 Then Throw New InvalidOperationException("нет открытых окон")
        If key = "" Then
            If Form.ActiveForm IsNot Nothing AndAlso forms.Contains(Form.ActiveForm) Then Return Form.ActiveForm
            Dim modal = forms.LastOrDefault(Function(f) f.Modal)
            Return If(modal, forms.Last())
        End If
        Dim n As Integer
        If Integer.TryParse(key, n) Then
            Dim all = OpenForms()
            If n >= 0 AndAlso n < all.Count Then Return all(n)
        End If
        Dim found = forms.LastOrDefault(Function(f) String.Equals(f.GetType().Name, key, StringComparison.OrdinalIgnoreCase))
        If found Is Nothing Then found = forms.LastOrDefault(Function(f) f.Text.IndexOf(key, StringComparison.CurrentCultureIgnoreCase) >= 0)
        If found Is Nothing Then Throw New InvalidOperationException("окно не найдено: " & key)
        Return found
    End Function

    Private Function Tree(formKey As String, all As Boolean) As String
        Dim f = FindForm(formKey)
        Dim nodes As New List(Of Object)
        Walk(f, 0, all, nodes)
        Return Ok("form", f.GetType().Name, "title", f.Text, "controls", nodes)
    End Function

    Private Sub Walk(c As Control, depth As Integer, all As Boolean, nodes As List(Of Object))
        For Each child As Control In c.Controls
            If Not all AndAlso Not child.Visible Then Continue For
            Dim n As New Dictionary(Of String, Object) From {{"d", depth}, {"type", child.GetType().Name}}
            If child.Name <> "" Then n("name") = child.Name
            Dim text = VisibleText(child)
            If text <> "" Then n("text") = text
            If Not child.Enabled Then n("enabled") = False

            If TypeOf child Is CheckBox Then n("checked") = DirectCast(child, CheckBox).Checked
            If TypeOf child Is RadioButton Then n("checked") = DirectCast(child, RadioButton).Checked
            If TypeOf child Is ProgressBar Then n("value") = DirectCast(child, ProgressBar).Value
            If TypeOf child Is TabControl Then
                Dim tc = DirectCast(child, TabControl)
                n("tabs") = tc.TabPages.Cast(Of TabPage)().Select(Function(p) p.Text).ToList()
                n("selected") = If(tc.SelectedTab Is Nothing, "", tc.SelectedTab.Text)
            End If
            If TypeOf child Is ComboBox Then
                Dim cb = DirectCast(child, ComboBox)
                n("items") = cb.Items.Cast(Of Object)().Select(Function(x) cb.GetItemText(x)).ToList()
                n("selected") = cb.SelectedIndex
            End If
            If TypeOf child Is ListBox Then
                Dim lb = DirectCast(child, ListBox)
                n("items") = lb.Items.Cast(Of Object)().Select(Function(x) lb.GetItemText(x)).ToList()
                n("selected") = lb.SelectedIndex
            End If
            If TypeOf child Is DataGridView Then n("grid") = GridRows(DirectCast(child, DataGridView))

            nodes.Add(n)
            If Not TypeOf child Is DataGridView Then Walk(child, depth + 1, all, nodes)
        Next
    End Sub

    ' Текст так, как его видит оператор; пароль - только длиной.
    Private Function VisibleText(c As Control) As String
        If TypeOf c Is TextBox Then
            Dim t = DirectCast(c, TextBox)
            If t.UseSystemPasswordChar OrElse t.PasswordChar <> ChrW(0) Then Return New String("•"c, t.TextLength)
        End If
        If TypeOf c Is RichTextBox Then
            Dim s = c.Text
            Return If(s.Length > 3000, "…" & s.Substring(s.Length - 3000), s)
        End If
        Return c.Text
    End Function

    Private Function GridRows(g As DataGridView) As Object
        Dim cols = g.Columns.Cast(Of DataGridViewColumn)().Where(Function(c) c.Visible).OrderBy(Function(c) c.DisplayIndex).ToList()
        Dim rows As New List(Of Object)
        For Each r As DataGridViewRow In g.Rows
            If rows.Count >= 300 Then Exit For
            If r.IsNewRow Then Continue For
            rows.Add(cols.Select(Function(c) Convert.ToString(r.Cells(c.Index).FormattedValue)).ToList())
        Next
        Return New Dictionary(Of String, Object) From {
            {"columns", cols.Select(Function(c) c.HeaderText).ToList()}, {"rows", rows}, {"total", g.Rows.Count}}
    End Function

    Private Function Snap(formKey As String, file As String) As String
        Dim f = FindForm(formKey)
        If file = "" Then
            Dim dir = Path.Combine(AgentLog.Folder, "snaps")
            Directory.CreateDirectory(dir)
            file = Path.Combine(dir, "snap_" & DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") & ".png")
        End If
        Using bmp As New Bitmap(f.Width, f.Height)
            f.DrawToBitmap(bmp, New Rectangle(0, 0, f.Width, f.Height))
            bmp.Save(file, Imaging.ImageFormat.Png)
        End Using
        Return Ok("file", file, "form", f.GetType().Name)
    End Function

    ' ******************** действия ********************

    Private Function FindControl(f As Form, key As String, Optional kind As Type = Nothing) As Control
        Dim found As Control = Nothing
        Dim clean = key.Replace("&", "").Trim()
        Dim visit As Action(Of Control) = Nothing
        visit = Sub(c)
                    For Each child As Control In c.Controls
                        If found IsNot Nothing Then Return
                        If child.Visible AndAlso (kind Is Nothing OrElse kind.IsInstanceOfType(child)) AndAlso
                           (child.Name = key OrElse child.Text.Replace("&", "").Trim() = clean) Then
                            found = child : Return
                        End If
                        visit(child)
                    Next
                End Sub
        visit(f)
        Return found
    End Function

    Private Function Click(key As String, formKey As String) As String
        Dim f = FindForm(formKey)

        ' вкладка: выбрать её
        For Each tc In AllControls(f).OfType(Of TabControl)()
            For Each page As TabPage In tc.TabPages
                If page.Text = key Then
                    tc.SelectedTab = page
                    Return Ok("tab", page.Text)
                End If
            Next
        Next

        Dim c = FindControl(f, key)
        If c Is Nothing Then Return Fail("не найдено в окне " & f.GetType().Name & ": " & key)
        If Not c.Enabled Then Return Fail("элемент недоступен: " & c.Text)
        Dim why = Blocked(f, c)
        If why <> "" Then
            AgentLog.Write("agent", "refused", c.Name, "text", c.Text, "why", why)
            Return Fail("запрещено каналом: " & why)
        End If

        ' нажатие отложено: кнопка может открыть модальное окно, ответ не ждёт его
        invoker.BeginInvoke(New Action(Sub()
                                           If TypeOf c Is Button Then
                                               DirectCast(c, Button).PerformClick()
                                           ElseIf TypeOf c Is CheckBox Then
                                               Dim cb = DirectCast(c, CheckBox) : cb.Checked = Not cb.Checked
                                           ElseIf TypeOf c Is RadioButton Then
                                               DirectCast(c, RadioButton).Checked = True
                                           Else
                                               GetType(Control).GetMethod("OnClick", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).
                                                   Invoke(c, New Object() {EventArgs.Empty})
                                           End If
                                       End Sub))
        Return Ok("clicked", c.Text, "name", c.Name, "form", f.GetType().Name)
    End Function

    Private Function AllControls(root As Control) As IEnumerable(Of Control)
        Dim list As New List(Of Control)
        Dim visit As Action(Of Control) = Nothing
        visit = Sub(c)
                    For Each child As Control In c.Controls
                        list.Add(child) : visit(child)
                    Next
                End Sub
        visit(root)
        Return list
    End Function

    ' Пусто - можно нажимать; иначе причина отказа.
    Private Function Blocked(f As Form, c As Control) As String
        If TypeOf f Is frmFW AndAlso c IsNot DirectCast(f, frmFW).Button2 Then
            Return "запись прошивки и загрузчика запускает только оператор"
        End If
        If TypeOf f Is frmSelecter AndAlso c.Text <> "Отмена" Then
            Return "смена типа модуля перезагружает контроллер"
        End If
        If TypeOf f Is frmInfo AndAlso DirectCast(f, frmInfo).Button2.Visible AndAlso c Is DirectCast(f, frmInfo).Button1 Then
            Return "подтверждение действия - за оператором"
        End If
        If BlockedNames.Contains(c.Name) OrElse BlockedTexts.Contains(c.Text) Then
            Return "действие пишет в плату или запускает на ней операцию"
        End If
        Return ""
    End Function

    ' Закрыть окно как крестиком: окно записи прошивки само не даст закрыться посреди работы.
    Private Function CloseForm(formKey As String) As String
        Dim f = FindForm(formKey)
        invoker.BeginInvoke(New Action(Sub() f.Close()))
        Return Ok("closing", f.GetType().Name)
    End Function

    Private Function TypeText(name As String, text As String, formKey As String) As String
        Dim f = FindForm(formKey)
        Dim c = TryCast(FindControl(f, name, GetType(TextBox)), TextBox)
        If c Is Nothing Then Return Fail("нет поля " & name)
        If c.UseSystemPasswordChar OrElse c.PasswordChar <> ChrW(0) Then Return Fail("пароль вводит оператор")
        c.Text = text
        Return Ok("field", c.Name, "text", c.Text)
    End Function

    Private Function SelectItem(name As String, item As String, formKey As String) As String
        Dim f = FindForm(formKey)
        Dim c = TryCast(FindControl(f, name, GetType(ListControl)), ListControl)
        If c Is Nothing Then Return Fail("нет списка " & name)
        Dim items = If(TypeOf c Is ComboBox, DirectCast(c, ComboBox).Items, DirectCast(c, ListBox).Items)
        For i = 0 To items.Count - 1
            ' пункт по полному тексту или по началу (у платы - порт)
            Dim itemText = c.GetItemText(items(i))
            If itemText = item OrElse itemText.StartsWith(item & " ") Then
                c.SelectedIndex = i
                Return Ok("list", c.Name, "selected", itemText)
            End If
        Next
        Return Fail("нет пункта " & item)
    End Function
End Module
