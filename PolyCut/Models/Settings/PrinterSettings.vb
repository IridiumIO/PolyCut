Imports System.Text.Json

Public Class PrinterSettings : Inherits SettingsBase : Implements ISettingsService

    'Public Property SettingsFiles As List(Of IO.FileInfo) Implements ISettingsService.SettingsFiles
    'Public Property SettingsFolder As IO.DirectoryInfo Implements ISettingsService.SettingsFolder


    'Public Function GetValue(Of Printer)(settingName As String) As Printer Implements ISettingsService.GetValue

    '    Dim path As String = IO.Path.Combine(SettingsFolder.FullName, settingName & ".json")
    '    Dim p = JsonSerializer.Deserialize(Of Printer)(IO.File.ReadAllText(path), New JsonSerializerOptions With {.IncludeFields = True})
    '    Return p
    'End Function

End Class
