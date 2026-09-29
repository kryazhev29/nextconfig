Imports System.IO

' Поиск файлов прошивки и загрузчика для платы, чтобы оператор не выбирал вслепую.
' Ищем в папке, откуда файл брали в прошлый раз, и рядом с программой - в комплекте
' файлы лежат в Binaries\рабочая_FW и Binaries\загрузчик_0x32\<профиль>.
Public Module FirmwareFiles
    Private Const MaxDepth As Integer = 4

    Private ReadOnly Property PrefFile As String
        Get
            Return Path.Combine(AgentLog.Folder, "fwfolder.txt")
        End Get
    End Property

    ' папка последнего файла, выбранного вручную
    Public Property LastFolder As String
        Get
            Try
                Return File.ReadAllText(PrefFile).Trim()
            Catch
                Return ""
            End Try
        End Get
        Set(value As String)
            Try
                Directory.CreateDirectory(AgentLog.Folder)
                File.WriteAllText(PrefFile, value)
            Catch
            End Try
        End Set
    End Property

    Private Function Roots() As List(Of String)
        Dim result As New List(Of String)
        Dim last = LastFolder
        If last <> "" AndAlso Directory.Exists(last) Then result.Add(last)
        Dim dir = New DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory)
        For i = 0 To 2
            If dir Is Nothing Then Exit For
            result.Add(dir.FullName)
            dir = dir.Parent
        Next
        Return result
    End Function

    Private Function Candidates(pattern As String) As List(Of String)
        Dim found As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each root In Roots()
            Collect(root, pattern, 0, found)
        Next
        Return found.ToList()
    End Function

    Private Sub Collect(dir As String, pattern As String, depth As Integer, found As HashSet(Of String))
        Try
            For Each f In Directory.GetFiles(dir, pattern)
                found.Add(Path.GetFullPath(f))
            Next
            If depth >= MaxDepth Then Return
            For Each d In Directory.GetDirectories(dir)
                Dim name = Path.GetFileName(d)
                If name.StartsWith(".") OrElse name.Equals("obj", StringComparison.OrdinalIgnoreCase) Then Continue For
                Collect(d, pattern, depth + 1, found)
            Next
        Catch
        End Try
    End Sub

    ' Самая свежая сборка для платы boardHw (0 - плата неизвестна, подходит любая).
    ' Nothing - подходящего файла рядом нет, тогда оператор выбирает сам.
    Public Function FindFirmware(boardHw As Byte) As String
        Dim best As String = Nothing
        Dim bestBuild As UInteger = 0
        For Each f In Candidates("*.bin")
            Dim app As clBoot.s_appfile
            Dim err As String = ""
            If Not clBoot.CheckAppFile(f, app, err) Then Continue For
            If boardHw <> 0 AndAlso app.hard <> boardHw Then Continue For
            If best Is Nothing OrElse app.build > bestBuild Then
                best = f
                bestBuild = app.build
            End If
        Next
        Return best
    End Function

    ' Загрузчики, подходящие к плате: разные по содержимому файлы.
    ' У v5 и MODEM плата одна (139) - тогда файлов два и выбирает оператор.
    Public Function FindBootloaders(boardHw As Byte) As List(Of String)
        Dim result As New List(Of String)
        Dim seen As New HashSet(Of UInteger)
        For Each f In Candidates("DomovoiBoot*.bin")
            Try
                Dim image = File.ReadAllBytes(f)
                Dim hw As Byte, err As String = ""
                If Not clOpros.CheckBootImage(image, hw, err) Then Continue For
                If boardHw <> 0 AndAlso hw <> boardHw Then Continue For
                If seen.Add(clOpros.Crc32Stm(image)) Then result.Add(f)
            Catch
            End Try
        Next
        Return result
    End Function

    ' Имя профиля по папке файла: ...\загрузчик_0x32\v5\DomovoiBoot.bin -> v5.
    Public Function ProfileName(file As String) As String
        Return Path.GetFileName(Path.GetDirectoryName(file))
    End Function
End Module

' Что проверить, когда плата вернётся после обновления: экран подключения
' сам дождётся её, подключится и покажет итог «было / стало».
Public Class UpdateCheck
    Public Property Bootloader As Boolean
    Public Property BeforeBuild As UInteger
    Public Property ExpectedBuild As UInteger
    Public Property BeforeBoot As Byte
End Class
