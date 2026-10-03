Imports System.Text.RegularExpressions


Public NotInheritable Class LazyGCodeLine

    Private Const HorizontalRule As String = ";######################################"

    Private Shared ReadOnly TokenizerRegex As New Regex(
     "(?ix)
            (?<Comment>        ;.*$ )
          | (?<ParenComment>   \(.*?\) )
          | (?<KlipperExpr>    \[[^\]]+\] )
          | (?<KlipperParam>   \b[A-Z_][A-Z0-9_]*=[^\s]+ )
          | (?<GCode>          \b[GM]\d+(?:\.\d+)?\b )
          | (?<Axis>           \b[XYZ][+-]?\d+(?:\.\d+)?\b )
          | (?<Feed>           \b[FSE][+-]?\d+(?:\.\d+)?\b )
          | (?<Macro>          \b[A-Z_]{2,}[A-Z0-9_]*\b )
          | (?<Number>         [+-]?\d+(?:\.\d+)? )
        ",
        RegexOptions.Compiled)


    Private ReadOnly _line As String
    Private _tokens As InlineBuilder.LineTokens

    Public Sub New(line As String)
        _line = If(line, "")
    End Sub

    Public ReadOnly Property IsHorizontalRule As Boolean
        Get
            Return Tokens.IsHorizontalRule
        End Get
    End Property

    Public ReadOnly Property Tokens As InlineBuilder.LineTokens
        Get
            If _tokens Is Nothing Then _tokens = Tokenize(_line)
            Return _tokens
        End Get
    End Property

    Private Shared Function Tokenize(line As String) As InlineBuilder.LineTokens

        If String.Equals(line.Trim(), HorizontalRule, StringComparison.Ordinal) Then
            Return New InlineBuilder.LineTokens With {.IsHorizontalRule = True}
        End If

        Dim lt As New InlineBuilder.LineTokens()
        Dim last As Integer = 0

        For Each m As Match In TokenizerRegex.Matches(line)

            If m.Index > last Then
                lt.Tokens.Add(New InlineBuilder.TokenDto(0, line.Substring(last, m.Index - last)))
            End If

            If m.Groups("Comment").Success Then
                lt.Tokens.Add(New InlineBuilder.TokenDto(1, m.Value))
                last = line.Length
                Exit For
            End If

            Dim ttype As Integer
            If m.Groups("ParenComment").Success Then
                ttype = 2
            ElseIf m.Groups("KlipperExpr").Success Then
                ttype = 3
            ElseIf m.Groups("KlipperParam").Success Then
                ttype = 4
            ElseIf m.Groups("GCode").Success Then
                ttype = 5
            ElseIf m.Groups("Axis").Success Then
                ttype = 6
            ElseIf m.Groups("Feed").Success Then
                ttype = 7
            ElseIf m.Groups("Macro").Success Then
                ttype = 8
            ElseIf m.Groups("Number").Success Then
                ttype = 9
            Else
                ttype = 0
            End If

            lt.Tokens.Add(New InlineBuilder.TokenDto(ttype, m.Value))
            last = m.Index + m.Length
        Next

        If last < line.Length Then
            lt.Tokens.Add(New InlineBuilder.TokenDto(0, line.Substring(last)))
        End If

        Return lt
    End Function

End Class
