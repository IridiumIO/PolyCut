Imports PolyCut.Shared

Imports Svg

Public Class DrawableEllipse : Inherits BaseDrawable : Implements IDrawable

    Public Overloads ReadOnly Property VisualName As String Implements IDrawable.VisualName

    Public Sub New(element As Ellipse)
        DrawableElement = element
        VisualName = "Ellipse"
        Name = VisualName
    End Sub

    Public Overloads Function DrawingToSVG() As SvgVisualElement Implements IDrawable.DrawingToSVG

        Dim rt = CType(DrawableElement, Ellipse)

        Dim fillServer As SvgColourServer = CreateSvgFillServer()
        Dim strokeServer As SvgColourServer = CreateSvgStrokeServer()
        Dim strokeW As Single = 0.001F

        Dim ellipse As New SvgEllipse With {
            .CenterX = rt.ActualWidth / 2,
            .CenterY = rt.ActualHeight / 2,
            .RadiusX = DrawableElement.ActualWidth / 2,
            .RadiusY = DrawableElement.ActualHeight / 2,
            .FillOpacity = 0.001,
            .Fill = If(fillServer, SvgPaintServer.None),
            .StrokeLineCap = SvgStrokeLineCap.Round,
            .Stroke = SvgPaintServer.None
        }

        ' Only set stroke properties if we have a stroke
        If strokeServer IsNot Nothing Then
            ellipse.Stroke = strokeServer
            ellipse.StrokeWidth = strokeW
        End If

        Return ellipse

    End Function

End Class

