Imports System.IO
Imports System.Text.Json

Imports CommunityToolkit.Mvvm.ComponentModel

Imports PolyCut.Core

Public Class SettingsBase : Implements ISettingsService

    Public Property SettingsFiles As New List(Of FileInfo) Implements ISettingsService.SettingsFiles
    Public Property SettingsFolder As DirectoryInfo Implements ISettingsService.SettingsFolder

    Public Async Function SetValue(settingName As String, setting As Object) As Task Implements ISettingsService.SetValue
        Dim fileN As String = settingName & ".json"

        Dim js As New JsonSerializerOptions With {
        .IncludeFields = True, .IgnoreReadOnlyProperties = True,
        .WriteIndented = True}

        Dim output = JsonSerializer.Serialize(setting, js)

        Await IO.File.WriteAllTextAsync(IO.Path.Combine(SettingsFolder.FullName, fileN), output)

    End Function

    Public Async Function InitialiseSettings(Of T As {ISaveable, New})(appName As String, Subfolder As String) As Task Implements ISettingsService.InitialiseSettings
        Dim DataFolder = IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IridiumIO", appName)

        SettingsFolder = New IO.DirectoryInfo(IO.Path.Combine(DataFolder, Subfolder))

        If Not SettingsFolder.Exists Then SettingsFolder.Create()

        Dim files = SettingsFolder.GetFiles("*.json")
        If files.Count = 0 Then
            Dim p = New T()
            Await SetValue(p.Name, p)
            SettingsFiles.Add(New FileInfo(IO.Path.Combine(SettingsFolder.FullName, p.Name & ".json")))
        Else
            SettingsFiles.AddRange(files)
        End If

        'Read then write all settings back to disk to ensure they are correctly formatted between updates
        For Each file In SettingsFiles
            Dim p = GetValue(Of T)(file.FullName)
            Await SetValue(p.Name, p)
        Next

    End Function

    Public Function GetValue(Of T)(settingName As String) As T Implements ISettingsService.GetValue

        Dim path As String = IO.Path.Combine(SettingsFolder.FullName, settingName)
        Dim p = JsonSerializer.Deserialize(Of T)(IO.File.ReadAllText(path), New JsonSerializerOptions With {.IncludeFields = True})
        Return p
    End Function
End Class
