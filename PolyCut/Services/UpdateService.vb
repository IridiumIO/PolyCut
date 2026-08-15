Imports System.Net.Http
Imports System.Text.Json

Imports NuGet.Versioning
Public Class UpdateService

    Private ReadOnly versionURL As String = "https://raw.githubusercontent.com/IridiumIO/PolyCut/refs/heads/master/Data/version.json"

    Private ReadOnly updateURL As String = "https://github.com/IridiumIO/PolyCut/releases/latest"

    Public Shared ReadOnly httpClient As New HttpClient()


    Public Async Function CheckForUpdate(includePrerelease As Boolean) As Task
        Try
            Dim json = Await httpClient.GetStringAsync(versionURL)
            Dim versions = JsonSerializer.Deserialize(Of Dictionary(Of String, String))(json)

            If versions Is Nothing Then Return

            Dim versionString = If(includePrerelease, versions("Latest"), versions("LatestNonPreRelease"))

            Dim newVersion = NuGetVersion.Parse(versionString)

            Dim currentVersion = SettingsHandler.SemanticVersion

            If newVersion > currentVersion Then
                Application.GetService(Of SnackbarService).GenerateUpdate(SettingsHandler.ToFriendlyVersion(newVersion), updateURL)
            End If


        Catch ex As Exception
            Debug.WriteLine(ex.Message)
        End Try
    End Function


End Class
