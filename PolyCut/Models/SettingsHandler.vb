Imports System.Collections.ObjectModel
Imports System.IO
Imports System.Reflection
Imports System.Text.Json

Imports CommunityToolkit.Mvvm.ComponentModel

Imports PolyCut.Core

Imports SharpVectors.Renderers

Public Class SettingsHandler : Inherits ObservableObject

    Public Shared Property Version As String = "0.1"

    Public Shared Property DataFolder As IO.DirectoryInfo = New IO.DirectoryInfo(IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IridiumIO", "PolyCut"))

    Public Shared Property SettingsJSONFile As IO.FileInfo = New IO.FileInfo(IO.Path.Combine(DataFolder.FullName, "settings.json"))

    Public Shared Property ConfigurationSettings As ConfigurationsSettings = New ConfigurationsSettings
    Public Shared Property PrinterSettings As PrinterSettings = New PrinterSettings
    Public Shared Property CuttingMatSettings As CuttingMatSettings = New CuttingMatSettings


    Shared Async Function InitialiseSettings() As Task

        If Not DataFolder.Exists Then DataFolder.Create()
        Await PrinterSettings.InitialiseSettings(Of Printer)("PolyCut", $"{NameOf(Printer)}s")
        Await CuttingMatSettings.InitialiseSettings(Of CuttingMat)("PolyCut", $"{NameOf(CuttingMat)}s")
        Await ConfigurationSettings.InitialiseSettings(Of ProcessorConfiguration)("PolyCut", $"{NameOf(ProcessorConfiguration)}s")
        If Not SettingsJSONFile.Exists Then Await SettingsJSONFile.Create().DisposeAsync()

    End Function

    Private Shared Function GetCollection(Of T)(handler As ISettingsService) As ObservableCollection(Of T)
        Dim collection As New ObservableCollection(Of T)

        Dim files = handler.SettingsFiles

        For Each file In files
            collection.Add(handler.GetValue(Of T)(file.FullName))
        Next

        Return collection

    End Function

    Shared Function GetPrinters() As ObservableCollection(Of Printer)
        Return GetCollection(Of Printer)(PrinterSettings)
    End Function
    Shared Async Sub WritePrinter(printer As Printer)
        Await PrinterSettings.SetValue(printer.Name, printer)
    End Sub

    Shared Function GetCuttingMats() As ObservableCollection(Of CuttingMat)
        Return GetCollection(Of CuttingMat)(CuttingMatSettings)
    End Function

    Shared Async Sub WriteCuttingMat(cuttingmat As CuttingMat)
        Await CuttingMatSettings.SetValue(cuttingmat.Name, cuttingmat)
    End Sub


    Shared Function GetConfigurations() As ObservableCollection(Of ProcessorConfiguration)
        Return GetCollection(Of ProcessorConfiguration)(ConfigurationSettings)
    End Function

    Shared Async Sub WriteConfiguration(Configuration As ProcessorConfiguration)
        Await ConfigurationSettings.SetValue(Configuration.Name, Configuration)
    End Sub



End Class
