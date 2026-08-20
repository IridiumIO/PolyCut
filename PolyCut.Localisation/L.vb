Imports System.Diagnostics
Imports System.Globalization
Imports System.IO

Imports Tomlyn
Imports Tomlyn.Model


Partial Public NotInheritable Class L

    Private Sub New()
    End Sub

    ' ====================
    ' State
    ' ====================

    Private Shared _strings As New Dictionary(Of String, String)(StringComparer.Ordinal)

    Private Shared _contexts As New Dictionary(Of String, Dictionary(Of String, String))(StringComparer.Ordinal)

    Private Shared _culture As CultureInfo = CultureInfo.CurrentCulture

    Public Shared ReadOnly Property CurrentCulture As CultureInfo
        Get
            Return _culture
        End Get
    End Property

    ' ====================
    ' Language-change notification
    ' ====================

    Public Shared Event LanguageChanged As EventHandler

    Private Shared Sub NotifyLanguageChanged()
        LocalisationState.Instance.Refresh()
        RaiseEvent LanguageChanged(Nothing, EventArgs.Empty)
    End Sub

    ' ====================
    ' Translation API
    ' ====================

    Public Shared Function T(source As String, Optional context As String = Nothing) As String

        If String.IsNullOrEmpty(source) Then Return source

        Dim translated As String = Nothing

        ' Context-specific translation takes priority.
        If Not String.IsNullOrEmpty(context) Then

            Dim contextStrings As Dictionary(Of String, String) = Nothing

            If _contexts.TryGetValue(context, contextStrings) AndAlso contextStrings.TryGetValue(source, translated) AndAlso Not String.IsNullOrEmpty(translated) Then
                Return translated
            End If

        End If

        ' Fall back to the ordinary translation.
        If _strings.TryGetValue(source, translated) AndAlso
            Not String.IsNullOrEmpty(translated) Then

            Return translated

        End If

        MissingTranslation(source, context)

        ' English is always the final fallback.
        Return source

    End Function

    Public Shared Function TF(format As String, ParamArray args As Object()) As String

        Return String.Format(_culture, T(format), args)

    End Function

    Public Shared Function TFC(context As String, format As String, ParamArray args As Object()) As String

        Return String.Format(_culture, T(format, context), args)

    End Function

    ' ====================
    ' Language loading
    ' ====================

    Public Shared Function TryLoadLanguage(cultureName As String, filePath As String) As Boolean

        Try
            Dim toml = File.ReadAllText(filePath)
            LoadLanguageFromToml(cultureName, toml)
            Return True
        Catch ex As Exception
            Debug.WriteLine($"[LOC] Failed to load language '{cultureName}': {ex.Message}")
            _strings.Clear()
            _contexts.Clear()
            NotifyLanguageChanged()
            Return False
        End Try

    End Function

    Public Shared Sub LoadLanguageFromToml(cultureName As String, sourceText As String)

        Dim model = TomlSerializer.Deserialize(Of TomlTable)(sourceText)
        Dim newStrings As New Dictionary(Of String, String)(StringComparer.Ordinal)
        Dim newContexts As New Dictionary(Of String, Dictionary(Of String, String))(StringComparer.Ordinal)

        For Each item In model

            If item.Key = "context" AndAlso TypeOf item.Value Is TomlTable Then
                Dim contextRoot = DirectCast(item.Value, TomlTable)

                For Each contextItem In contextRoot
                    If Not TypeOf contextItem.Value Is TomlTable Then Continue For

                    Dim values As New Dictionary(Of String, String)(StringComparer.Ordinal)

                    For Each translation In DirectCast(contextItem.Value, TomlTable)
                        If TypeOf translation.Value Is String Then values(translation.Key) = CStr(translation.Value)
                    Next

                    newContexts(contextItem.Key) = values
                Next

            ElseIf TypeOf item.Value Is String Then
                newStrings(item.Key) = CStr(item.Value)
            End If

        Next

        _culture = CultureInfo.GetCultureInfo(cultureName)
        _strings = newStrings
        _contexts = newContexts
        NotifyLanguageChanged()

    End Sub

    Public Shared Sub UseEnglish(Optional cultureName As String = Nothing)

        _strings.Clear()
        _contexts.Clear()

        If Not String.IsNullOrWhiteSpace(cultureName) Then _culture = CultureInfo.GetCultureInfo(cultureName)

        NotifyLanguageChanged()

    End Sub

    ' ====================
    ' Diagnostics
    ' ====================

    <Conditional("DEBUG")>
    Private Shared Sub MissingTranslation(source As String, context As String)

        If String.IsNullOrEmpty(context) Then
            Debug.WriteLine($"[LOC] Missing translation: ""{source}""")
        Else
            Debug.WriteLine($"[LOC] Missing translation: ""{source}"" (Context: {context})")
        End If

    End Sub

    <Conditional("DEBUG")>
    Private Shared Sub LocalisationWarning(message As String)
        Debug.WriteLine($"[LOC] {message}")
    End Sub

End Class
