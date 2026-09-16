Imports System.IO.Ports
Imports System.Threading

' Демонстрационный режим: программа показывает экраны на выдуманных данных
' и не открывает ни одного порта. Нужен, чтобы проверять внешний вид,
' состояния и ошибки, ничего не отправляя на плату.
'
' Запуск:
'   NextConfig.exe --demo        устройство найдено, статус идёт
'   NextConfig.exe --demo:none   портов нет
'   NextConfig.exe --demo:boot   на порту отвечает загрузчик
Public Enum eDemoMode
    Off = 0
    Found = 1
    NoPorts = 2
    Boot = 3
End Enum

Public Module Demo

    Public ReadOnly Mode As eDemoMode = ParseArgs()

    Public ReadOnly Property IsOn As Boolean
        Get
            Return Mode <> eDemoMode.Off
        End Get
    End Property

    Private Function ParseArgs() As eDemoMode
        For Each a As String In Environment.GetCommandLineArgs()
            Select Case a.Trim().ToLowerInvariant()
                Case "--demo", "/demo", "--demo:found"
                    Return eDemoMode.Found
                Case "--demo:none", "/demo:none"
                    Return eDemoMode.NoPorts
                Case "--demo:boot", "/demo:boot"
                    Return eDemoMode.Boot
            End Select
        Next
        Return eDemoMode.Off
    End Function

End Module

Partial Public Class clOpros

    Private demoTick As Integer = 0

    ' Вместо опроса портов - поток, который выдаёт те же события с выдуманными данными.
    Private Sub StartDemo()
        Comport = New ClComPort(New SerialPort("COM9"), False)

        _exit = False
        _continue = True

        thread_opros = New Threading.Thread(AddressOf Me.DemoCikl)
        thread_opros.Priority = Threading.ThreadPriority.Normal
        thread_opros.Name = "opros demo"
        thread_opros.IsBackground = True
        thread_opros.Start()
    End Sub

    Private Sub DemoCikl()

        Thread.Sleep(700)

        Select Case Demo.Mode

            Case eDemoMode.NoPorts
                RaiseEvent onFind("")

            Case eDemoMode.Boot
                RaiseEvent onFind("COM9")
                Thread.Sleep(900)
                rejim = eRejim.Boot
                RaiseEvent onBootFound("COM9", &H32, 139)

            Case Else
                RaiseEvent onFind("COM9")
                Thread.Sleep(900)
                rejim = eRejim.WaitConnect
                RaiseEvent isFinded("COM9", "867442070217472", 1)

        End Select

        While _continue
            Thread.Sleep(500)

            If rejim = eRejim.Work AndAlso Not _stopGetStatus Then
                demoTick += 1
                RaiseEvent onStatus(DemoStatus())
            End If
        End While

        _exit = True
    End Sub

    ' Правдоподобный статус платы №1: FW 71, загрузчик 0x32, плата 139, на сервере.
    Private Function DemoStatus() As s_status
        Dim s As New s_status

        s.time = CUInt(DateTimeToUnixTimestamp(DateTime.UtcNow))
        s.in1 = ((demoTick \ 8) Mod 2) = 1
        s.in2 = False
        s.out1 = ((demoTick \ 12) Mod 2) = 1
        s.out2 = False
        s.fw_version = 71
        s.fw_build = 1789482488
        s.boot_version = 50
        s.hw_version = 139
        s.signal = CByte(24 + (demoTick Mod 6))
        s.balance = 0
        s.gsm = 1
        s.gprs = 1
        s.connectToServer = 1
        s.netType = eNetType._4G
        s.peripheryState = New Byte(7) {}
        s.usedMem = 1274
        s.moduleType = eModuleType.M_SLM336

        Return s
    End Function

    ' Настройки, какие обычно стоят на стендовой плате.
    Private Function DemoConfig() As s_config
        Dim c As New s_config

        c.time = CUInt(DateTimeToUnixTimestamp(DateTime.UtcNow))
        c.pin = BitConverter.ToUInt32(System.Text.Encoding.UTF8.GetBytes("4321"), 0)
        c.server1 = "87.244.4.92:15010"
        c.server2 = "93.189.229.197:15020"
        c.ntpServer = "pool.ntp.org"
        c.opType = "internet"
        c.netType = eNetType._Авто
        c.countryCode = "7"
        c.simNumber = "+7 900 000-00-00"
        c.ussdBalance = "*100#"
        c.resetTime = 0
        c.fullLog = 0
        c.VoLTE = 1
        c.rs485Mode = 0
        c.enableNTPsync = 1
        c.wiegandType = 26
        c.in1Func = 0
        c.in2Func = 0
        c.useServer1 = 1
        c.useserver2 = 0

        Return c
    End Function

End Class
