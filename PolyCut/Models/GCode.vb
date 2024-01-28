Imports System.Collections.ObjectModel
Imports System.Text.RegularExpressions

Imports CommunityToolkit.Mvvm.ComponentModel

Imports PolyCut.Core


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
            GCode.Add(Core.GCode.Parse(line))
        Next

        BuildLines()


    End Sub

    Public Sub New(gcodeCollection As List(Of GCode))
        GCode.AddRange(gcodeCollection)

        BuildLines()
    End Sub

    Public Sub BuildLines()

        Dim isFirstLine As Boolean = True

        For i As Integer = 0 To GCode.Count - 2


            If GCode(i).Mode <> "G" Then Continue For

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
                           .Stroke = New SolidColorBrush(DirectCast(ColorConverter.ConvertFromString("#ccccff"), Color)),
                           .StrokeThickness = 0.2,
                           .StrokeEndLineCap = PenLineCap.Round,
                           .StrokeStartLineCap = PenLineCap.Round}

        ' If it's a rapid move, change the stroke color
        If isRapidMove Then
            line.Stroke = Brushes.OrangeRed
        End If


        Return line

    End Function
End Class
