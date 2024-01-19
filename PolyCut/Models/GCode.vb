Imports System.Collections.ObjectModel
Imports System.Text.RegularExpressions
Imports CommunityToolkit.Mvvm.ComponentModel

Public Class GCode : Inherits ObservableObject

    Public Enum LinearMove
        G0
        G1
    End Enum

    Public Property Code As Integer?

    Public Property F As Double?
    Public Property E As Double?
    Public Property X As Double?
    Public Property Y As Double?
    Public Property Z As Double?

    Public Property OriginalString As String = Nothing

    Public Sub New(line As String)
        line = line.Trim
        If String.IsNullOrWhiteSpace(line) Then
            Return
        End If

        OriginalString = line

        ParseGCode(line)

    End Sub

    Public Sub ParseGCode(line As String)
        Dim matches = Regex.Matches(line, "([A-Z])(-?\d+(\.\d+)?)")

        For Each match As Match In matches
            Dim parameterType As Char = match.Groups(1).Value(0)
            Dim parameterValue As Double = CDbl(match.Groups(2).Value)

            Select Case parameterType
                Case "F"
                    F = parameterValue
                Case "E"
                    E = parameterValue
                Case "X"
                    X = parameterValue
                Case "Y"
                    Y = parameterValue
                Case "Z"
                    Z = parameterValue
                Case "G"
                    Code = parameterValue
            End Select
        Next
    End Sub

End Class

Public Class GCodeGeometry : Inherits ObservableObject

    Public Property GCode As New ObservableCollection(Of GCode)


    Public Property Paths As New ObservableCollection(Of Line)

    Public ReadOnly Property TravelPaths
        Get
            Return Paths.Where(Function(f) f.Stroke Is Brushes.OrangeRed)
        End Get
    End Property

    Public Sub New(instr As String)

        For Each line As String In instr.Split(Environment.NewLine)
            GCode.Add(New GCode(line))
        Next

        BuildLines()


    End Sub

    Public Sub BuildLines()

        Dim isFirstLine As Boolean = True

        For i As Integer = 0 To GCode.Count - 2

            'We only care about G0 and G1 moves
            If GCode(i).Code <> 0 AndAlso GCode(i).Code <> 1 Then
                Continue For
            End If
            'That contain X and Y moves
            If GCode(i).X Is Nothing OrElse GCode(i).Y Is Nothing Then
                Continue For
            End If

            Dim nextUsable As Integer = i + 1

            While GCode(nextUsable)?.X Is Nothing OrElse GCode(nextUsable)?.Y Is Nothing
                If nextUsable = GCode.Count - 1 Then
                    Continue For
                End If
                nextUsable += 1
            End While

            If isFirstLine Then
                Dim fline As Line = DrawLine(0, 0, GCode(i).X, GCode(i).Y, True)
                Paths.Add(fline)
                isFirstLine = False
            End If

            Dim line As Line = DrawLine(GCode(i).X, GCode(i).Y, GCode(nextUsable).X, GCode(nextUsable).Y, GCode(nextUsable).Code = 0)

            Paths.Add(line)

            i = nextUsable - 1

        Next


    End Sub

    Private Function DrawLine(x1 As Double, y1 As Double, x2 As Double, y2 As Double, Optional isRapidMove As Boolean = False) As Line

        Dim line As New Line With {
                           .X1 = Math.Round(x1, 2),
                           .Y1 = Math.Round(y1, 2),
                           .X2 = Math.Round(x2, 2),
                           .Y2 = Math.Round(y2, 2),
                           .Stroke = New SolidColorBrush(DirectCast(ColorConverter.ConvertFromString("#bbbbff"), Color)),
                           .StrokeThickness = 0.2
}

        ' If it's a rapid move, change the stroke color
        If isRapidMove Then
            line.Stroke = Brushes.OrangeRed
        End If


        Return line

    End Function
End Class
