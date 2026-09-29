Imports System.Data
Imports System.Text

' Полный пакет NEXT: structures.h:465-499 config_s (512 байт, pack 1).
' reserv[0] — скорость RS-485: см. clOprosCommands.EncodeConfig / GetConfig.
Public NotInheritable Class ConfigSnapshot
    Public Shared Function Parse(config As clOpros.s_config, showSecrets As Boolean) As DataTable
        Dim table As New DataTable()
        table.Columns.Add("Байты") : table.Columns.Add("Параметр") : table.Columns.Add("Значение")
        Dim bytes = config.rawConfig
        If bytes Is Nothing OrElse bytes.Length <> 512 Then Return table
        Add(table, 0, 4, "Время изменения, Unix", BitConverter.ToUInt32(bytes, 0).ToString())
        Add(table, 4, 4, "ПИН-код", If(showSecrets, Str(bytes, 4, 4), "••••"))
        Dim names = {"Сервер 1", "Сервер 2", "NTP-сервер", "Параметры оператора"}
        For i = 0 To 3
            Add(table, 8 + i * 32, 32, names(i), If(i = 3 AndAlso Not showSecrets, "••••", Str(bytes, 8 + i * 32, 32)))
        Next
        Add(table, 136, 1, "Тип сети (код)", bytes(136).ToString())
        Add(table, 137, 8, "Код страны", Str(bytes, 137, 8))
        Add(table, 145, 16, "Телефон SIM", Str(bytes, 145, 16))
        Add(table, 161, 8, "USSD баланса", Str(bytes, 161, 8))
        names = {"Интервал перезапуска, часы", "Расширенный журнал", "VoLTE", "Режим RS-485", "NTP включён", "Скорость RS-485 (код)"}
        For i = 0 To names.Length - 1
            Add(table, 169 + i, 1, names(i), bytes(169 + i).ToString())
        Next
        Add(table, 175, 3, "Резерв", Hex(bytes, 175, 3))
        Add(table, 178, 1, "Тип Wiegand (код)", bytes(178).ToString())
        For i = 0 To 7
            Add(table, 179 + i * 2, 1, "Периферия " & (i + 1) & " · адрес", bytes(179 + i * 2).ToString())
            Add(table, 180 + i * 2, 1, "Периферия " & (i + 1) & " · тип", bytes(180 + i * 2).ToString())
        Next
        names = {"Разблокировка", "Режим", "Команда сервера", "Wiegand", "Камера", "RFID", "Другая периферия RS-485", "RF433", "Импульс", "Пауза", "Повтор"}
        For output = 0 To 1
            For i = 0 To 10
                Dim offset = 195 + output * 11 + i
                Add(table, offset, 1, "Выход " & (output + 1) & " · " & names(i) & " (код)", bytes(offset).ToString())
            Next
        Next
        names = {"Функция входа 1", "Функция входа 2", "Использовать сервер 1", "Использовать сервер 2"}
        For i = 0 To 3
            Add(table, 217 + i, 1, names(i), bytes(217 + i).ToString())
        Next
        For offset = 221 To 509 Step 16
            Dim count = Math.Min(16, 510 - offset)
            Add(table, offset, count, "Резерв", Hex(bytes, offset, count))
        Next
        Add(table, 510, 2, "CRC пакета настроек", Hex(bytes, 510, 2))
        Return table
    End Function
    Private Shared Sub Add(t As DataTable, offset As Integer, length As Integer, name As String, value As String)
        t.Rows.Add(If(length = 1, offset.ToString(), offset & "–" & (offset + length - 1)), name, value)
    End Sub
    Private Shared Function Str(b As Byte(), offset As Integer, count As Integer) As String
        Return Encoding.UTF8.GetString(b, offset, count).Split(ChrW(0))(0)
    End Function
    Private Shared Function Hex(b As Byte(), offset As Integer, count As Integer) As String
        Return BitConverter.ToString(b, offset, count).Replace("-", " ")
    End Function
End Class
