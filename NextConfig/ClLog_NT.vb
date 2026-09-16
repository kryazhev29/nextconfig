Imports System.IO
Imports System.Reflection

Public Class ClLog
    Implements IDisposable

    Private mFileName, mPath As String
    Private mSW As StreamWriter
    Private mLogEnable As Boolean = False
    Private num As Integer = 0

    Public ReadOnly Property GetCurentFileName() As String
        Get
            Return mPath & mFileName
        End Get
    End Property

    Public Sub New()
        mLogEnable = False
    End Sub

    Public Sub Open(ByVal _path As String, ByVal _filename As String, ByVal _title As String, Optional ByVal dayOld As Integer = 30)
        'создаем новый файл Log 
        mFileName = _filename & ".log"
        mPath = My.Application.Info.DirectoryPath & "\LOG\" & _path & "\"

        Directory.CreateDirectory(mPath)
        DeleteOldFile(mPath, dayOld)

        Using fs As FileStream = File.Create(mPath & mFileName)
        End Using

        mSW = New StreamWriter(mPath & mFileName, True)

        If _title IsNot Nothing AndAlso _title.Length > 0 Then
            mSW.WriteLine(_title)
        End If

        mSW.Flush()
        mSW.Close()
        num = 0
    End Sub

    Private Sub DeleteOldFile(ByVal _path As String, ByVal dayOld As Integer)
        Dim di = New IO.DirectoryInfo(_path)
        For Each dra As IO.FileInfo In di.GetFiles()
            If dra.LastWriteTime < Now.AddDays(-dayOld) Then
                dra.Delete()
            End If
        Next

    End Sub

    Public Sub Close()
        EnableLog = False
    End Sub

    Public Property EnableLog() As Boolean
        Get
            Return mLogEnable
        End Get
        Set(ByVal value As Boolean)
            mLogEnable = value
        End Set
    End Property

    Public Sub Write(ByVal message As String, Optional ByVal withTime As Boolean = True, Optional ByVal AddCrlf As Boolean = True)
        Try
            If mLogEnable AndAlso mSW IsNot Nothing Then

                mSW = File.AppendText(GetCurentFileName)
                If withTime Then
                    mSW.WriteLine(num.ToString & vbTab & Now.ToString("yyyyMMdd_HHmmss") & vbTab & message)
                Else
                    mSW.WriteLine(num.ToString & vbTab & message)
                End If

                mSW.Flush()
                mSW.Close()

                num += 1
            End If

        Catch ex As Exception

        End Try

    End Sub

#Region " IDisposable Support "

    Private disposedValue As Boolean = False

    ' IDisposable
    Protected Overridable Sub Dispose(ByVal disposing As Boolean)
        If Not disposedValue Then
            If disposing Then
                mFileName = Nothing
                mPath = Nothing
            End If
        End If
        disposedValue = True
    End Sub


    ' This code added by Visual Basic to correctly implement the disposable pattern.
    Public Sub Dispose() Implements IDisposable.Dispose
        ' Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.

        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub
#End Region


End Class
