Imports System.IO
Imports System.Text

Imports Tomlyn
Imports Tomlyn.Model

''' <summary>
''' Keeps translation catalogue files in step with the English catalogue: new strings are added (restoring
''' previous translations from the obsolete section where possible) and removed strings are archived as obsolete.
''' </summary>
Friend NotInheritable Class LocalisationSynchroniser

    Private Sub New()
    End Sub

    ' ====================
    ' Public API
    ' ====================

    Public Shared Sub SynchroniseAll(localisationDirectory As String, Optional englishFileName As String = "en-AU.toml")

        Dim englishPath = Path.Combine(localisationDirectory, englishFileName)
        If Not File.Exists(englishPath) Then
            Debug.WriteLine($"[LOC] Cannot synchronise translations: {englishFileName} does not exist.")
            Return
        End If

        Dim english = ParseCatalogue(englishPath)

        For Each filePath In Directory.EnumerateFiles(localisationDirectory, "*.toml")
            If Path.GetFileName(filePath).Equals(englishFileName, StringComparison.OrdinalIgnoreCase) Then Continue For
            SynchroniseLanguage(filePath, english)
        Next

    End Sub

    ' ====================
    ' Synchronisation
    ' ====================

    Private Shared Sub SynchroniseLanguage(filePath As String, english As Catalogue)

        Dim language = ParseCatalogue(filePath)
        Dim fileInfo = ReadFileInfo(filePath)

        ' Remove anything accidentally placed in obsolete while it is already active.
        For Each key In english.Active.Keys
            If language.Active.ContainsKey(key) Then language.Obsolete.Remove(key)
        Next

        ' Add new source strings, restoring previous translations from obsolete where possible.
        For Each englishItem In english.Active.Values

            If language.Active.ContainsKey(englishItem.Key) Then Continue For

            Dim restored As Entry = Nothing

            If language.Obsolete.TryGetValue(englishItem.Key, restored) Then
                language.Obsolete.Remove(englishItem.Key)
                language.Active(englishItem.Key) = restored
            Else
                language.Active(englishItem.Key) = New Entry(englishItem.Source, englishItem.Source, englishItem.Context, englishItem.Comment, todo:=True)
            End If

        Next

        ' Anything no longer in the English catalogue becomes obsolete.
        For Each key In language.Active.Keys.ToArray()

            If english.Active.ContainsKey(key) Then Continue For

            Dim item = language.Active(key)
            language.Active.Remove(key)
            language.Obsolete(key) = item

        Next

        Dim output = WriteCatalogue(language, fileInfo)
        Dim original = File.ReadAllText(filePath)

        If Not String.Equals(original, output, StringComparison.Ordinal) Then File.WriteAllText(filePath, output, Encoding.UTF8)

        Dim todoCount = language.Active.Values.Where(Function(x) x.Todo).Count()
        Dim translatedCount = language.Active.Count - todoCount
        Dim cultureName = Path.GetFileNameWithoutExtension(filePath)

        Debug.WriteLine($"[LOC] {cultureName}: {translatedCount} translated, {todoCount} TODO, {language.Obsolete.Count} obsolete")

    End Sub

    ' ====================
    ' Catalogue parsing
    ' ====================

    Private Shared Function ParseCatalogue(filePath As String) As Catalogue

        Dim catalogue As New Catalogue()
        Dim context As String = Nothing
        Dim obsolete = False

        For Each rawLine In File.ReadLines(filePath)

            Dim line = rawLine.Trim()
            If String.IsNullOrEmpty(line) OrElse line.StartsWith("#") Then Continue For

            If line = "[obsolete]" Then
                obsolete = True
                context = Nothing
                Continue For
            End If

            If line.StartsWith("[obsolete.context.", StringComparison.Ordinal) Then
                obsolete = True
                context = ParseContextName(line, "[obsolete.context.")
                Continue For
            End If

            If line.StartsWith("[context.", StringComparison.Ordinal) Then
                obsolete = False
                context = ParseContextName(line, "[context.")
                Continue For
            End If

            If line.StartsWith("[") Then Continue For

            Dim entry = ParseEntry(line, context)
            If entry Is Nothing Then Continue For

            If obsolete Then
                catalogue.Obsolete(entry.Key) = entry
            Else
                catalogue.Active(entry.Key) = entry
            End If

        Next

        Return catalogue

    End Function

    Private Shared Function ReadFileInfo(filePath As String) As FileInfo

        Dim info As New FileInfo()

        For Each rawLine In File.ReadLines(filePath)

            Dim line = rawLine.Trim()

            If line.StartsWith("# @version ", StringComparison.OrdinalIgnoreCase) Then
                Integer.TryParse(line.Substring(11).Trim(), info.Version)
                Exit For
            End If

            If Not String.IsNullOrWhiteSpace(line) AndAlso Not line.StartsWith("#") Then Exit For

        Next

        Return info

    End Function

    Private Shared Function ParseContextName(line As String, prefix As String) As String

        Dim value = line.Substring(prefix.Length, line.Length - prefix.Length - 1).Trim()

        If value.StartsWith("""") Then Return DecodeString(value)

        Return value

    End Function

    Private Shared Function ParseEntry(line As String, context As String) As Entry

        Dim equalsIndex = FindOutsideString(line, "="c)
        If equalsIndex < 0 Then Return Nothing

        Dim keyText = line.Substring(0, equalsIndex).Trim()
        Dim remainder = line.Substring(equalsIndex + 1).Trim()

        Dim commentIndex = FindOutsideString(remainder, "#"c)
        Dim valueText = If(commentIndex >= 0, remainder.Substring(0, commentIndex).Trim(), remainder)
        Dim comment = If(commentIndex >= 0, remainder.Substring(commentIndex + 1).Trim(), Nothing)

        Dim todo = Not String.IsNullOrEmpty(comment) AndAlso comment.Contains("LOC:TODO", StringComparison.OrdinalIgnoreCase)

        If Not String.IsNullOrEmpty(comment) Then
            comment = comment.Replace("LOC:TODO", "", StringComparison.OrdinalIgnoreCase).Trim()
            comment = comment.Trim("|"c).Trim()
            If comment.Length = 0 Then comment = Nothing
        End If

        Return New Entry(DecodeString(keyText), DecodeString(valueText), context, comment, todo)

    End Function

    Private Shared Function FindOutsideString(value As String, character As Char) As Integer

        Dim quoted = False
        Dim escaped = False

        For i = 0 To value.Length - 1

            Dim c = value(i)

            If escaped Then
                escaped = False
                Continue For
            End If

            If c = "\"c AndAlso quoted Then
                escaped = True
                Continue For
            End If

            If c = """"c Then
                quoted = Not quoted
                Continue For
            End If

            If Not quoted AndAlso c = character Then Return i

        Next

        Return -1

    End Function

    Private Shared Function DecodeString(value As String) As String

        Dim model = TomlSerializer.Deserialize(Of TomlTable)($"value = {value}")
        Return CStr(model("value"))

    End Function

    ' ====================
    ' Catalogue writing
    ' ====================

    Private Shared Function WriteCatalogue(catalogue As Catalogue, fileInfo As FileInfo) As String

        Dim builder As New StringBuilder()

        builder.AppendLine("# PolyCut Localisation")
        builder.AppendLine($"# @version {fileInfo.Version}")
        builder.AppendLine()
        builder.AppendLine("# ===================")
        builder.AppendLine("# Translations")
        builder.AppendLine()

        WriteActive(builder, catalogue.Active)
        WriteObsolete(builder, catalogue.Obsolete)

        Return builder.ToString()

    End Function

    Private Shared Sub WriteActive(builder As StringBuilder, entries As Dictionary(Of (Context As String, Source As String), Entry))

        Dim root = entries.Values.Where(Function(x) x.Context Is Nothing).OrderBy(Function(x) x.Source, StringComparer.Ordinal)

        For Each item In root
            WriteEntry(builder, item)
        Next

        Dim contexts = entries.Values.Where(Function(x) x.Context IsNot Nothing).GroupBy(Function(x) x.Context).OrderBy(Function(x) x.Key, StringComparer.Ordinal)

        For Each group In contexts

            If builder.Length > 0 Then builder.AppendLine()

            builder.AppendLine($"[context.{EncodeString(group.Key)}]")

            For Each item In group.OrderBy(Function(x) x.Source, StringComparer.Ordinal)
                WriteEntry(builder, item)
            Next

        Next

    End Sub

    Private Shared Sub WriteObsolete(builder As StringBuilder, entries As Dictionary(Of (Context As String, Source As String), Entry))

        If entries.Count = 0 Then Return

        Dim root = entries.Values.Where(Function(x) x.Context Is Nothing).OrderBy(Function(x) x.Source, StringComparer.Ordinal).ToList()

        If root.Count > 0 Then

            If builder.Length > 0 Then builder.AppendLine()

            builder.AppendLine("[obsolete]")

            For Each item In root
                WriteEntry(builder, item)
            Next

        End If

        Dim contexts = entries.Values.Where(Function(x) x.Context IsNot Nothing).GroupBy(Function(x) x.Context).OrderBy(Function(x) x.Key, StringComparer.Ordinal)

        For Each group In contexts

            If builder.Length > 0 Then builder.AppendLine()

            builder.AppendLine($"[obsolete.context.{EncodeString(group.Key)}]")

            For Each item In group.OrderBy(Function(x) x.Source, StringComparer.Ordinal)
                WriteEntry(builder, item)
            Next

        Next

    End Sub

    Private Shared Sub WriteEntry(builder As StringBuilder, item As Entry)

        Dim line = $"{EncodeString(item.Source)} = {EncodeString(item.Translation)}"

        If Not String.IsNullOrWhiteSpace(item.Comment) AndAlso item.Todo Then
            line &= $" # {item.Comment} | LOC:TODO"
        ElseIf Not String.IsNullOrWhiteSpace(item.Comment) Then
            line &= $" # {item.Comment}"
        ElseIf item.Todo Then
            line &= " # LOC:TODO"
        End If

        builder.AppendLine(line)

    End Sub

    Private Shared Function EncodeString(value As String) As String

        Dim table As New TomlTable From {
            {"value", value}
        }

        Dim source = TomlSerializer.Serialize(table)

        Dim equalsIndex = source.IndexOf("="c)
        Return source.Substring(equalsIndex + 1).Trim()

    End Function

    ' ====================
    ' Data classes
    ' ====================

    Private NotInheritable Class Entry

        Public Sub New(source As String, translation As String, Optional context As String = Nothing, Optional comment As String = Nothing, Optional todo As Boolean = False)
            Me.Source = source
            Me.Translation = translation
            Me.Context = context
            Me.Comment = comment
            Me.Todo = todo
        End Sub

        Public Property Source As String
        Public Property Translation As String
        Public Property Context As String
        Public Property Comment As String
        Public Property Todo As Boolean

        Public ReadOnly Property Key As (Context As String, Source As String)
            Get
                Return (Context, Source)
            End Get
        End Property

    End Class

    Private NotInheritable Class Catalogue
        Public ReadOnly Active As New Dictionary(Of (Context As String, Source As String), Entry)
        Public ReadOnly Obsolete As New Dictionary(Of (Context As String, Source As String), Entry)
    End Class

    Private NotInheritable Class FileInfo
        Public Property Version As Integer = 1
    End Class

End Class
