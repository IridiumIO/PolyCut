Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Xml

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Global.PolyCut.Localisation

#If DEBUG Then


    'DEBUG-only tool walks XAML and VB sources, collecting localised strings and flagging strings that look like they should be localised.
    'Regenerates the English catalogue and synchronises all translation files.
    Public NotInheritable Class LocalisationScanner

        Private Sub New()
        End Sub

        ' ====================
        ' Configuration
        ' ====================

        Private Shared ReadOnly XamlCandidateProperties As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            "Text", "Content", "Header", "ToolTip", "Title", "StringFormat"
        }

        ' ====================
        ' Public entry point
        ' ====================

        Public Shared Sub Run()

            Try
                Dim rootPath = FindProjectRoot()
                If rootPath Is Nothing Then Throw New DirectoryNotFoundException("Could not locate PolyCut.vbproj.")

                Dim candidates As New List(Of LocalisationCandidate)
                Dim localisedStrings As New List(Of LocalisedString)

                For Each filePath In EnumerateSourceFiles(rootPath, "*.xaml")
                    ScanXaml(filePath, candidates, localisedStrings)
                Next

                For Each filePath In EnumerateSourceFiles(rootPath, "*.vb")
                    If Path.GetFileName(filePath).Equals(NameOf(LocalisationScanner) & ".vb", StringComparison.OrdinalIgnoreCase) Then Continue For
                    ScanVisualBasic(filePath, candidates, localisedStrings)
                Next

                Dim localisationDirectory = Path.Combine(rootPath, "Resources", "Localisation")

                WriteReport(rootPath, candidates)
                WriteEnglishCatalogue(localisationDirectory, localisedStrings)
                LocalisationSynchroniser.SynchroniseAll(localisationDirectory)

                Debug.WriteLine($"[LOC SCAN] {localisedStrings.Count} localised strings, {candidates.Count} possible unlocalised strings.")

            Catch ex As Exception
                Debug.WriteLine($"[LOC SCAN] Failed: {ex.Message}")
            End Try

        End Sub

        ' ====================
        ' Source discovery
        ' ====================

        Private Shared Function FindProjectRoot() As String

            Dim directory As New DirectoryInfo(AppContext.BaseDirectory)

            While directory IsNot Nothing
                If File.Exists(Path.Combine(directory.FullName, "PolyCut.vbproj")) Then Return directory.FullName
                directory = directory.Parent
            End While

            Return Nothing

        End Function

        Private Shared Iterator Function EnumerateSourceFiles(rootPath As String, pattern As String) As IEnumerable(Of String)

            For Each filePath In Directory.EnumerateFiles(rootPath, pattern, SearchOption.AllDirectories)

                Dim relativePath = Path.GetRelativePath(rootPath, filePath)

                If relativePath.StartsWith("bin" & Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) Then Continue For
                If relativePath.StartsWith("obj" & Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) Then Continue For

                Yield filePath

            Next

        End Function

        ' ====================
        ' XAML scanning
        ' ====================

        Private Shared Sub ScanXaml(filePath As String, candidates As List(Of LocalisationCandidate), localisedStrings As List(Of LocalisedString))

            Try
                Dim document = XDocument.Load(filePath, LoadOptions.SetLineInfo)

                For Each element In document.Descendants()

                    Dim locValue = element.Attributes().FirstOrDefault(Function(a) IsLocAttribute(a, "Value"))

                    If locValue IsNot Nothing Then
                        Dim context = FindLocalisationContext(element)
                        localisedStrings.Add(New LocalisedString(locValue.Value, context))
                    End If

                    For Each attribute In element.Attributes()

                        Dim translated = ExtractLocalisedMarkupExtension(attribute.Value)

                        If translated IsNot Nothing Then
                            localisedStrings.Add(translated)
                            Continue For
                        End If

                        If Not XamlCandidateProperties.Contains(attribute.Name.LocalName) Then Continue For

                        Dim value = attribute.Value.Trim()

                        If Not IsLikelyUserFacingString(value) Then Continue For
                        If value.StartsWith("{", StringComparison.Ordinal) Then Continue For

                        Dim info = TryCast(attribute, IXmlLineInfo)
                        Dim line = If(info IsNot Nothing AndAlso info.HasLineInfo(), info.LineNumber, 0)

                        candidates.Add(New LocalisationCandidate(filePath, line, $"{attribute.Name.LocalName}=""{value}"""))

                    Next

                Next

                For Each textNode In document.DescendantNodes().OfType(Of XText)()

                    Dim value = textNode.Value.Trim()
                    If Not IsLikelyUserFacingString(value) Then Continue For

                    Dim info = TryCast(textNode, IXmlLineInfo)
                    Dim line = If(info IsNot Nothing AndAlso info.HasLineInfo(), info.LineNumber, 0)
                    Dim context = If(textNode.Parent IsNot Nothing, FindLocalisationContext(textNode.Parent), Nothing)

                    candidates.Add(New LocalisationCandidate(filePath, line, value, context))

                Next

            Catch ex As Exception
                Debug.WriteLine($"[LOC SCAN] Could not parse XAML '{filePath}': {ex.Message}")
            End Try

        End Sub

        Private Shared Function FindLocalisationContext(element As XElement) As String

            Dim current As XElement = element

            While current IsNot Nothing

                Dim contextAttribute = current.Attributes().FirstOrDefault(Function(a) IsLocAttribute(a, "Context"))
                If contextAttribute IsNot Nothing Then Return contextAttribute.Value

                current = current.Parent

            End While

            Return Nothing

        End Function

        Private Shared Function IsLocAttribute(attribute As XAttribute, propertyName As String) As Boolean
            Return attribute.Name.LocalName = $"L.{propertyName}" AndAlso attribute.Name.NamespaceName.Contains("PolyCut.Localisation", StringComparison.OrdinalIgnoreCase)
        End Function

        Private Shared Function ExtractLocalisedMarkupExtension(value As String) As LocalisedString

            If value.StartsWith("{loc:T ", StringComparison.OrdinalIgnoreCase) Then
                Return ExtractTMarkup(value)
            End If

            If value.StartsWith("{loc:Binding ", StringComparison.OrdinalIgnoreCase) Then
                Return ExtractBindingMarkup(value)
            End If

            Return Nothing

        End Function

        Private Shared Function ExtractTMarkup(value As String) As LocalisedString

            Dim match = Regex.Match(value, "^\{loc:T\s+(?:'(?<source>.*?)'|(?<source>.*?))(?:\s*,\s*Context\s*=\s*(?:'(?<context>.*?)'|(?<context>[^}]*?)))?\s*\}$", RegexOptions.Singleline Or RegexOptions.IgnoreCase)

            If Not match.Success Then Return Nothing

            Dim source = match.Groups("source").Value.Trim()
            Dim context = match.Groups("context").Value.Trim()

            Return New LocalisedString(source, If(String.IsNullOrEmpty(context), Nothing, context))

        End Function

        Private Shared Function ExtractBindingMarkup(value As String) As LocalisedString

            Dim formatMatch = Regex.Match(value, "Format=\{\}(.+?)(?=,\s*\w+=|\}$)")
            If Not formatMatch.Success Then Return Nothing

            Dim source = formatMatch.Groups(1).Value.Trim()

            Dim contextMatch = Regex.Match(value, "Context=([^,}]+)")
            Dim context = If(contextMatch.Success, contextMatch.Groups(1).Value.Trim(), Nothing)

            Return New LocalisedString(source, context)

        End Function

        ' ====================
        ' Visual Basic scanning
        ' ====================

        Private Shared Sub ScanVisualBasic(filePath As String, candidates As List(Of LocalisationCandidate), localisedStrings As List(Of LocalisedString))

            Try
                Dim source = File.ReadAllText(filePath)
                Dim tree = VisualBasicSyntaxTree.ParseText(source)
                Dim root = tree.GetRoot()

                For Each literal In root.DescendantNodes().OfType(Of LiteralExpressionSyntax)()

                    If Not literal.IsKind(VisualBasic.SyntaxKind.StringLiteralExpression) Then Continue For

                    Dim value = literal.Token.ValueText.Trim()
                    If Not IsLikelyUserFacingString(value) Then Continue For

                    Dim localised = GetLocalisedString(literal)

                    If localised IsNot Nothing Then
                        localisedStrings.Add(localised)
                        Continue For
                    End If

                    Dim position = tree.GetLineSpan(literal.Span).StartLinePosition
                    candidates.Add(New LocalisationCandidate(filePath, position.Line + 1, literal.ToString()))

                Next

            Catch ex As Exception
                Debug.WriteLine($"[LOC SCAN] Could not parse VB '{filePath}': {ex.Message}")
            End Try

        End Sub

        Private Shared Function GetLocalisedString(literal As LiteralExpressionSyntax) As LocalisedString

            Dim memberAccess = TryCast(literal.Parent, MemberAccessExpressionSyntax)
            If memberAccess Is Nothing OrElse memberAccess.Expression IsNot literal Then Return Nothing

            Dim invocation = TryCast(memberAccess.Parent, InvocationExpressionSyntax)
            If invocation Is Nothing Then Return Nothing

            Dim memberName = memberAccess.Name.Identifier.ValueText
            If memberName <> "LT" AndAlso memberName <> "LTF" AndAlso memberName <> "LTFC" Then Return Nothing

            Dim source = literal.Token.ValueText
            Dim context As String = Nothing

            If memberName = "LT" Then
                For Each argument In invocation.ArgumentList.Arguments
                    If TypeOf argument Is SimpleArgumentSyntax Then
                        Dim simple = DirectCast(argument, SimpleArgumentSyntax)

                        If simple.NameColonEquals IsNot Nothing AndAlso simple.NameColonEquals.Name.Identifier.ValueText = "context" Then
                            Dim contextLiteral = TryCast(simple.Expression, LiteralExpressionSyntax)
                            If contextLiteral IsNot Nothing AndAlso contextLiteral.IsKind(VisualBasic.SyntaxKind.StringLiteralExpression) Then context = contextLiteral.Token.ValueText
                        End If
                    End If
                Next
            ElseIf memberName = "LTFC" AndAlso invocation.ArgumentList.Arguments.Count > 0 Then
                Dim firstArgument = TryCast(invocation.ArgumentList.Arguments(0), SimpleArgumentSyntax)
                Dim contextLiteral = If(firstArgument IsNot Nothing, TryCast(firstArgument.Expression, LiteralExpressionSyntax), Nothing)
                If contextLiteral IsNot Nothing Then context = contextLiteral.Token.ValueText
            End If

            Return New LocalisedString(source, context)

        End Function

        ' ====================
        ' Candidate filtering
        ' ====================

        Private Shared Function IsLikelyUserFacingString(value As String) As Boolean

            If String.IsNullOrWhiteSpace(value) Then Return False
            If value.Length < 2 Then Return False
            If Not value.Any(AddressOf Char.IsLetter) Then Return False

            If value.StartsWith(".", StringComparison.Ordinal) Then Return False
            If value.StartsWith("#", StringComparison.Ordinal) Then Return False
            If value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) Then Return False
            If value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) Then Return False
            If value.StartsWith("pack://", StringComparison.OrdinalIgnoreCase) Then Return False
            If value.StartsWith("clr-namespace:", StringComparison.OrdinalIgnoreCase) Then Return False
            If value.StartsWith("application/", StringComparison.OrdinalIgnoreCase) Then Return False

            If value.Contains("\") Then Return False

            Return True

        End Function

        ' ====================
        ' Output: candidate report
        ' ====================

        Private Shared Sub WriteReport(rootPath As String, results As List(Of LocalisationCandidate))

            Dim outputPath = Path.Combine(AppContext.BaseDirectory, "LocalisationCandidates.txt")
            Dim builder As New StringBuilder()

            builder.AppendLine($"PolyCut localisation candidates")
            builder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
            builder.AppendLine($"Candidates: {results.Count}")
            builder.AppendLine()

            For Each group In results.OrderBy(Function(x) x.FilePath).ThenBy(Function(x) x.Line).GroupBy(Function(x) x.FilePath)

                builder.AppendLine($"===== {Path.GetRelativePath(rootPath, group.Key)} =====")
                builder.AppendLine()

                For Each candidate In group

                    If String.IsNullOrEmpty(candidate.Context) Then
                        builder.AppendLine($"Line {candidate.Line}: {candidate.Text}")
                    Else
                        builder.AppendLine($"Line {candidate.Line}: [{candidate.Context}] {candidate.Text}")
                    End If

                Next

                builder.AppendLine()

            Next

            File.WriteAllText(outputPath, builder.ToString())
            Debug.WriteLine($"[LOC SCAN] Report written to {outputPath}")

        End Sub

        ' ====================
        ' Output: English catalogue
        ' ====================

        Private Shared Sub WriteEnglishCatalogue(localisationDirectory As String, localisedStrings As IEnumerable(Of LocalisedString))

            Dim strings = New SortedSet(Of String)(StringComparer.Ordinal)
            Dim contexts = New SortedDictionary(Of String, SortedSet(Of String))(StringComparer.Ordinal)

            For Each item In localisedStrings

                If String.IsNullOrEmpty(item.Context) Then
                    strings.Add(item.Source)
                Else
                    Dim values As SortedSet(Of String) = Nothing

                    If Not contexts.TryGetValue(item.Context, values) Then
                        values = New SortedSet(Of String)(StringComparer.Ordinal)
                        contexts(item.Context) = values
                    End If

                    values.Add(item.Source)
                End If

            Next

            Dim builder As New StringBuilder()

            builder.AppendLine("# PolyCut Localisation")
            builder.AppendLine("# @version 1")
            builder.AppendLine()
            builder.AppendLine("# ===================")
            builder.AppendLine("# Translations")
            builder.AppendLine()

            For Each source In strings
                builder.AppendLine($"{TomlString(source)} = {TomlString(source)}")
            Next

            For Each contextItem In contexts
                builder.AppendLine()
                builder.AppendLine($"[context.{TomlString(contextItem.Key)}]")

                For Each source In contextItem.Value
                    builder.AppendLine($"{TomlString(source)} = {TomlString(source)}")
                Next
            Next

            Dim filePath = Path.Combine(localisationDirectory, "en-AU.toml")
            Dim output = builder.ToString()

            If Not File.Exists(filePath) OrElse Not String.Equals(File.ReadAllText(filePath), output, StringComparison.Ordinal) Then
                File.WriteAllText(filePath, output, Encoding.UTF8)
            End If

        End Sub

        Private Shared Function TomlString(value As String) As String
            Return """" & value.Replace("\", "\\").Replace("""", "\""").Replace(vbCr, "\r").Replace(vbLf, "\n").Replace(vbTab, "\t") & """"
        End Function

        ' ====================
        ' Data classes
        ' ====================

        Private NotInheritable Class LocalisationCandidate

            Public Sub New(filePath As String, line As Integer, text As String, Optional context As String = Nothing)
                Me.FilePath = filePath
                Me.Line = line
                Me.Text = text
                Me.Context = context
            End Sub

            Public ReadOnly Property FilePath As String
            Public ReadOnly Property Line As Integer
            Public ReadOnly Property Text As String
            Public ReadOnly Property Context As String

        End Class

        Private NotInheritable Class LocalisedString
            Public Sub New(source As String, Optional context As String = Nothing)
                Me.Source = source
                Me.Context = context
            End Sub

            Public ReadOnly Property Source As String
            Public ReadOnly Property Context As String
        End Class

    End Class

#End If

End Namespace
