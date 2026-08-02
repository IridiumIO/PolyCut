Imports System.IO

Public Class VTracerService


    Public Function ConvertPNGToSVG(pngFilePath As String) As String


        Dim vm = Application.GetService(Of BitmapToSVGWindowViewModel)
        vm.BaseImagePath = pngFilePath

        Dim window = New BitmapToSVGWindow(vm)
        vm.Initialise()
        Dim result = window.ShowDialog()

        If result Then
            Return vm.ResultSvgPath
        End If

        'Dim tempSVGPath = Path.Combine(Path.GetTempPath(), $"polycut-output.svg")

        'Dim arguments = $"-i ""{pngFilePath}"" -o ""{tempSVGPath}"""

        'Dim result = Await RunEmbeddedExecutable.Run("vtracer.exe", arguments)

        'Dim svg = Await File.ReadAllTextAsync(tempSVGPath)

        'Return svg

        Return Nothing

    End Function



End Class
