Imports System.Collections.ObjectModel
Imports System.IO
Imports System.Reflection
Imports System.Text
Imports System.Text.Json

Imports CommunityToolkit.Mvvm.ComponentModel

Imports PolyCut.Core

Imports SharpVectors.Renderers

Public Class SettingsHandler : Inherits ObservableObject

    Public Shared Property SemanticVersion As NuGet.Versioning.NuGetVersion = New NuGet.Versioning.NuGetVersion(0, 10, 0)

    Public Shared ReadOnly Property Version As String
        Get
            Return ToFriendlyVersion(SemanticVersion)
        End Get
    End Property

    Public Shared Property DataFolder As IO.DirectoryInfo = New IO.DirectoryInfo(IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IridiumIO", "PolyCut"))

    Public Shared Property SettingsJSONFile As IO.FileInfo

    Public Shared Property ConfigurationSettings As ConfigurationsSettings = New ConfigurationsSettings
    Public Shared Property PrinterSettings As PrinterSettings = New PrinterSettings
    Public Shared Property UISettings As UISettings = New UISettings

    Shared Async Function InitialiseSettings(truePortable As Boolean) As Task

        If truePortable Then DataFolder = New IO.DirectoryInfo(IO.Path.Combine(IO.Path.GetDirectoryName(AppContext.BaseDirectory), ".PolyCutData"))


        If Not DataFolder.Exists Then DataFolder.Create()
        Await PrinterSettings.InitialiseSettings(Of Printer)("PolyCut", $"{NameOf(Printer)}s")
        Await ConfigurationSettings.InitialiseSettings(Of ProcessorConfiguration)("PolyCut", $"{NameOf(ProcessorConfiguration)}s")
        Await UISettings.InitialiseSettings(Of UIConfiguration)("PolyCut", $"UIConfiguration")

        SettingsJSONFile = New IO.FileInfo(IO.Path.Combine(DataFolder.FullName, "settings.json"))

        If Not SettingsJSONFile.Exists Then Await SettingsJSONFile.Create().DisposeAsync()

        Dim languagesFolderPath As String = IO.Path.Combine(SettingsHandler.DataFolder.FullName, "Localisation")
        If Not IO.Path.Exists(languagesFolderPath) Then
            IO.Directory.CreateDirectory(languagesFolderPath)
        End If
        Await LocalisationService.SynchroniseEmbeddedLanguages()

        GenerateEV()

    End Function

    Public Shared Function ToFriendlyVersion(semVer As NuGet.Versioning.NuGetVersion) As String
        Dim sb As New StringBuilder
        sb.Append(semVer.Major).Append("."c).Append(semVer.Minor).Append("."c).Append(semVer.Patch)
        If Not String.IsNullOrEmpty(semVer.Release) Then
            sb.Append(" "c).Append(semVer.ReleaseLabels(0))
            If semVer.ReleaseLabels.Count > 1 Then sb.Append(" "c).Append(semVer.ReleaseLabels(1))
        End If
        Return sb.ToString()
    End Function

    Private Shared Async Sub GenerateEV()


        Dim exepath As String = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName

        Dim EV1 = Environment.GetEnvironmentVariable("IridiumIO", EnvironmentVariableTarget.User)

        If EV1 Is Nothing Then
            Await Task.Run(Sub() Environment.SetEnvironmentVariable("IridiumIO", IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IridiumIO"), EnvironmentVariableTarget.User))
        End If

    End Sub

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

    Shared Sub DeletePrinter(printer As Printer)
        PrinterSettings.DeleteValue(printer.Name)
    End Sub

    Shared Function GetConfigurations() As ObservableCollection(Of ProcessorConfiguration)
        Return GetCollection(Of ProcessorConfiguration)(ConfigurationSettings)
    End Function

    Shared Async Sub WriteConfiguration(Configuration As ProcessorConfiguration)
        Await ConfigurationSettings.SetValue(Configuration.Name, Configuration)
    End Sub

    Shared Function GetUIConfiguration() As UIConfiguration
        Return UISettings.GetValue(Of UIConfiguration)(IO.Path.Combine(UISettings.SettingsFolder.FullName, $"UIConfiguration.json"))
    End Function

    Shared Async Sub WriteUIConfiguration(Configuration As UIConfiguration)
        Await UISettings.SetValue(Configuration.Name, Configuration)
    End Sub

End Class
