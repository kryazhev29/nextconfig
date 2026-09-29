Imports System.Threading

Partial Public Class clOpros
    Private demoPhotos As PhotoDemoWire

    Public Sub Photo(capture As Boolean, address As Byte, imei As String, folder As String,
                     progress As Action(Of PhotoProgress), cancel As CancellationToken)
        SyncLock Me
            _isPause = True
            Dim events = Comport.EnableEventReceived
            Try
                cancel.ThrowIfCancellationRequested()
                Dim wire As IPhotoWire
                If Demo.IsOn Then
                    If demoPhotos Is Nothing Then demoPhotos = New PhotoDemoWire(PhotoDemoWire.Scenario)
                    wire = demoPhotos
                Else
                    wire = New PhotoSerialWire(Comport)
                End If
                Dim session As New PhotoSession(wire, progress, Sub(frame) ParseRec(frame))
                If capture Then
                    session.Capture(address, cancel)
                Else
                    session.Download(imei, folder, cancel)
                End If
            Finally
                Comport.EnableEventReceived = events
                _isPause = False
            End Try
        End SyncLock
    End Sub
End Class
