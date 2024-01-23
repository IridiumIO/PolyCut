
Public Class CommandLineArgsService

    Public Property Args As String()

    Public Sub New()
        Args = Environment.GetCommandLineArgs()
    End Sub


End Class
