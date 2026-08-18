Imports PolyCut.Shared

Imports Svg

Public Class DrawableRectangle : Inherits BaseDrawable : Implements IDrawable


    Public Overloads ReadOnly Property VisualName As String Implements IDrawable.VisualName

    Public Sub New(element As Rectangle)
        DrawableElement = element
        VisualName = "Rectangle"
        Name = VisualName
    End Sub

    Public Overrides Function DrawingToSVG() As SvgVisualElement Implements IDrawable.DrawingToSVG

        Dim rt = CType(DrawableElement, Rectangle)

        Dim fillServer As SvgColourServer = CreateSvgFillServer()
        Dim strokeServer As SvgColourServer = CreateSvgStrokeServer()
        Dim strokeW As Single = 0.001F

        Dim rect As New SvgRectangle With {
            .X = 0,
            .Y = 0,
            .Width = rt.ActualWidth,
            .Height = rt.ActualHeight,
            .FillOpacity = 0.01,
            .Fill = If(fillServer, SvgPaintServer.None),
            .StrokeLineCap = SvgStrokeLineCap.Round,
            .Stroke = SvgPaintServer.None
        }

        ' Only set stroke properties if we have a stroke
        If strokeServer IsNot Nothing Then
            rect.Stroke = strokeServer
            rect.StrokeWidth = strokeW
        End If

        Return rect

    End Function


End Class

