Imports System.IO

Public Class VTracerService


    Public Function ConvertPNGToSVG(pngFilePath As String) As String


        Dim vm = Application.GetService(Of BitmapToSVGWindowViewModel)
        vm.Cleanup()
        vm.BaseImagePath = pngFilePath

        Dim window = New BitmapToSVGWindow(vm)
        vm.Initialise()
        Dim result = window.ShowDialog()

        If result Then
            Return vm.ResultSvgPath
        End If

        Return Nothing

    End Function



End Class
