Imports System.Net

Imports CommunityToolkit.Mvvm.ComponentModel
Imports CommunityToolkit.Mvvm.Input

Imports Microsoft.Win32

Imports PolyCut.Core

Partial Public Class ExportViewModel : Inherits ObservableObject

    Public Property MainVM As MainViewModel
    Public Property FilePath As String

    Public Sub New(mainvm As MainViewModel)

        Me.MainVM = mainvm
        FilePath = mainvm.PolyCutDocumentName

    End Sub

    <RelayCommand>
    Private Async Sub SaveFile()

        Dim diskexporter As New DiskExporter(MainVM.Configuration)
        Dim fsd As New SaveFileDialog With {
            .Filter = "GCode (*.gcode;*.ngc)|*.gcode;*.ngc",
            .FileName = FilePath
        }

        If fsd.ShowDialog() Then
            Dim ret = Await diskexporter.Export(MainVM.GeneratedGCode, fsd.FileName)
            If ret = 0 Then
                Application.GetService(Of SnackbarService).GenerateSuccess("File Saved", $"Saved to: {fsd.FileName}")
            Else
                Application.GetService(Of SnackbarService).GenerateError("Error Saving File", $"An unknown error occurred")
            End If
        End If

    End Sub


    <RelayCommand>
    Private Async Sub NetworkUpload()
        Dim moonraker As New MoonrakerExporter(MainVM.Configuration)
        Dim ret = Await moonraker.Export(MainVM.GeneratedGCode, FilePath)
        ParseRet(ret)
    End Sub


    <RelayCommand>
    Private Async Sub NetworkUploadPreview()
        Dim gcodes = BuildBoundingBoxGCode(MainVM.GeneratedGCode)
        Dim moonraker As New MoonrakerExporter(MainVM.Configuration)
        Dim ret = Await moonraker.Export(gcodes, "PolyCut_Preview.gcode")
        ParseRet(ret)
    End Sub

    Private Sub ParseRet(status As Integer)
        Dim sb = Application.GetService(Of SnackbarService)
        Select Case status
            Case 0
                sb.GenerateSuccess("Sent to Printer", $"Sucessfully uploaded {FilePath} to Moonraker")
            Case 1
                sb.GenerateError("Error uploading to Moonraker", $"An unknown error occurred")
            Case -1
                sb.GenerateError("Error uploading to Moonraker", $"The host could not be found")
            Case Else
                Dim codeText = New Http.HttpResponseMessage(status)
                codeText.ReasonPhrase = If(status = 418, "I'm a teapot (you don't have any GCode to upload)", codeText.ReasonPhrase)
                sb.GenerateError("Error uploading to Moonraker", $"{status}: {codeText.ReasonPhrase}")
        End Select
    End Sub


    Private Function BuildBoundingBoxGCode(generatedGCode As IEnumerable(Of GCode)) As String

        Dim FSpeed = 5 * 60 ' mm/min

        Dim points = generatedGCode.Where(Function(gc) gc.X.HasValue AndAlso gc.Y.HasValue).Select(Function(gc) (X:=gc.X.Value, Y:=gc.Y.Value)).ToList()
        If points.Count = 0 Then Return String.Empty

        Dim minX = points.Min(Function(p) p.X)
        Dim minY = points.Min(Function(p) p.Y)
        Dim maxX = points.Max(Function(p) p.X)
        Dim maxY = points.Max(Function(p) p.Y)

        If Double.IsInfinity(minX) OrElse Double.IsInfinity(minY) Then Return String.Empty

        Dim sb As New Text.StringBuilder()
        sb.AppendLine(MainVM.Printer.PreviewStartGCode.Trim)

        Dim lines As New List(Of GCode) From {
            Core.GCode.GZ(MainVM.Configuration.TravelZ),
            Core.GCode.G0(minX, minY, F:=FSpeed),
            Core.GCode.G1(maxX, minY, F:=FSpeed),
            Core.GCode.G1(maxX, maxY, F:=FSpeed),
            Core.GCode.G1(minX, maxY, F:=FSpeed),
            Core.GCode.G1(minX, minY, F:=FSpeed)
        }

        sb.AppendLine(String.Join(Environment.NewLine, lines.Select(Function(g) g.ToString())))
        sb.AppendLine(MainVM.Printer.PreviewEndGCode.Trim)

        Return sb.ToString()

    End Function



End Class
