' ПИН платы NEXT: прошивка с f3ebc9e хранит его числом (4–5 цифр, не больше 99999),
' сервер СофтЭксперт и ранние сборки — четырьмя ASCII-цифрами в том же uint32.
' Прошивка принимает оба вида и сама переводит ASCII в число; при входе (команда 102) ждёт 4–5 цифр текстом.
Public NotInheritable Class PinFormat
    Public Shared Function IsValid(text As String) As Boolean
        Return text IsNot Nothing AndAlso (text.Length = 4 OrElse text.Length = 5) AndAlso text.All(Function(c) c >= "0"c AndAlso c <= "9"c)
    End Function

    ' Цифры ПИН из сохранённого значения; непонятное значение — пустая строка, а не мусор.
    Public Shared Function Digits(stored As UInteger) As String
        If stored <= 99999UI Then Return stored.ToString("0000")
        Dim ascii = Text4(BitConverter.GetBytes(stored))
        Return If(ascii, "")
    End Function

    Private Shared Function Text4(bytes As Byte()) As String
        If bytes.Any(Function(b) b < AscW("0"c) OrElse b > AscW("9"c)) Then Return Nothing
        Return System.Text.Encoding.ASCII.GetString(bytes)
    End Function

    ' Значение для настроек: 4 цифры — прежним видом (его понимают сервер и все сборки), 5 — числом.
    Public Shared Function ToStored(text As String) As UInteger
        If text.Length = 4 Then Return BitConverter.ToUInt32(System.Text.Encoding.ASCII.GetBytes(text), 0)
        Return UInteger.Parse(text)
    End Function

    ' Тот же ли ПИН: «0123» и сохранённое число 123 — одно и то же.
    Public Shared Function Matches(text As String, stored As UInteger) As Boolean
        Dim known = Digits(stored)
        Return IsValid(text) AndAlso known <> "" AndAlso UInteger.Parse(text) = UInteger.Parse(known)
    End Function
End Class
