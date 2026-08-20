Imports System.Globalization
Imports System.IO
Imports System.Net.Http
Imports System.Text

Imports PolyCut.Localisation

Imports Tomlyn
Imports Tomlyn.Model

Public Class LocalisationService
    Private Shared ReadOnly HttpClient As New HttpClient()
    Private Const LocalisationBaseUrl As String = "https://raw.githubusercontent.com/IridiumIO/PolyCut/refs/heads/master/PolyCut/Resources/"
    Private Const ManifestUrl As String = LocalisationBaseUrl & "manifest.toml"

    Private Shared ReadOnly Property LocalisationFolder As String
        Get
            Return IO.Path.Combine(SettingsHandler.DataFolder.FullName, "Localisation")
        End Get
    End Property


    Public Shared Function LoadLanguage(languageCode As String) As Boolean

        Dim success = L.TryLoadLanguage(languageCode, IO.Path.Combine(LocalisationFolder, $"{languageCode}.toml"))

        If Not success Then
            Application.GetService(Of SnackbarService).GenerateError("Language Load Error".LT(), "Failed to load language: {0}".LTF(languageCode))
        End If

        Return success

    End Function

    Public Shared Function GetAllLanguages() As List(Of LanguageItem)

        Dim languages As New List(Of LanguageItem)

        For Each file In IO.Directory.GetFiles(LocalisationFolder, "*.toml")
            Dim languageCode As String = IO.Path.GetFileNameWithoutExtension(file)

            Dim lang As New LanguageItem With {
                .ISOCountryCode = languageCode.Split("-"c)(1),
                .CultureCode = languageCode
            }

            Dim langFound = FamFamFam.Flags.Wpf.CountryData.TryGetName(lang.ISOCountryCode, lang.Name)
            If Not langFound Then lang.Name = lang.ISOCountryCode
            languages.Add(lang)

        Next

        Return languages.OrderBy(Function(f) f.Name).ToList()

    End Function


    Public Shared Async Function SynchroniseEmbeddedLanguages() As Task

        Directory.CreateDirectory(LocalisationFolder)

        Dim assembly = System.Reflection.Assembly.GetExecutingAssembly()
        Const resourcePrefix As String = "PolyCut."

        For Each resourceName In assembly.GetManifestResourceNames()

            If Not resourceName.StartsWith(resourcePrefix, StringComparison.Ordinal) OrElse
           Not resourceName.EndsWith(".toml", StringComparison.OrdinalIgnoreCase) Then Continue For

            Dim fileName = resourceName.Substring(resourcePrefix.Length)
            Dim outputPath = Path.Combine(LocalisationFolder, fileName)

            Using stream = assembly.GetManifestResourceStream(resourceName)
                If stream Is Nothing Then Continue For

                Using reader As New StreamReader(stream, Encoding.UTF8)
                    Dim embeddedText = Await reader.ReadToEndAsync()

                    If ShouldReplaceLocalisationFile(outputPath, embeddedText) Then
                        Await File.WriteAllTextAsync(outputPath, embeddedText, Encoding.UTF8)
                    End If
                End Using
            End Using

        Next

    End Function

    Private Shared Function ShouldReplaceLocalisationFile(filePath As String, embeddedText As String) As Boolean

        If Not File.Exists(filePath) Then Return True

        Dim embeddedVersion = ReadLocalisationVersion(embeddedText)
        Dim localVersion = ReadLocalisationVersion(File.ReadAllText(filePath))

        Return embeddedVersion > localVersion

    End Function


    Private Shared Function ReadLocalisationVersion(sourceText As String) As Integer

        Using reader As New StringReader(sourceText)

            While True

                Dim line = reader.ReadLine()
                If line Is Nothing Then Exit While

                line = line.Trim()

                If line.StartsWith("# @version ", StringComparison.OrdinalIgnoreCase) Then
                    Dim version As Integer
                    If Integer.TryParse(line.Substring(11).Trim(), version) Then Return version
                    Return 0
                End If

                If Not String.IsNullOrWhiteSpace(line) AndAlso Not line.StartsWith("#"c) Then Exit While

            End While

        End Using

        Return 0

    End Function


    Public Shared Async Function CheckForLanguageUpdate() As Task(Of Boolean)

        Try
            Dim languageCode = Application.GetService(Of MainViewModel)().UIConfiguration.Language
            Dim localPath = Path.Combine(LocalisationFolder, $"{languageCode}.toml")

            Dim manifestText = Await HttpClient.GetStringAsync(ManifestUrl)
            Dim manifest = TomlSerializer.Deserialize(Of TomlTable)(manifestText)

            If Not manifest.TryGetValue("locales", Nothing) Then Return False

            Dim locales = TryCast(manifest("locales"), TomlTable)
            If locales Is Nothing OrElse Not locales.ContainsKey(languageCode) Then Return False

            Dim locale = TryCast(locales(languageCode), TomlTable)
            If locale Is Nothing Then Return False

            Dim remoteVersion = Convert.ToInt32(locale("version"))
            Dim remoteFileName = CStr(locale("file"))

            Dim localVersion = 0

            If File.Exists(localPath) Then
                localVersion = ReadLocalisationVersion(Await File.ReadAllTextAsync(localPath))
            End If

            If remoteVersion <= localVersion Then Return False

            Dim remoteUrl = LocalisationBaseUrl & remoteFileName
            Dim remoteText = Await HttpClient.GetStringAsync(remoteUrl)

            ' Validate the downloaded file before touching the existing file.
            Dim downloadedVersion = ReadLocalisationVersion(remoteText)

            If downloadedVersion <> remoteVersion Then
                Throw New InvalidDataException($"Downloaded {remoteFileName} version {downloadedVersion} does not match manifest version {remoteVersion}.")
            End If

            'Write to tepm file just in case first so we don't cook languages
            Dim tempPath = localPath & ".tmp"
            Await File.WriteAllTextAsync(tempPath, remoteText, Encoding.UTF8)
            File.Move(tempPath, localPath, True)

            Return True

        Catch ex As Exception
            Debug.WriteLine($"[LOC] Failed to check for localisation update: {ex.Message}")
            Return False
        End Try

    End Function



End Class

Public Class LanguageItem
    Public Property Name As String
    Public Property ISOCountryCode As String
    Public Property CultureCode As String
    Public ReadOnly Property LanguageName As String
        Get
            Return CultureInfo.GetCultureInfo(CultureCode).NativeName
        End Get
    End Property
End Class