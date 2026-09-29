Imports System.IO

Partial Public Class OldFirmware
    Private Sub CheckBase(before As DataTable)
        RequireAdministrator()
        If Not OldNumbers.Same(OldNumbers.Records(before), OldNumbers.Records(ReadNumbers())) Then Throw New IOException("База изменилась после чтения. Прочитайте её заново перед записью.")
    End Sub
    Private Sub CheckNumberReply(reply As Byte(), cmd As Byte)
        If reply Is Nothing OrElse Not reply.SequenceEqual(New Byte() {&H55, cmd, 0}) Then Throw New IOException("Плата не подтвердила изменение номера. Прочитайте базу перед повтором.")
    End Sub
    Public Sub ChangeNumber(before As DataTable, original As Byte(), replacement As Byte(), backup As String)
        CheckBase(before)
        Dim expected = OldNumbers.Records(before)
        If original IsNot Nothing Then
            If expected.Where(Function(r) OldNumbers.Id(r) = OldNumbers.Id(original)).Count() <> 1 Then Throw New InvalidDataException("Номер отсутствует или неоднозначен.")
            expected.RemoveAll(Function(r) OldNumbers.Id(r) = OldNumbers.Id(original))
        End If
        If replacement IsNot Nothing Then
            OldNumbers.Validate(replacement)
            If expected.Any(Function(r) OldNumbers.Id(r) = OldNumbers.Id(replacement)) Then Throw New InvalidDataException("Такой номер / ID уже есть в базе.")
            If expected.Count >= NumberLimit Then Throw New InvalidDataException("База заполнена.")
            expected.Add(replacement)
        End If
        BackupNumbers(before, backup)
        ' В обеих прошивках 2A добавляет в конец, а не заменяет существующую запись.
        If original IsNot Nothing Then
            CheckNumberReply(If(demoMode, Demo.OldFirmwareReply(&H2B, original), clBoot.DeleteOldNumber(port, original)), &H2B)
        End If
        If replacement IsNot Nothing Then
            CheckNumberReply(If(demoMode, Demo.OldFirmwareReply(&H2A, replacement), clBoot.AddOldNumber(port, replacement)), &H2A)
        End If
        If Not OldNumbers.Same(expected, OldNumbers.Records(ReadNumbers())) Then Throw New IOException("Проверка базы не совпала. Изменение могло выполниться частично; сохранён CSV-резерв. Сначала прочитайте базу.")
    End Sub
    Private Shared Sub BackupNumbers(before As DataTable, path As String)
        If File.Exists(path) Then Throw New IOException("Файл резерва уже существует. Выберите новое имя.")
        OldFirmware.SaveCsv(before, path)
    End Sub
    Public Sub ImportNumbers(before As DataTable, replacement As DataTable, backup As String)
        CheckBase(before)
        Dim records = OldNumbers.Records(replacement)
        If records.Count > NumberLimit Then Throw New InvalidDataException("База превышает объём платы.")
        OldNumbers.Changes(before, replacement) ' проверка дублей
        BackupNumbers(before, backup)
        If records.Count = 0 Then
            Dim reply = If(demoMode, Demo.OldFirmwareReply(&H2D, New Byte() {1}), clBoot.ClearOldNumbers(port))
            CheckImportReply(reply, 1)
        Else
            For index = 0 To records.Count - 1 Step 16
                stopRead.Token.ThrowIfCancellationRequested()
                Dim block = records.Skip(index).Take(16).SelectMany(Function(r) r).ToArray()
                Dim payload = New Byte() {0, CByte(index And 255), CByte(index >> 8), CByte(block.Length \ 64), 0}.Concat(block).ToArray()
                Dim reply = If(demoMode, Demo.OldFirmwareReply(&H2D, payload), clBoot.WriteOldNumbersBlock(port, payload))
                CheckImportReply(reply, 0)
            Next
        End If
        If demoMode Then
            Demo.OldFirmwareReply(&H2D, New Byte() {2})
        Else
            clBoot.FinishOldNumbers(port)
        End If
        If Not OldNumbers.Same(records, OldNumbers.Records(ReadNumbers())) Then Throw New IOException("Загрузка не прошла проверку. База могла записаться частично. Сначала прочитайте её; резерв CSV сохранён.")
    End Sub
    Private Shared Sub CheckImportReply(reply As Byte(), operation As Byte)
        If reply Is Nothing OrElse Not reply.SequenceEqual(New Byte() {&H55, &H2D, 0, operation}) Then Throw New IOException("Блок базы не подтверждён или нарушен индекс. Загрузка остановлена, возможна частичная запись. Прочитайте базу перед повтором.")
    End Sub
    Public Sub CompressNumbers(before As DataTable)
        CheckBase(before)
        Dim reply = If(demoMode, Demo.OldFirmwareReply(&H28, Nothing), clBoot.CompressOldNumbers(port))
        If reply Is Nothing OrElse Not reply.SequenceEqual(New Byte() {&H55, &H28, 1}) Then Throw New IOException("Сжатие базы не подтверждено. Прочитайте её состояние.")
        If Not OldNumbers.Same(OldNumbers.Records(before), OldNumbers.Records(ReadNumbers())) Then Throw New IOException("Состав базы после сжатия изменился. Прочитайте базу.")
    End Sub
End Class
