Imports CommunityToolkit.Mvvm.ComponentModel
Imports CommunityToolkit.Mvvm.Input

Imports Microsoft.Win32

Imports PolyCut.Core

Imports WPF.Ui.Controls

Imports WPF.Ui
Imports System.Net

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
                MainVM.GenerateSnackbar("File Saved", $"Saved to: {fsd.FileName}", ControlAppearance.Success, SymbolRegular.CheckmarkCircle32, 4)
            Else
                MainVM.GenerateSnackbar("Error Saving File", $"An unknown error occurred", ControlAppearance.Danger, SymbolRegular.DismissCircle32, 4)
            End If
        End If

    End Sub

    Private Async Sub UploadFile()
        Dim moonraker As New MoonrakerExporter(MainVM.Configuration)
        Dim ret = Await moonraker.Export(MainVM.GeneratedGCode, FilePath)

        Select Case ret
            Case 0
                MainVM.GenerateSnackbar("Sent to Printer", $"Sucessfully uploaded {FilePath} to Moonraker", ControlAppearance.Success, SymbolRegular.CheckmarkCircle32, 4)
            Case 1
                MainVM.GenerateSnackbar("Error uploading to Moonraker", $"An unknown error occurred", ControlAppearance.Danger, SymbolRegular.DismissCircle32, 4)
            Case -1
                MainVM.GenerateSnackbar("Error uploading to Moonraker", $"The host could not be found", ControlAppearance.Caution, SymbolRegular.DismissCircle32, 4)
            Case Else
                Dim codeText = New Http.HttpResponseMessage(ret)
                codeText.ReasonPhrase = If(ret = 418, "I'm a teapot (you don't have any GCode to upload)", codeText.ReasonPhrase)
                MainVM.GenerateSnackbar("Error uploading to Moonraker", $"{ret}: {codeText.ReasonPhrase}", ControlAppearance.Danger, SymbolRegular.DismissCircle32, 4)
        End Select
    End Sub


End Class
