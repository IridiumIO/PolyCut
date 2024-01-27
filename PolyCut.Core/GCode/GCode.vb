Imports System.Text.RegularExpressions
Imports System.Windows

'A simplified GCode class since we don't need all the features of the full GCode spec
Public Class GCode

    Public Property Mode As String = "G"
    Public Property Code As Integer? = Nothing
    Public Property X As Double? = Nothing
    Public Property Y As Double? = Nothing
    Public Property Z As Double? = Nothing
    Public Property E As Double? = Nothing
    Public Property F As Double? = Nothing
    Public Property Comment As String = Nothing
    Public Property BlankLine As Boolean = False

    Public Sub New()
    End Sub

    Public Sub New(G As Integer, X As Double?, Y As Double?, Z As Double?, Feedrate As Double?)
        Code = G
        Me.X = X
        Me.Y = Y
        Me.Z = Z
        Me.F = Feedrate
    End Sub
    Public Sub New(M As String, G As Integer?, X As Double?, Y As Double?, Z As Double?, E As Double?, F As Double?, Optional Comment As String = Nothing)
        Mode = M
        Code = G
        Me.X = X
        Me.Y = Y
        Me.Z = Z
        Me.E = E
        Me.F = F
        Me.Comment = Comment
    End Sub


    Public Shared Function Parse(line As String) As GCode
        Dim M As String = Nothing
        Dim Code As Integer? = Nothing
        Dim X As Double? = Nothing
        Dim Y As Double? = Nothing
        Dim Z As Double? = Nothing
        Dim E As Double? = Nothing
        Dim F As Double? = Nothing
        Dim Comment As String = Nothing

        Dim cIndex = line.IndexOf(";"c)
        If cIndex <> -1 Then
            Comment = line.Substring(cIndex + 1)
            line = line.Substring(0, cIndex)
        End If

        Dim matches = Regex.Matches(line, "([A-Z])(-?\d+(.\d+)?)")


        For Each match As Match In matches
            Dim parameterType As Char = match.Groups(1).Value(0)
            Dim parameterValue As Double? = CDbl(match.Groups(2).Value)

            Select Case parameterType
                Case "F"
                    F = If(parameterValue, Nothing)
                Case "E"
                    E = If(parameterValue, Nothing)
                Case "X"
                    X = If(parameterValue, Nothing)
                Case "Y"
                    Y = If(parameterValue, Nothing)
                Case "Z"
                    Z = If(parameterValue, Nothing)
                Case "G"
                    Code = If(parameterValue, Nothing)
                    M = "G"
                Case "M"
                    Code = If(parameterValue, Nothing)
                    M = "M"

            End Select

        Next
        Return New GCode(M, Code, X, Y, Z, E, F, Comment)

    End Function

    Public Shared Function CommentLine(comment As String) As GCode

        Return New GCode With {.Comment = comment, .Mode = Nothing}

    End Function

    Public Shared Function Blank() As GCode
        Return New GCode With {.BlankLine = True, .Mode = Nothing}

    End Function

    Public Overrides Function ToString() As String
        Dim GcodeLine As String = ""

        If BlankLine Then Return ""

        GcodeLine += Mode
        If Code IsNot Nothing Then
            GcodeLine += Code.ToString
        End If
        If X IsNot Nothing Then
            GcodeLine += " X" + Math.Round(X.Value, 3).ToString
        End If
        If Y IsNot Nothing Then
            GcodeLine += " Y" + Math.Round(Y.Value, 3).ToString
        End If
        If Z IsNot Nothing Then
            GcodeLine += " Z" + Math.Round(Z.Value, 3).ToString
        End If
        If E IsNot Nothing Then
            GcodeLine += " E" + Math.Round(E.Value, 3).ToString
        End If
        If F IsNot Nothing Then
            GcodeLine += " F" + Math.Round(F.Value, 3).ToString
        End If
        If Comment IsNot Nothing Then
            GcodeLine += " ;" + Comment
        End If

        Return GcodeLine.Trim

    End Function

    Public Shared Function G1(Optional X As Double? = Nothing, Optional Y As Double? = Nothing, Optional Z As Double? = Nothing, Optional F As Double? = Nothing) As GCode
        Return New GCode(1, X, Y, Z, F)
    End Function
    Public Shared Function G1(Optional XY As Point = Nothing, Optional Z As Double? = Nothing, Optional F As Double? = Nothing) As GCode
        Return New GCode(1, XY.X, XY.Y, Z, F)
    End Function
    Public Shared Function G0(Optional X As Double? = Nothing, Optional Y As Double? = Nothing, Optional Z As Double? = Nothing, Optional F As Double? = Nothing) As GCode
        Return New GCode(0, X, Y, Z, F)
    End Function
    Public Shared Function G0(Optional XY As Point = Nothing, Optional Z As Double? = Nothing, Optional F As Double? = Nothing) As GCode
        Return New GCode(0, XY.X, XY.Y, Z, F)
    End Function

    Public Shared Function GZ(Z As Double, Optional F As Double? = Nothing)
        Return New GCode(0, Nothing, Nothing, Z, F)
    End Function

End Class

