Imports LazyTranslate

Public Class LocalisationService
    Private Shared ReadOnly LocalisationBaseUri As New Uri("https://raw.githubusercontent.com/IridiumIO/PolyCut/refs/heads/master/PolyCut/Resources/Localisation/")

    Public Shared Async Function InitializeAsync() As Task
        Await LazyTranslate.InitialiseAsync(New LocalisationOptions With {
            .SourceCulture = "en-AU",
            .InitialCulture = SettingsHandler.GetUIConfiguration().Language,
            .CatalogueDirectory = IO.Path.Combine(SettingsHandler.DataFolder.FullName, "Localisation"),
            .ResourceAssembly = GetType(Application).Assembly,
            .ApplicationName = "PolyCut",
            .ProgramVersion = SettingsHandler.SemanticVersion.ToNormalizedString(),
            .OutputMissingTranslationsToDebug = False
        })
    End Function

    Public Shared Async Function LoadLanguage(languageCode As String) As Task(Of Boolean)
        Dim success = L.TryLoadLanguage(languageCode)

        If success Then
            Dim configuration = Application.GetService(Of MainViewModel)().UIConfiguration
            configuration.Language = languageCode
            Await SettingsHandler.WriteUIConfiguration(configuration)
        Else
            Application.GetService(Of SnackbarService).GenerateError("Language Load Error".LT(), "Failed to load language: {0}".LTF(languageCode))
        End If

        Return success
    End Function

    Public Shared Function CheckForLanguageUpdate() As Task(Of Boolean)
        Dim languageCode = Application.GetService(Of MainViewModel)().UIConfiguration.Language
        Return LazyTranslate.CheckForLanguageUpdateAsync(languageCode, LocalisationBaseUri)
    End Function
End Class
