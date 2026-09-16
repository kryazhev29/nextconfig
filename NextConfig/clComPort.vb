Imports System.IO.Ports
Imports System.Threading

Public Class ClComPort
    Implements IDisposable

    Public Event ReceiveChar(ByVal value As Char)
    Public Event ReceiveComand(ByVal value As String)
    Public Event ReceiveBytes(ByVal buffer As Byte())
    Public Event ErrorPort()

    ' Строка журнала в демонстрационном режиме: порт закрыт, а показать
    ' работу журнала надо.
    Friend Sub DemoReceive(ByVal line As String)
        If Demo.IsOn Then RaiseEvent ReceiveComand(line)
    End Sub

    Private ReadOnly log As New ClLog
    Private WithEvents _port As SerialPort
    Public EnableEventReceived As Boolean = False
    Private sCommand As String = vbNullString

    Public Sub New(ByVal _SerialPort As SerialPort, ByVal enable_log As Boolean)

        MyBase.New()

        _port = _SerialPort
        log.EnableLog = enable_log
        If log.EnableLog Then
            log.Open("ERROR", Now.ToString("yyyyMMdd_HHmm"), "ФАЙЛ ОШИБОК ПО ИНТЕРФЕЙСУ")
        End If

    End Sub

    Public ReadOnly Property Port() As SerialPort
        Get
            Return _port
        End Get
    End Property

    Public Enum EProtokol
        ASCII
        BIN
    End Enum

    Private _protokol As EProtokol = EProtokol.BIN

    Public ReadOnly Property Loging() As ClLog
        Get
            Return log
        End Get
    End Property

    ''' <summary>
    ''' выбор режима работы ASCII или BIN
    ''' </summary>
    ''' <value></value>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Property Protokol() As EProtokol
        Get
            Return _protokol
        End Get
        Set(ByVal value As EProtokol)
            _protokol = value
        End Set
    End Property

    ''' <summary>
    ''' проверка контрольной суммы строки
    ''' </summary>
    ''' <param name="val"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function CheckCRC(ByRef val As String, Optional ByVal RemoveCS As Boolean = False) As Boolean

        CheckCRC = False
        Try
            If val.Length > 2 Then
                Dim iCRC1 As Integer = 0
                Dim iCRC2 As Integer = Convert.ToInt32(val.Substring(val.Length - 2), 16)

                For i As Integer = 0 To val.Length - 3
                    iCRC1 += Asc(val.Substring(i, 1))
                Next

                If (iCRC1 And 255) = iCRC2 Then
                    CheckCRC = True

                    If RemoveCS Then
                        Dim len As Integer = val.Length
                        val = val.Remove(len - 2, 2)
                    End If

                End If

            End If
        Catch ex As Exception

        End Try

    End Function

    ''' <summary>
    ''' добавляет контрольную сумму к строке
    ''' </summary>
    ''' <param name="val"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function AddCRC(ByVal val As String) As String
        Dim cs As Long = 0
        For Each ch As Char In val
            cs += Asc(ch)
        Next

        cs = cs And 255

        Return val & Dtohc(cs)
    End Function

    ''' <summary>
    ''' преобразует в HEX формат с одним лидирующим нулем
    '''  val - не более 255
    ''' </summary>
    ''' <param name="val"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function Dtohc(ByVal val As Integer)
        val = val And 255
        If val < 16 Then Return "0" & val.ToString("X")
        Return val.ToString("X")
    End Function

    ''' <summary>
    ''' процедура чтения записи по последовательному порту
    ''' Rec - принятая строка
    ''' Send - отправляемая строка
    ''' TimeOut - тайм аут на прием
    ''' CheckSum - контрольная сумма
    ''' Count - количество попыток посылок
    ''' SendVBCr - отправлять ли VbCr
    ''' Contain - список допустимых ответов на запрос
    ''' </summary>
    ''' <param name="Rec"></param>
    ''' <param name="Send"></param>
    ''' <param name="TimeOut"></param>
    ''' <param name="CheckSum"></param>
    ''' <param name="SendVbCR"></param>
    ''' <param name="Contain"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Overloads Function SendRec(ByRef Rec As String, _
                                      ByVal Send As String, _
                                      ByVal TimeOut As Integer, _
                                      ByVal CheckSum As Boolean, _
                                      ByVal SendVbCR As Boolean, _
                                      ByVal SendVbLF As Boolean, _
                                      ByVal RemoveCS As Boolean, _
                                      ByVal ParamArray Contain() As String) As Boolean

        If _port IsNot Nothing AndAlso _port.IsOpen Then

            Dim TimeStart As Integer

            If CheckSum Then Send = AddCRC(Send)

            If SendVbCR Then
                Send &= vbCr
            End If

            If SendVbLF Then
                Send &= vbLf
            End If

            Try
                _port.DiscardOutBuffer()
                _port.DiscardInBuffer()
                _port.Write(Send)

                Do While _port.BytesToWrite > 0
                    Thread.Sleep(1)
                Loop

                TimeStart = Environment.TickCount
                Rec = ""

                'reception
                Do While (Environment.TickCount - TimeStart) < TimeOut

                    If _port.BytesToRead Then TimeStart = Environment.TickCount

                    Dim rbuf As String = _port.ReadExisting()

                    For Each recchar In rbuf
                        If (recchar = ControlChars.Cr) OrElse (recchar = ControlChars.Lf) Then
                            'пришел CRLF проверяем на наличие ответа
                            If Rec.Length > 2 Then
                                If CheckSum Then
                                    If CheckCRC(Rec, RemoveCS) Then
                                        For Each vl As String In Contain
                                            If Rec.IndexOf(vl) <> -1 Then
                                                Return True
                                            End If
                                        Next
                                    End If
                                    '//--
                                    Return False
                                Else
                                    For Each vl As String In Contain
                                        If Rec.IndexOf(vl) <> -1 Then
                                            Return True
                                        End If
                                    Next
                                    '//--
                                    Return False
                                End If
                            Else
                                Debug.Write("else")
                            End If
                        Else
                            Rec &= recchar
                        End If
                    Next
                    Thread.Sleep(1)
                Loop
            Catch ex As Exception
                Debug.WriteLine("Exception - " & ex.Message &
                                " send - " & Send & " rec - " & Rec)
                log.Write(Now.ToShortTimeString & " S = " & Send & " R = " & Rec & " Exception: " & ex.Message)

                Return False
            End Try

            Debug.WriteLine("TimeOut - " & (Environment.TickCount - TimeStart).ToString &
                            " send - " & Send & " rec - " & Rec)
            log.Write(Now.ToShortTimeString & " S = " & Send & " R = " & Rec)

            Return False
        Else
            Return False
        End If

        Return False

    End Function

    Private Function Crc16(ByVal modbusframe As Byte(), Optional ByVal start As Integer = 0) As Integer

        Dim reg_crc As Integer = &HFFFF

        For i As Integer = start To modbusframe.GetUpperBound(0)
            reg_crc = reg_crc Xor modbusframe(i)
            For j As Integer = 0 To 7
                If reg_crc And &H1 Then
                    reg_crc = (reg_crc >> 1) Xor &HA001
                Else
                    reg_crc >>= 1
                End If
            Next
        Next

        Return reg_crc

    End Function

    Public Function Crc16(ByVal modbusframe As Byte(), ByVal start As Integer, ByVal len As Integer) As Integer

        Dim reg_crc As Integer = &HFFFF

        For i As Integer = start To (start + len - 1)
            reg_crc = reg_crc Xor modbusframe(i)
            For j As Integer = 0 To 7
                If reg_crc And &H1 Then
                    reg_crc = (reg_crc >> 1) Xor &HA001
                Else
                    reg_crc >>= 1
                End If
            Next
        Next

        Return reg_crc

    End Function

    Public Overloads Function SendRec_ModbusRtu(ByRef data_rec As Byte(), _
                                                ByVal command As Byte(), _
                                                ByVal timeout As Integer, _
                                                Optional ByVal swap_crc As Boolean = False, _
                                                Optional ByVal withSleep As Boolean = True) As Boolean

        If _port IsNot Nothing AndAlso _port.IsOpen Then

            'формируем команду
            Dim al As New List(Of Byte)
            Dim bytes As Byte()
            Dim adr As Byte = command(0)

            For Each dt As Byte In command
                al.Add(dt) 'command
            Next

            Dim crc As Byte() = BitConverter.GetBytes(Convert.ToInt32(Crc16(al.ToArray)))
            Dim TimeStart As Integer
            Dim readedBytes As Integer

            If swap_crc = False Then
                al.Add(crc(0))
                al.Add(crc(1))
            Else
                al.Add(crc(1))
                al.Add(crc(0))
            End If

            bytes = al.ToArray

            Try
                _port.DiscardInBuffer()
                _port.DiscardOutBuffer()
                _port.Write(bytes, 0, bytes.Length)

                Do While _port.BytesToWrite > 0
                    If withSleep Then Thread.Sleep(1)
                Loop

                'log.Write(Now.ToShortTimeString & " S = " & BitConverter.ToString(bytes))

                TimeStart = Environment.TickCount
                al.Clear()

                Do While (Environment.TickCount - TimeStart) < timeout
                    '_port.BytesToRead <> Nothing AndAlso _port.BytesToRead
                    readedBytes = _port.BytesToRead
                    If readedBytes > 0 Then
                        Dim bBuf(readedBytes - 1) As Byte

                        _port.Read(bBuf, 0, readedBytes)
                        TimeStart = Environment.TickCount

                        ' log.Write(Now.ToShortTimeString & " R = " & BitConverter.ToString(bBuf))

                        For Each bt In bBuf

                            Select Case al.Count
                                Case 0 'find prefix
                                    If bt = adr Then
                                        al.Add(bt)
                                    End If
                                Case Else
                                    al.Add(bt)
                            End Select

                            If al.Count > 2 Then
                                Select Case al.Item(1)
                                    Case 3
                                        If al.Count = (al.Item(2) + 5) Then
                                            data_rec = al.ToArray
                                            Dim crc_rec As Integer = data_rec(data_rec.GetUpperBound(0))
                                            crc_rec <<= 8
                                            crc_rec += data_rec(data_rec.GetUpperBound(0) - 1)

                                            Dim t_data_rec(data_rec.Length - 3) As Byte
                                            Array.Copy(data_rec, 0, t_data_rec, 0, t_data_rec.Length)
                                            Dim crc_16 As Integer = Crc16(t_data_rec)
                                            If crc_16 = crc_rec Then
                                                Return True
                                            Else
                                                Exit Do
                                            End If
                                        End If

                                    Case 5, 6, 16
                                        If al.Count = 8 Then
                                            data_rec = al.ToArray
                                            Dim crc_rec As Integer

                                            If swap_crc = False Then
                                                crc_rec = data_rec(data_rec.GetUpperBound(0))
                                                crc_rec <<= 8
                                                crc_rec += data_rec(data_rec.GetUpperBound(0) - 1)
                                            Else
                                                crc_rec = data_rec(data_rec.GetUpperBound(0) - 1)
                                                crc_rec <<= 8
                                                crc_rec += data_rec(data_rec.GetUpperBound(0))
                                            End If

                                            Dim t_data_rec(data_rec.Length - 3) As Byte
                                            Array.Copy(data_rec, 0, t_data_rec, 0, t_data_rec.Length)
                                            Dim crc_16 As Integer = Crc16(t_data_rec)
                                            If crc_16 = crc_rec Then
                                                Return True
                                            Else
                                                Exit Do
                                            End If
                                        End If

                                    Case Else
                                        If al.Count = (al.Item(2) + 5) Then
                                            data_rec = al.ToArray

                                            Dim crc_rec As Integer

                                            If swap_crc = False Then
                                                crc_rec = data_rec(data_rec.GetUpperBound(0))
                                                crc_rec <<= 8
                                                crc_rec += data_rec(data_rec.GetUpperBound(0) - 1)
                                            Else
                                                crc_rec = data_rec(data_rec.GetUpperBound(0) - 1)
                                                crc_rec <<= 8
                                                crc_rec += data_rec(data_rec.GetUpperBound(0))
                                            End If

                                            Dim t_data_rec(data_rec.Length - 3) As Byte
                                            Array.Copy(data_rec, 0, t_data_rec, 0, t_data_rec.Length)
                                            Dim crc_16 As Integer = Crc16(t_data_rec)
                                            If crc_16 = crc_rec Then
                                                Return True
                                            Else
                                                Exit Do
                                            End If
                                        End If
                                End Select
                            End If
                        Next
                    End If

                    If withSleep Then Thread.Sleep(1)
                Loop

            Catch ex As Exception
                Debug.WriteLine("Exception - " & ex.Message &
                                " modbus address - " & command(0).ToString)
                log.Write(Now.ToShortTimeString & " S = " & BitConverter.ToString(command) &
                          " R = " & BitConverter.ToString(al.ToArray) & " Exception: " & ex.Message)
                Return False
            End Try

            Debug.WriteLine("TimeOut - " & (Environment.TickCount - TimeStart).ToString &
                            " modbus address - " & command(0).ToString)
            log.Write(Now.ToShortTimeString & " S = " & BitConverter.ToString(command) &
                      " R = " & BitConverter.ToString(al.ToArray))
        End If

        Return False
    End Function

    Public Overloads Function SendRecCmd(ByRef data_rec As Byte(),
                                         ByVal command As Byte(),
                                         ByVal timeout As Integer,
                                         Optional ByVal swap_crc As Boolean = False,
                                         Optional ByVal withSleep As Boolean = True) As Boolean

        If _port IsNot Nothing AndAlso _port.IsOpen Then

            EnableEventReceived = False

            'формируем команду
            Dim al As New List(Of Byte)
            Dim bytes As Byte()

            For Each dt As Byte In command
                al.Add(dt)
            Next

            Dim crc As Byte() = BitConverter.GetBytes(Convert.ToInt32(Crc16(al.ToArray, 4)))
            Dim TimeStart As Integer
            Dim readedBytes As Integer
            Dim len As Integer

            If swap_crc = False Then
                al.Add(crc(0))
                al.Add(crc(1))
            Else
                al.Add(crc(1))
                al.Add(crc(0))
            End If

            bytes = al.ToArray

            Try
                _port.DiscardInBuffer()
                _port.DiscardOutBuffer()
                _port.Write(bytes, 0, bytes.Length)

                Do While _port.BytesToWrite > 0
                    If withSleep Then Thread.Sleep(1)
                Loop

                TimeStart = Environment.TickCount
                al.Clear()

                Do While (Environment.TickCount - TimeStart) < timeout
                    readedBytes = _port.BytesToRead
                    If readedBytes > 0 Then
                        Dim bBuf(readedBytes - 1) As Byte

                        _port.Read(bBuf, 0, readedBytes)
                        TimeStart = Environment.TickCount

                        For Each bt In bBuf

                            Select Case al.Count
                                Case 0 'find prefix SLDN
                                    If bt = 83 Then
                                        al.Add(bt)
                                    End If
                                Case 1
                                    If bt = 76 Then
                                        al.Add(bt)
                                    End If
                                Case 2
                                    If bt = 68 Then
                                        al.Add(bt)
                                    End If
                                Case 3
                                    If bt = 78 Then
                                        al.Add(bt)
                                    End If
                                Case 4
                                    len = bt
                                    al.Add(bt)
                                Case 5
                                    len += CType(bt, UShort) << 8
                                    al.Add(bt)
                                Case Else
                                    al.Add(bt)
                            End Select

                            If al.Count > 6 Then
                                If al.Count = len + 8 Then
                                    data_rec = al.ToArray

                                    Dim crc_rec As Integer = data_rec(data_rec.GetUpperBound(0))

                                    crc_rec <<= 8
                                    crc_rec += data_rec(data_rec.GetUpperBound(0) - 1)

                                    Dim t_data_rec(data_rec.Length - 7) As Byte

                                    Array.Copy(data_rec, 4, t_data_rec, 0, t_data_rec.Length)

                                    Dim crc_16 As Integer = Crc16(t_data_rec)

                                    If crc_16 = crc_rec Then
                                        EnableEventReceived = True
                                        Return True
                                    Else
                                        Exit Do
                                    End If

                                End If

                            End If
                        Next
                    End If

                    If withSleep Then Thread.Sleep(1)
                Loop

            Catch ex As Exception
                EnableEventReceived = True
                Return False
            End Try

            EnableEventReceived = True
        Else
            ' в демонстрационном режиме порт не открывается намеренно:
            ' это не потеря связи с контроллером
            If Not Demo.IsOn Then RaiseEvent ErrorPort()
        End If

        Return False
    End Function

    Private Sub Port_DataReceived(ByVal sender As Object, ByVal e As System.IO.Ports.SerialDataReceivedEventArgs) Handles _port.DataReceived
        If EnableEventReceived = False Then Exit Sub

        Dim iPnt As Int32, Buffer() As Byte = {0, 13, 16}
        Dim oEnc As System.Text.Encoding = System.Text.Encoding.GetEncoding("ASCII") '1252)
        '-------------------------------------------------------------
        If e.EventType = IO.Ports.SerialData.Chars Then

            Try
                If CType(sender, IO.Ports.SerialPort).BytesToRead > 0 Then
                    ReDim Buffer(CType(sender, IO.Ports.SerialPort).BytesToRead - 1)
                    CType(sender, IO.Ports.SerialPort).Read(Buffer, 0, CType(sender, IO.Ports.SerialPort).BytesToRead)
                End If
            Catch
            End Try

            Debug.WriteLine(Buffer.ToString)

            'проверим на наличие команды
            CheckReceivedBytes(Buffer)

            Dim ch As Char

            For iPnt = 0 To Buffer.Length - 1
                ch = oEnc.GetString(Buffer, iPnt, 1)

                If ch = vbCr Then
                    RaiseEvent ReceiveComand(sCommand)
                    sCommand = vbNullString
                Else
                    sCommand &= ch
                End If

                RaiseEvent ReceiveChar(ch)
            Next

        End If

    End Sub

    Public Sub CheckReceivedBytes(ByVal buffer As Byte())
        Dim al As New List(Of Byte)
        Dim len As Integer
        Dim data_rec As Byte()

        al.Clear()

        For Each bt In buffer

            Select Case al.Count
                Case 0 'find prefix SLDN
                    If bt = 83 Then
                        al.Add(bt)
                    End If
                Case 1
                    If bt = 76 Then
                        al.Add(bt)
                    End If
                Case 2
                    If bt = 68 Then
                        al.Add(bt)
                    End If
                Case 3
                    If bt = 78 Then
                        al.Add(bt)
                    End If
                Case 4
                    len = bt
                    al.Add(bt)
                Case 5
                    len += CType(bt, UShort) << 8
                    al.Add(bt)
                Case Else
                    al.Add(bt)
            End Select

            If al.Count > 6 Then
                If al.Count = len + 8 Then
                    data_rec = al.ToArray

                    Dim crc_rec As Integer = data_rec(data_rec.GetUpperBound(0))

                    crc_rec <<= 8
                    crc_rec += data_rec(data_rec.GetUpperBound(0) - 1)

                    Dim t_data_rec(data_rec.Length - 7) As Byte

                    Array.Copy(data_rec, 4, t_data_rec, 0, t_data_rec.Length)

                    Dim crc_16 As Integer = Crc16(t_data_rec)

                    If crc_16 = crc_rec Then
                        RaiseEvent ReceiveBytes(data_rec)
                    End If
                End If
            End If
        Next

    End Sub

#Region " IDisposable Support "

    Private disposedValue As Boolean = False        ' Чтобы обнаружить избыточные вызовы

    ' IDisposable
    Protected Overridable Sub Dispose(ByVal disposing As Boolean)
        If Not disposedValue Then
            If disposing Then
                ' TODO: освободить другие состояния (управляемые объекты).
                If _port.IsOpen Then _port.Close()
                _port.Dispose()

                'log.Dispose()

            End If

            ' TODO: освободить собственные состояния (неуправляемые объекты).
            ' TODO: задать большие поля как null.
        End If
        disposedValue = True
    End Sub

    ' Этот код добавлен редактором Visual Basic для правильной реализации шаблона высвобождаемого класса.
    Public Sub Dispose() Implements IDisposable.Dispose
        ' Не изменяйте этот код. Разместите код очистки выше в Dispose(ByVal disposing As Boolean).
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub
#End Region

End Class