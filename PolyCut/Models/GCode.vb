Imports System.Collections.ObjectModel
Imports System.Text.RegularExpressions

Imports CommunityToolkit.Mvvm.ComponentModel

Imports PolyCut.Core

Public Class GCodeGeometry : Inherits ObservableObject

    ' Use List for performance, convert to ObservableCollection later if needed
    Private ReadOnly _paths As List(Of Line) = New List(Of Line)
    Public ReadOnly Property Paths As ReadOnlyCollection(Of Line)
        Get
            Return _paths.AsReadOnly()
        End Get
    End Property

    ' Lazy-evaluated and cached travel paths
    Private _travelPaths As List(Of Line) = Nothing
    Public ReadOnly Property TravelPaths As IEnumerable(Of Line)
        Get
            If _travelPaths Is Nothing Then
                _travelPaths = _paths.Where(Function(f) f.Stroke Is Brushes.OrangeRed).ToList()
            End If
            Return _travelPaths
        End Get
    End Property

    ' Use List for faster bulk population
    Public ReadOnly Property GCode As List(Of GCode)

    Public Sub New(instr As String)
        GCode = instr.Split(Environment.NewLine).
                     Select(Function(line) Core.GCode.Parse(line)).
                     Where(Function(g) g IsNot Nothing).
                     ToList()
        BuildLines()
    End Sub

    Public Sub New(gcodeCollection As List(Of GCode))
        GCode = New List(Of GCode)(gcodeCollection)
        BuildLines()
    End Sub

    Public Sub BuildLines()
        _paths.Clear()
        _travelPaths = Nothing ' Invalidate cache

        Dim lastX As Double = 0
        Dim lastY As Double = 0
        Dim firstLineDrawn As Boolean = False

        For i = 0 To GCode.Count - 1
            Dim cmd = GCode(i)
            If cmd.Mode <> "G" OrElse (cmd.Code <> 0 AndAlso cmd.Code <> 1) Then Continue For
            If cmd.X Is Nothing OrElse cmd.Y Is Nothing Then Continue For

            Dim x = cmd.X.Value
            Dim y = cmd.Y.Value
            Dim isRapid = cmd.Code = 0

            If Not firstLineDrawn Then
                _paths.Add(DrawLine(0, 0, x, y, True))
                firstLineDrawn = True
            Else
                _paths.Add(DrawLine(lastX, lastY, x, y, isRapid))
            End If

            lastX = x
            lastY = y
        Next
    End Sub

    Private Shared Function DrawLine(x1 As Double, y1 As Double, x2 As Double, y2 As Double, Optional isRapidMove As Boolean = False) As Line
        Return New Line With {
            .X1 = Math.Round(x1, 2),
            .Y1 = Math.Round(y1, 2),
            .X2 = Math.Round(x2, 2),
            .Y2 = Math.Round(y2, 2),
            .Stroke = If(isRapidMove, Brushes.OrangeRed, New SolidColorBrush(DirectCast(ColorConverter.ConvertFromString("#ccccff"), Color))),
            .StrokeThickness = If(isRapidMove, 0.1, 0.2),
            .StrokeEndLineCap = PenLineCap.Round,
            .StrokeStartLineCap = PenLineCap.Round
        }
    End Function
End Class
