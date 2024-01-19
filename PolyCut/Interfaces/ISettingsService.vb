Imports System.Diagnostics.Contracts

Public Interface ISettingsService

    Property SettingsFiles As List(Of IO.FileInfo)
    Property SettingsFolder As IO.DirectoryInfo

    Sub SetValue(settingName As String, setting As Object)

    <Pure>
    Sub GetValue(settingName As String)

    Sub InitialiseSettings(appName As String)

End Interface
