Imports PolyCut.Shared

Imports Svg
Imports Svg.Transforms

Public Class DrawableEllipse : Inherits BaseDrawable : Implements IDrawable

    Public Overloads ReadOnly Property VisualName As String Implements IDrawable.VisualName

    Public Sub New(element As Ellipse)
        DrawableElement = element
        VisualName = "Ellipse"
    End Sub

    Public Overloads Function DrawingToSVG() As SvgVisualElement Implements IDrawable.DrawingToSVG

        Dim rt = CType(DrawableElement, Ellipse)

        Dim svgE = New SvgEllipse With {
            .CenterX = rt.ActualWidth / 2,
            .CenterY = rt.ActualHeight / 2,
            .RadiusX = DrawableElement.ActualWidth / 2,
            .RadiusY = DrawableElement.ActualHeight / 2,  'Why do I need this stuff below?
            .FillOpacity = 0.001,
            .Fill = New SvgColourServer(System.Drawing.Color.White),
            .Stroke = New SvgColourServer(System.Drawing.Color.Black),
            .StrokeWidth = 0.001,
            .StrokeLineCap = SvgStrokeLineCap.Round}

        Return svgE

    End Function

    Public Overloads Function GetTransformedSVGElement() As SvgVisualElement Implements IDrawable.GetTransformedSVGElement

        Dim component As SvgVisualElement = DrawingToSVG().DeepCopy

        Dim sx = component.BakeTransforms(DrawableElement)
        Return sx

    End Function
End Class

