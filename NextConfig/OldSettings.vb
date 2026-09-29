Imports System.IO
Imports System.Text

' Раскладка packed ConfStr из structures.h обеих веток, в порядке ParseSettings.
Public Class OldSettingField
    Public Offset As Integer
    Public Width As Integer = 1
    Public Mask As Integer = 255
    Public Shift As Integer
    Public Kind As String = "number"
    Public Secret As Boolean
    Public Hint As String = "Целое число; 0x — шестнадцатеричное."
    Public ReadOnly Property Editable As Boolean
        Get
            Return Offset < 195
        End Get
    End Property
End Class

Public Class OldSettings
    Public Shared ReadOnly Fields As List(Of OldSettingField) = MakeFields()
    Private Shared ReadOnly Ansi As Encoding = Encoding.GetEncoding(1251, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)

    Private Shared Function MakeFields() As List(Of OldSettingField)
        Dim f As New List(Of OldSettingField)
        Dim number As Action(Of Integer, Integer) = Sub(p, w) f.Add(New OldSettingField With {.Offset = p, .Width = w})
        Dim text As Action(Of Integer, Integer) = Sub(p, w) f.Add(New OldSettingField With {.Offset = p, .Width = w, .Kind = "text", .Hint = "Текст Windows-1251, до " & (w - 1) & " байт."})
        Dim bits As Action(Of Integer, Integer) = Sub(p, count)
                                                    For i = 0 To count - 1
                                                        f.Add(New OldSettingField With {.Offset = p, .Mask = 1 << i, .Shift = i, .Hint = "0 — нет, 1 — да."})
                                                    Next
                                                End Sub
        For p = 0 To 4 : number(p, 1) : Next
        number(5, 4) : f.Last().Hint = "Время платы: секунды Unix (UInt32)."
        number(9, 2) : f.Last().Secret = True
        number(11, 2) : f.Last().Secret = True
        bits(13, 8) : number(14, 1) : text(15, 40)
        text(55, 5) : f.Last().Secret = True
        bits(60, 1) : f.Add(New OldSettingField With {.Offset = 60, .Mask = 254, .Shift = 1})
        number(61, 1) : number(62, 2) : number(64, 2)
        text(66, 24) : number(90, 2) : text(92, 24) : number(116, 2) : text(118, 16)
        For p = 134 To 159 : number(p, 1) : Next
        number(160, 1) : f.Last().Kind = "signed"
        For p = 161 To 163 : number(p, 1) : Next
        bits(164, 5) : f.Add(New OldSettingField With {.Offset = 164, .Mask = 224, .Shift = 5})
        For p = 165 To 171 : number(p, 1) : Next
        number(172, 2) : number(174, 2) : text(176, 16)
        For p = 192 To 194 : number(p, 1) : Next
        number(195, 315) : number(510, 2)
        For Each field In f
            Dim key = ""
            Select Case field.Offset
                Case 14 : key = "operator"
                Case 134 : key = "output"
                Case 159 : key = "photo"
                Case 169 : key = "input1"
                Case 170 : key = "input2"
                Case 194 : key = "reader"
            End Select
            If key <> "" Then field.Hint = "Введите код. " & String.Join("; ", Enumerable.Range(0, 8).Select(Function(i) OldFirmware.SettingCode(key, CByte(i))))
        Next
        Return f
    End Function

    Public Shared Function Table(block As Byte()) As DataTable
        If block Is Nothing OrElse block.Length <> 512 Then Throw New InvalidDataException("Требуется полный блок настроек 512 байт.")
        Return OldFirmware.ParseSettings(New Byte() {&H55, &H20, 0, 0, 0, 2}.Concat(block).ToArray())
    End Function

    Public Shared Function RawValue(block As Byte(), index As Integer) As String
        Dim f = Fields(index)
        If f.Kind = "text" Then Return Ansi.GetString(block, f.Offset, f.Width).Split(ChrW(0))(0)
        If Not f.Editable Then Return "Вычисляется или сохраняется автоматически"
        If f.Kind = "signed" Then Return If(block(f.Offset) < 128, CInt(block(f.Offset)), CInt(block(f.Offset)) - 256).ToString()
        If f.Width = 4 Then Return BitConverter.ToUInt32(block, f.Offset).ToString()
        If f.Width = 2 Then Return BitConverter.ToUInt16(block, f.Offset).ToString()
        Return ((CInt(block(f.Offset)) And f.Mask) >> f.Shift).ToString()
    End Function

    Public Shared Function Change(block As Byte(), index As Integer, value As String) As Byte()
        Dim f = Fields(index)
        If Not f.Editable Then Throw New InvalidOperationException("Резерв сохраняется, CRC рассчитывается автоматически.")
        Dim result = CType(block.Clone(), Byte())
        If RawValue(block, index) = value Then Return result
        If f.Kind = "text" Then
            Dim bytes = Ansi.GetBytes(value)
            If bytes.Length >= f.Width OrElse value.Contains(ChrW(0)) Then Throw New InvalidDataException(f.Hint)
            If f.Offset = 55 AndAlso value.Any(Function(c) c < "0"c OrElse c > "9"c) Then Throw New InvalidDataException("PIN SIM: до четырёх цифр.")
            Array.Clear(result, f.Offset, f.Width) : Array.Copy(bytes, 0, result, f.Offset, bytes.Length)
        Else
            Dim n As Long
            If value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) Then
                n = Convert.ToInt64(value.Substring(2), 16)
            ElseIf Not Long.TryParse(value, n) Then
                Throw New InvalidDataException("Введите целое число.")
            End If
            Dim maximum As Long = If(f.Width = 4, CLng(UInteger.MaxValue), If(f.Width = 2, 65535, f.Mask >> f.Shift))
            Dim minimum As Long = 0
            If f.Kind = "signed" Then minimum = -128 : maximum = 127
            If n < minimum OrElse n > maximum Then Throw New InvalidDataException("Допустимо " & minimum & "–" & maximum & ".")
            If f.Width = 1 Then
                result(f.Offset) = CByte((CInt(result(f.Offset)) And (255 Xor f.Mask)) Or ((CInt(n And 255) << f.Shift) And f.Mask))
            Else
                Array.Copy(BitConverter.GetBytes(CUInt(n)), 0, result, f.Offset, f.Width)
            End If
        End If
        UpdateCrc(result)
        Return result
    End Function

    Public Shared Sub UpdateCrc(block As Byte())
        Dim crc = clBoot.Crc16(block.Take(510).ToArray())
        block(510) = CByte(crc And 255) : block(511) = CByte(crc >> 8)
    End Sub

    Public Shared Function Changes(before As Byte(), after As Byte()) As String
        Dim a = Table(before), b = Table(after)
        Dim lines As New List(Of String)
        For i = 0 To Fields.Count - 1
            If Not Fields(i).Editable OrElse RawValue(before, i) = RawValue(after, i) Then Continue For
            Dim oldValue = If(Fields(i).Secret, "••••", CStr(a.Rows(i)(2)))
            Dim newValue = If(Fields(i).Secret, "•••• (изменён)", CStr(b.Rows(i)(2)))
            lines.Add(CStr(a.Rows(i)(1)) & ": " & oldValue & " → " & newValue)
        Next
        Return String.Join(vbCrLf, lines)
    End Function
End Class
