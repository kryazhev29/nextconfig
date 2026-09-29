Imports System.IO
Imports System.Text
Imports Microsoft.VisualBasic.FileIO

Public Class OldNumbers
    Private Shared ReadOnly Ansi As Encoding = Encoding.GetEncoding(1251, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
    Public Shared Function Record(row As DataRow) As Byte()
        Dim parts = CStr(row(12)).Split({" "c}, StringSplitOptions.RemoveEmptyEntries)
        If parts.Length <> 64 Then Throw New InvalidDataException("Запись базы должна содержать 64 байта.")
        Dim result = parts.Select(Function(x) Byte.Parse(x, Globalization.NumberStyles.HexNumber)).ToArray()
        Validate(result)
        Return result
    End Function
    Public Shared Function Id(record As Byte()) As String
        Return Ansi.GetString(record, 1, 11).Split(ChrW(0))(0)
    End Function
    Public Shared Sub Validate(record As Byte())
        If record Is Nothing OrElse record.Length <> 64 OrElse record(0) <> &HAA Then Throw New InvalidDataException("Неверная метка или длина записи базы. Повторите чтение.")
        If Id(record).Length <> 10 OrElse record(11) <> 0 Then Throw New InvalidDataException("Номер / ID должен занимать 10 символов и завершаться нулём.")
    End Sub
    Public Shared Function Records(table As DataTable) As List(Of Byte())
        Return table.Rows.Cast(Of DataRow)().Select(Function(r) Record(r)).ToList()
    End Function
    Public Shared Function Same(a As IEnumerable(Of Byte()), b As IEnumerable(Of Byte())) As Boolean
        ' Счётчик проходов меняется автономно и хранится отдельно от phoneparams_t.
        Return a.Select(Function(r) BitConverter.ToString(r.Take(39).Concat(r.Skip(40)).ToArray())).OrderBy(Function(s) s).
            SequenceEqual(b.Select(Function(r) BitConverter.ToString(r.Take(39).Concat(r.Skip(40)).ToArray())).OrderBy(Function(s) s))
    End Function
    Public Shared Function Edit(source As Byte(), idValue As String, name As String, kind As Integer, access As Integer,
                                expires As UInteger, passages As Byte, schedule As Byte) As Byte()
        If kind < 0 OrElse kind > 2 OrElse access < 0 OrElse access > 3 Then Throw New InvalidDataException("Выберите тип и уровень доступа.")
        If idValue.Length <> 10 OrElse (kind = 0 AndAlso idValue.Any(Function(c) c < "0"c OrElse c > "9"c)) OrElse
            (kind <> 0 AndAlso idValue.Any(Function(c) Not Uri.IsHexDigit(c))) Then Throw New InvalidDataException("Телефон: 10 цифр без +7; ID: 10 шестнадцатеричных символов.")
        Dim bytes = Ansi.GetBytes(name)
        If bytes.Length > 19 OrElse name.Contains(ChrW(0)) Then Throw New InvalidDataException("Имя: до 19 байт Windows-1251.")
        If schedule > 7 Then Throw New InvalidDataException("Расписание: 0 (нет) или 1–7.")
        Dim result = If(source Is Nothing, New Byte(63) {}, CType(source.Clone(), Byte()))
        result(0) = &HAA
        Array.Clear(result, 1, 31)
        Array.Copy(Ansi.GetBytes(idValue), 0, result, 1, 10)
        Array.Copy(bytes, 0, result, 12, bytes.Length)
        result(32) = CByte((result(32) And &HCC) Or (kind << 4) Or access)
        Array.Copy(BitConverter.GetBytes(expires), 0, result, 33, 4)
        result(37) = passages : result(38) = schedule : result(39) = 0
        Return result
    End Function
    Public Shared Function ToTable(records As IList(Of Byte())) As DataTable
        Dim table = OldFirmware.NumbersTable()
        For i = 0 To records.Count - 1
            Dim body = New Byte() {&H55, &H2C, 1, CByte(i And 255), CByte(i >> 8), 1, 0}.Concat(records(i)).ToArray()
            table.ImportRow(OldFirmware.ParseNumbers(body, i, records.Count).Rows(0))
        Next
        Return table
    End Function
    Public Shared Function LoadCsv(path As String, limit As Integer) As DataTable
        Dim result = OldFirmware.NumbersTable()
        Using reader As New TextFieldParser(path, Encoding.UTF8, True)
            reader.SetDelimiters(";") : reader.HasFieldsEnclosedInQuotes = True : reader.TrimWhiteSpace = False
            Dim columns = reader.ReadFields()
            If columns Is Nothing OrElse Not columns.SequenceEqual(result.Columns.Cast(Of DataColumn)().Select(Function(c) c.ColumnName)) Then Throw New InvalidDataException("Требуется CSV, сохранённый NextConfig, со всеми исходными 64 байтами.")
            While Not reader.EndOfData
                Dim cells = reader.ReadFields()
                If cells.Length <> columns.Length OrElse result.Rows.Count >= limit Then Throw New InvalidDataException("Неверное число столбцов или превышен объём базы.")
                For i = 0 To cells.Length - 1
                    If cells(i).StartsWith("'") AndAlso cells(i).Length > 1 AndAlso ("=+-@".Contains(cells(i).Substring(1).TrimStart().FirstOrDefault()) OrElse Char.IsControl(cells(i)(1))) Then cells(i) = cells(i).Substring(1)
                Next
                result.Rows.Add(cells)
            End While
        End Using
        Dim records = OldNumbers.Records(result)
        If records.Select(Function(r) Id(r)).Distinct().Count() <> records.Count Then Throw New InvalidDataException("CSV содержит повторяющиеся номера / ID. Устраните дубли перед загрузкой.")
        Dim canonical = ToTable(records)
        For i = 0 To result.Rows.Count - 1
            For c = 1 To 11
                If CStr(result.Rows(i)(c)) <> CStr(canonical.Rows(i)(c)) Then Throw New InvalidDataException("CSV: отображаемые поля не совпадают с исходной записью в строке " & (i + 2) & ". Редактируйте запись в программе.")
            Next
        Next
        Return canonical
    End Function
    Public Shared Function Changes(before As DataTable, after As DataTable) As String
        Dim a = Records(before), b = Records(after)
        If a.Select(Function(r) Id(r)).Distinct().Count() <> a.Count OrElse b.Select(Function(r) Id(r)).Distinct().Count() <> b.Count Then Throw New InvalidDataException("В базе повторяются ID. Сначала устраните неоднозначность записей.")
        Dim added = b.Where(Function(r) Not a.Any(Function(x) Id(x) = Id(r))).Count()
        Dim deleted = a.Where(Function(r) Not b.Any(Function(x) Id(x) = Id(r))).Count()
        Dim changed = b.Where(Function(r) a.Any(Function(x) Id(x) = Id(r) AndAlso Not Same({x}, {r}))).Count()
        Dim lines As New List(Of String) From {"Добавится: " & added & "; изменится: " & changed & "; удалится: " & deleted & "."}
        For Each row As DataRow In before.Rows
            Dim nextRow = after.Rows.Cast(Of DataRow)().FirstOrDefault(Function(r) CStr(r(1)) = CStr(row(1)))
            If nextRow Is Nothing Then
                lines.Add(CStr(row(1)) & " · " & CStr(row(2)) & ": есть → удалить")
            Else
                For c = 1 To 11
                    If c = 8 Then Continue For
                    If CStr(row(c)) <> CStr(nextRow(c)) Then lines.Add(CStr(row(1)) & " · " & before.Columns(c).ColumnName & ": " & CStr(row(c)) & " → " & CStr(nextRow(c)))
                Next
                Dim aRaw = Record(row), bRaw = Record(nextRow)
                If Not aRaw.Skip(40).SequenceEqual(bRaw.Skip(40)) Then
                    lines.Add(CStr(row(1)) & " · Резерв записи: " & BitConverter.ToString(aRaw, 40) & " → " & BitConverter.ToString(bRaw, 40))
                End If
            End If
        Next
        For Each row As DataRow In after.Rows
            If Not before.Rows.Cast(Of DataRow)().Any(Function(r) CStr(r(1)) = CStr(row(1))) Then lines.Add(CStr(row(1)) & ": нет → " & String.Join("; ", row.ItemArray.Skip(2).Take(6).Select(Function(v) CStr(v))))
        Next
        Return String.Join(vbCrLf, lines)
    End Function
End Class
