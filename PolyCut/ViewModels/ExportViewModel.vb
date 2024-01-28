Imports System.Net

Imports CommunityToolkit.Mvvm.ComponentModel
Imports CommunityToolkit.Mvvm.Input

Imports Microsoft.Win32

Imports PolyCut.Core

Public Class ExportViewModel : Inherits ObservableObject

    Public Property MainVM As MainViewModel
    Public Property FilePath As String

    Public Property NetworkUploadCommand As ICommand = New RelayCommand(AddressOf UploadFile)
    Public Property SaveFileCommand As ICommand = New RelayCommand(AddressOf SaveFile)



    Public Sub New(mainvm As MainViewModel)

        Me.MainVM = mainvm
        FilePath = mainvm.PolyCutDocumentName

    End Sub


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

    Private Async Sub UploadFile()
        Dim moonraker As New MoonrakerExporter(MainVM.Configuration)
        Dim ret = Await moonraker.Export(MainVM.GeneratedGCode, FilePath)

        Select Case ret
            Case 0
                Application.GetService(Of SnackbarService).GenerateSuccess("Sent to Printer", $"Sucessfully uploaded {FilePath} to Moonraker")
            Case 1
                Application.GetService(Of SnackbarService).GenerateError("Error uploading to Moonraker", $"An unknown error occurred")
            Case -1
                Application.GetService(Of SnackbarService).GenerateError("Error uploading to Moonraker", $"The host could not be found")
            Case Else
                Dim codeText = New Http.HttpResponseMessage(ret)
                codeText.ReasonPhrase = If(ret = 418, "I'm a teapot (you don't have any GCode to upload)", codeText.ReasonPhrase)
                Application.GetService(Of SnackbarService).GenerateError("Error uploading to Moonraker", $"{ret}: {codeText.ReasonPhrase}")
        End Select
    End Sub


End Class
