Imports System.IO
Imports System.Reflection
Imports System.Threading

Public Class RunEmbeddedExecutable

    Shared Async Function Run(executableName As String, args As String, Optional ctx As CancellationToken = Nothing) As Task(Of (String, String))
        Dim executingAssembly As Assembly = Assembly.GetExecutingAssembly()

        Dim executablePath As String = Path.Combine(SettingsHandler.DataFolder.FullName, executableName)

        If Not File.Exists(executablePath) Then
            Using stream As Stream = executingAssembly.GetManifestResourceStream(executingAssembly.GetName().Name & "." & executableName)
                If stream IsNot Nothing Then
                    Using tempFileStream As FileStream = File.Create(executablePath)
                        Await stream.CopyToAsync(tempFileStream, ctx)
                    End Using
                End If
            End Using
        End If

        ctx.ThrowIfCancellationRequested()

        Dim process As New Process()
        process.StartInfo.FileName = executablePath
        process.StartInfo.Arguments = args
        process.StartInfo.RedirectStandardOutput = True
        process.StartInfo.RedirectStandardError = True
        process.StartInfo.UseShellExecute = False
        process.StartInfo.CreateNoWindow = True
        process.Start()

        Dim outputTask As Task(Of String) = process.StandardOutput.ReadToEndAsync(ctx)
        Dim errorTask As Task(Of String) = process.StandardError.ReadToEndAsync(ctx)

        Try
            Await process.WaitForExitAsync(ctx)
            Return (Await outputTask, Await errorTask)

        Catch ex As OperationCanceledException
            process.Kill(True)
            Throw

        Catch ex As Exception
            ' Any other failure (e.g. process crashed) — ensure it isn't left running.
            process.Kill(True)
            Throw
        End Try

    End Function

End Class
