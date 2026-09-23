Imports System.IO
Imports System.Text
Imports System.Windows.Forms

' Доработка: Кряжев А., 2026
Public Class PeripheryNames
    Public ReadOnly Sections As New Dictionary(Of String, SortedDictionary(Of Byte, String))
    Public Notice As String = ""
    Public Sub New()
        Dim lines As String()
        Dim path = IO.Path.Combine(Application.StartupPath, "periphery.ini")
        Try
            lines = File.ReadAllLines(path, Encoding.UTF8)
        Catch ex As IOException
            Using reader As New StreamReader(GetType(PeripheryNames).Assembly.GetManifestResourceStream("NextConfig.periphery.ini"), Encoding.UTF8)
                lines = reader.ReadToEnd().Split(New String() {vbCrLf, vbLf}, StringSplitOptions.None)
            End Using
            Notice = "periphery.ini недоступен: используется встроенный список."
        End Try
        Dim section As String = ""
        For Each line In lines
            Dim t = line.Trim()
            If t.StartsWith("[") AndAlso t.EndsWith("]") Then
                section = t.Substring(1, t.Length - 2).ToLowerInvariant()
                If Not Sections.ContainsKey(section) Then Sections.Add(section, New SortedDictionary(Of Byte, String))
            ElseIf section <> "" AndAlso t.Contains("=") Then
                Dim parts = t.Split(New Char() {"="c}, 2)
                Dim key As Byte
                If Byte.TryParse(parts(0).Trim(), key) Then Sections(section)(key) = parts(1).Trim()
            End If
        Next
        For Each key In New String() {"types", "wiegand", "baud"}
            If Not Sections.ContainsKey(key) Then Sections.Add(key, New SortedDictionary(Of Byte, String))
        Next
    End Sub
    Public Function Name(section As String, value As Byte) As String
        If Sections(section).ContainsKey(value) Then Return Sections(section)(value)
        Return value.ToString()
    End Function
End Class

Partial Public Class Form2
    Private names485 As PeripheryNames
    Private mode485, baud485, wiegand485 As ComboBox
    Private address485(7) As AddressEditor
    Private number485(7) As Label
    Private type485(7) As ComboBox
    Private state485(7) As Label
    Private warning485 As Label

    Private Class IndexedName
        Public Value As Byte
        Public Caption As String
        Public Sub New(v As Byte, t As String)
            Value = v : Caption = t
        End Sub
        Public Overrides Function ToString() As String
            Return Caption
        End Function
    End Class

    Private Function Combo485(section As String) As ComboBox
        Dim c As New ComboBox
        Theme.Combo(c, 300)
        If section = "types" Then Theme.PeripheryCombo(c)
        For Each item In names485.Sections(section)
            c.Items.Add(New IndexedName(item.Key, item.Value))
        Next
        AddHandler c.SelectedIndexChanged, AddressOf Changed485
        Return c
    End Function
    Private Sub Select485(c As ComboBox, value As Byte)
        For i = 0 To c.Items.Count - 1
            If DirectCast(c.Items(i), IndexedName).Value = value Then
                c.SelectedIndex = i : Return
            End If
        Next
        c.SelectedIndex = c.Items.Add(New IndexedName(value, value.ToString()))
    End Sub
    Private Function Value485(c As ComboBox, previous As Byte) As Byte
        If c.SelectedItem Is Nothing Then Return previous
        Return DirectCast(c.SelectedItem, IndexedName).Value
    End Function
    Private Function Build485() As Control
        names485 = New PeripheryNames
        mode485 = New ComboBox
        Theme.Combo(mode485, 300)
        mode485.Items.Add(New IndexedName(0, "Пассивный"))
        mode485.Items.Add(New IndexedName(1, "Активный"))
        AddHandler mode485.SelectedIndexChanged, AddressOf Changed485
        baud485 = Combo485("baud") : wiegand485 = Combo485("wiegand")
        Dim options As New TableLayoutPanel
        Theme.BusOptions(options)
        options.Controls.Add(Theme.BusField("Режим работы", mode485), 0, 0)
        options.Controls.Add(Theme.BusField("Скорость, бод", baud485), 1, 0)
        options.Controls.Add(Theme.BusField("Тип Wiegand", wiegand485), 2, 0)

        Dim devices As New TableLayoutPanel
        Theme.BusTable(devices)
        Dim titles = New String() {"№", "Адрес", "Тип устройства", "Связь"}
        For col = 0 To 3
            devices.Controls.Add(Theme.BusCell(titles(col), True), col, 0)
        Next
        For i = 0 To 7
            address485(i) = New AddressEditor
            Theme.Address485(address485(i))
            AddHandler address485(i).ValueChanged, AddressOf Changed485
            type485(i) = Combo485("types")
            state485(i) = Theme.BusCell("Не отвечает", False)
            Dim addressBox = Theme.FieldBox(address485(i), 70, 24)
            Theme.BusEditor(addressBox)
            Theme.BusEditor(type485(i))
            number485(i) = Theme.BusCell((i + 1).ToString(), False)
            devices.Controls.Add(number485(i), 0, i + 1)
            devices.Controls.Add(addressBox, 1, i + 1)
            devices.Controls.Add(type485(i), 2, i + 1)
            devices.Controls.Add(state485(i), 3, i + 1)
        Next
        warning485 = Theme.BusNotice("JPEG-камера C328 должна быть на шине одна", True)
        Dim serverNote = Theme.BusNotice("При подключении к серверу эти настройки заменятся серверными", False)
        Return Theme.SettingsSection("485 и Wiegand", "Параметры шины и подключённых устройств", serverNote, options, warning485, devices,
            Hint("Адрес в списке должен совпадать с адресом, заданным в самом устройстве."))

    End Function
    Private Sub Changed485(sender As Object, e As EventArgs)
        If updatingFields OrElse warning485 Is Nothing Then Return
        Update485State()
        UpdateDirty()
    End Sub
    Private Sub Update485State()
        Dim camera As Boolean = False, count As Integer = 0
        For i = 0 To 7
            Dim value = Value485(type485(i), 0)
            If value = 3 Then camera = True
            If value <> 0 Then count += 1
            address485(i).Enabled = Value485(mode485, 0) = 1
            type485(i).Enabled = address485(i).Enabled
            Theme.BusRowAppearance(address485(i), type485(i), number485(i), value <> 0)
        Next
        warning485.Visible = camera AndAlso count > 1
        Status485()
    End Sub
    Private Function Peripherals(c As clOpros.s_config) As clOpros.s_periphery()
        Return {c.periph1, c.periph2, c.periph3, c.periph4, c.periph5, c.periph6, c.periph7, c.periph8}
    End Function
    Private Sub Load485()
        Select485(mode485, config.rs485Mode)
        Select485(baud485, config.rs485Baud)
        Select485(wiegand485, config.wiegandType)
        Dim p = Peripherals(config)
        For i = 0 To 7
            address485(i).Value = p(i).rs485Adr
            Select485(type485(i), p(i).rs485Type)
        Next
        Update485State()
    End Sub
    Private Sub Apply485()
        config.rs485Mode = Value485(mode485, config.rs485Mode)
        config.rs485Baud = Value485(baud485, config.rs485Baud)
        config.wiegandType = Value485(wiegand485, config.wiegandType)
        Dim p = Peripherals(config)
        For i = 0 To 7
            p(i).rs485Adr = CByte(address485(i).Value)
            p(i).rs485Type = Value485(type485(i), p(i).rs485Type)
        Next
        config.periph1 = p(0) : config.periph2 = p(1) : config.periph3 = p(2) : config.periph4 = p(3)
        config.periph5 = p(4) : config.periph6 = p(5) : config.periph7 = p(6) : config.periph8 = p(7)
    End Sub
    Private Sub Changes485(list As List(Of String))
        AddChange(list, "Режим 485", onBoard.rs485Mode.ToString(), config.rs485Mode.ToString())
        AddChange(list, "Скорость 485", names485.Name("baud", onBoard.rs485Baud), names485.Name("baud", config.rs485Baud))
        AddChange(list, "Тип Wiegand", names485.Name("wiegand", onBoard.wiegandType), names485.Name("wiegand", config.wiegandType))
        Dim old = Peripherals(onBoard), current = Peripherals(config)
        For i = 0 To 7
            AddChange(list, "Устройство " & (i + 1) & " адрес", old(i).rs485Adr.ToString(), current(i).rs485Adr.ToString())
            AddChange(list, "Устройство " & (i + 1) & " тип", names485.Name("types", old(i).rs485Type), names485.Name("types", current(i).rs485Type))
        Next
    End Sub
    Private Function FieldsDiffer485() As Boolean
        If Value485(mode485, config.rs485Mode) <> config.rs485Mode OrElse
           Value485(baud485, config.rs485Baud) <> config.rs485Baud OrElse
           Value485(wiegand485, config.wiegandType) <> config.wiegandType Then Return True
        Dim p = Peripherals(config)
        For i = 0 To 7
            If address485(i).Value <> p(i).rs485Adr OrElse Value485(type485(i), p(i).rs485Type) <> p(i).rs485Type Then Return True
        Next
        Return False
    End Function
    Private Sub Status485()
        For i = 0 To 7
            Dim responds = status.peripheryState IsNot Nothing AndAlso status.peripheryState.Length > i AndAlso status.peripheryState(i) <> 0
            Theme.BusStatus(state485(i), responds, Value485(type485(i), 0) <> 0)
        Next
    End Sub
End Class
