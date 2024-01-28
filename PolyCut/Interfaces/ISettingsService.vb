Imports System.Diagnostics.Contracts

Imports PolyCut.Core

Public Interface ISettingsService

    Property SettingsFiles As List(Of IO.FileInfo)
    Property SettingsFolder As IO.DirectoryInfo

    Function SetValue(settingName As String, setting As Object) As Task

    <Pure>
    Function GetValue(Of T)(settingName As String) As T

    Function InitialiseSettings(Of T As {ISaveable, New})(appName As String, Subfolder As String) As Task

End Interface
