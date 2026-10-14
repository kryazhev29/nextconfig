Imports System.Drawing.Text
Imports System.Runtime.InteropServices

' Шрифты принадлежат процессу: установки в Windows и прав администратора нет.
Public Module AppFonts
    Private ReadOnly collection As New PrivateFontCollection()
    Private ReadOnly memory As New List(Of IntPtr)()
    Private ReadOnly families As New Lazy(Of FontFamily())(AddressOf LoadFamilies)

    <DllImport("gdi32.dll", SetLastError:=True)>
    Private Function AddFontMemResourceEx(data As IntPtr, length As UInteger, reserved As IntPtr, ByRef count As UInteger) As IntPtr
    End Function

    Private Function LoadFamilies() As FontFamily()
        For Each name In New String() {"Montserrat-Regular.ttf", "Montserrat-SemiBold.ttf"}
            Using stream = GetType(Form1).Assembly.GetManifestResourceStream("NextConfig." & name),
                  buffer As New IO.MemoryStream()
                stream.CopyTo(buffer)
                Dim bytes = buffer.ToArray(), address = Marshal.AllocHGlobal(bytes.Length)
                Marshal.Copy(bytes, 0, address, bytes.Length)
                Dim count As UInteger
                If AddFontMemResourceEx(address, CUInt(bytes.Length), IntPtr.Zero, count) = IntPtr.Zero Then
                    Dim errorCode = Marshal.GetLastWin32Error()
                    Marshal.FreeHGlobal(address)
                    Throw New ComponentModel.Win32Exception(errorCode)
                End If
                ' GDI+ использует память до конца жизни коллекции; обе живут весь процесс.
                memory.Add(address)
                collection.AddMemoryFont(address, bytes.Length)
            End Using
        Next
        Return New FontFamily() {collection.Families.Single(Function(f) f.Name = "Montserrat"),
            collection.Families.Single(Function(f) f.Name = "Montserrat SemiBold")}
    End Function

    Public Function Create(size As Single, style As FontStyle) As Font
        Return New Font(families.Value(If((style And FontStyle.Bold) <> 0, 1, 0)), size, FontStyle.Regular)
    End Function
End Module
