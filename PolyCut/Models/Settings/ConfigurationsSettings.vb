Imports System.Text.Json

Imports PolyCut.Core
Public Class ConfigurationsSettings : Inherits SettingsBase : Implements ISettingsService

    'Public Property SettingsFiles As List(Of IO.FileInfo) Implements ISettingsService.SettingsFiles
    'Public Property SettingsFolder As IO.DirectoryInfo Implements ISettingsService.SettingsFolder


    'Public Async Sub SetValue(settingName As String, setting As Object) Implements ISettingsService.SetValue
    '    Dim fileN As String = settingName & ".json"

    '    Dim js As New JsonSerializerOptions With {
    '    .IncludeFields = True,
    '    .IgnoreReadOnlyProperties = True,
    '    .WriteIndented = True}

    '    Dim output = JsonSerializer.Serialize(setting, js)

    '    Await IO.File.WriteAllTextAsync(IO.Path.Combine(SettingsFolder.FullName, fileN), output)

    'End Sub

    'Public Function GetValue(Of ProcessorConfiguration)(settingName As String) As ProcessorConfiguration Implements ISettingsService.GetValue

    '    Dim path As String = IO.Path.Combine(SettingsFolder.FullName, settingName & ".json")
    '    Dim p = JsonSerializer.Deserialize(Of ProcessorConfiguration)(IO.File.ReadAllText(path), New JsonSerializerOptions With {.IncludeFields = True})
    '    Return p
    'End Function

    'Public Sub InitialiseSettings(appName As String) Implements ISettingsService.InitialiseSettings
    '    Dim DataFolder = IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IridiumIO", appName)

    '    SettingsFolder = New IO.DirectoryInfo(IO.Path.Combine(DataFolder, "Configurations"))

    '    If Not SettingsFolder.Exists Then SettingsFolder.Create()


    'End Sub
End Class
