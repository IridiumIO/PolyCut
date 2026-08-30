Imports LazyTranslate

Public Class LocalisationService
    Private Shared ReadOnly LocalisationBaseUri As New Uri("https://raw.githubusercontent.com/IridiumIO/PolyCut/refs/heads/master/PolyCut/Resources/Localisation/")

    Public Shared Function LoadLanguage(languageCode As String) As Boolean
        Dim success = L.TryLoadLanguage(languageCode)

        If Not success Then
            Application.GetService(Of SnackbarService).GenerateError("Language Load Error".LT(), "Failed to load language: {0}".LTF(languageCode))
        End If

        Return success
    End Function

    Public Shared Function CheckForLanguageUpdate() As Task(Of Boolean)
        Dim languageCode = Application.GetService(Of MainViewModel)().UIConfiguration.Language
        Return LazyTranslate.CheckForLanguageUpdateAsync(languageCode, LocalisationBaseUri)
    End Function
End Class
