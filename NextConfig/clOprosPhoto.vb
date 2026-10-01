Imports System.IO
Imports System.Threading

Partial Public Class clOpros
    Private demoPhotos As PhotoDemoWire

    Public Sub Photo(capture As Boolean, address As Byte, imei As String, folder As String,
                     progress As Action(Of PhotoProgress), cancel As CancellationToken)
        SyncLock Me
            _isPause = True
            Dim events As Boolean
            Try
                If Comport IsNot Nothing Then events = Comport.EnableEventReceived
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
                    ' Снимок сразу забирается в папку и показывается в рамке: пока плата снимает,
                    ' команда 21 отвечает «занята» - повторяем до 60 с.
                    If folder IsNot Nothing Then
                        Dim until = DateTime.Now.AddSeconds(60)
                        Do
                            cancel.WaitHandle.WaitOne(2000)
                            cancel.ThrowIfCancellationRequested()
                            Try
                                session.Download(imei, folder, cancel)
                                Exit Do
                            Catch ex As PhotoBusyException
                                If DateTime.Now > until Then Throw New IOException("Плата не закончила съёмку за 60 с. Проверьте камеру и повторите снимок.")
                                If progress IsNot Nothing Then progress(New PhotoProgress With {.Text = "Камера снимает…"})
                            End Try
                        Loop
                    End If
                Else
                    session.Download(imei, folder, cancel)
                End If
            Finally
                _isPause = False
                If Comport IsNot Nothing Then Comport.EnableEventReceived = events
            End Try
        End SyncLock
    End Sub
End Class
