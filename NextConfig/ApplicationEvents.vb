Imports Microsoft.VisualBasic.ApplicationServices

Namespace My
    ' Запуск программы: журнал работы, канал --agent и перехват падений.
    Partial Friend Class MyApplication

        Private Sub MyApplication_Startup(sender As Object, e As StartupEventArgs) Handles Me.Startup
            AgentLog.Start()
            AddHandler AppDomain.CurrentDomain.UnhandledException,
                Sub(s, a) AgentLog.Failure("поток", TryCast(a.ExceptionObject, Exception))
            If Environment.GetCommandLineArgs().Any(Function(x) x.Trim().ToLowerInvariant() = "--agent") Then
                AgentChannel.Start()
            End If
        End Sub

        ' Ошибка в потоке окна: пишем в журнал и продолжаем работу, а не закрываем
        ' программу посреди работы с платой.
        Private Sub MyApplication_UnhandledException(sender As Object, e As UnhandledExceptionEventArgs) Handles Me.UnhandledException
            AgentLog.Failure("окно", e.Exception)
            e.ExitApplication = False
            Try
                Using f As New frmInfo("Ошибка программы",
                                       e.Exception.Message & vbCrLf & vbCrLf &
                                       "Подробности записаны в журнал: " & AgentLog.CurrentFile)
                    ' с владельцем окно не уходит за главное и не запирает программу
                    f.ShowDialog(System.Windows.Forms.Form.ActiveForm)
                End Using
            Catch
            End Try
        End Sub

        Private Sub MyApplication_Shutdown(sender As Object, e As EventArgs) Handles Me.Shutdown
            AgentChannel.StopChannel()
            AgentLog.Write("stop")
        End Sub
    End Class
End Namespace
