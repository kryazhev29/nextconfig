' Резервная выгрузка базы ID с платы (USB 116) в CSV сервера: формат — образец Алексея 26.10.2026.
Public Class IdCell
    Public Cell As UShort
    Public Id As String
    Public Group As Byte
    Public BlockTime As UInteger
    Public UseTime As UShort
    Public Kind As Byte
    Public PerDay As Byte
End Class

Public Enum eIdsPage
    NoReply
    Busy
    Ready
End Enum

Public Module IdsExport
    Public Const Header As String = "countryCode;mainPhoneNumber;fullName;enableLifeTimeInMinutes;lifeTimeInMinutes;enableAutoBlockDate;autoBlockDate;restrictAccessRequestPerDay;accessRequestMaxCountPerDay;groupName;isBlocked;isDeleted;satelliteIdentificator;satelliteType;satelliteCellNumber;satelliteIsDeleted;satelliteIsBlocked"

    ' Ответ 116: код (1 — флеш занята), следующая ячейка (0 — конец), число записей и записи по 32 Б как в команде 13.
    Public Function ParsePage(reply As Byte(), cells As List(Of IdCell), ByRef nextCell As Integer) As eIdsPage
        If reply Is Nothing OrElse reply.Length < 4 Then Return eIdsPage.NoReply
        If reply(0) <> 0 Then Return eIdsPage.Busy
        Dim count = CInt(reply(3))
        If reply.Length < 4 + 32 * count Then Return eIdsPage.NoReply
        For i = 0 To count - 1
            Dim o = 4 + 32 * i
            Dim length = 0
            While length < 10 AndAlso reply(o + 2 + length) <> 0 AndAlso reply(o + 2 + length) <> 255
                length += 1
            End While
            Dim kind As Byte = 0
            While kind < 7 AndAlso (reply(o + 19) >> kind And 1) = 0
                kind += CByte(1)
            End While
            cells.Add(New IdCell With {.Cell = BitConverter.ToUInt16(reply, o), .Id = Text.Encoding.ASCII.GetString(reply, o + 2, length),
                .Group = reply(o + 12), .BlockTime = BitConverter.ToUInt32(reply, o + 13), .UseTime = BitConverter.ToUInt16(reply, o + 17),
                .Kind = If(reply(o + 19) = 0, CByte(0), kind), .PerDay = reply(o + 20)})
        Next
        nextCell = BitConverter.ToUInt16(reply, 1)
        Return eIdsPage.Ready
    End Function

    ' Имени абонента и названия группы в плате нет: имя пустое, группа — «Группа N».
    ' Тип сателлита у сервера с 0 (0 — телефон), у платы — номер бита признака.
    Public Function Csv(cells As IEnumerable(Of IdCell), countryCode As String) As String
        Dim text As New Text.StringBuilder(Header & vbCrLf)
        For Each c In cells
            Dim limited = c.PerDay <> 255
            Dim blockDate = If(c.BlockTime = 0, "", DateTimeOffset.FromUnixTimeSeconds(c.BlockTime).LocalDateTime.ToString("dd.MM.yyyy HH:mm:ss"))
            text.AppendLine(String.Join(";", countryCode, c.Id, "", c.UseTime > 0, c.UseTime, c.BlockTime > 0, blockDate,
                limited, If(limited, c.PerDay, 0), "Группа " & c.Group, False, False, c.Id, c.Kind, c.Cell, False, False))
        Next
        Return text.ToString()
    End Function
End Module
