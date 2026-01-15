Imports CommunityToolkit.Mvvm.ComponentModel

Imports PolyCut.Shared

Imports Svg
Imports Svg.Transforms

Public Class DrawableRectangle : Inherits BaseDrawable : Implements IDrawable


    Public Overloads ReadOnly Property VisualName As String Implements IDrawable.VisualName

    Public Sub New(element As Rectangle)
        DrawableElement = element
        VisualName = "Rectangle"
        Name = VisualName
    End Sub

    Public Overloads Function DrawingToSVG() As SvgVisualElement Implements IDrawable.DrawingToSVG

        Dim rt = CType(DrawableElement, Rectangle)

        Return New SvgRectangle With {
            .X = 0,
            .Y = 0,
            .Width = rt.ActualWidth,
            .Height = rt.ActualHeight,  'Why do I need this stuff below?
            .FillOpacity = 0.001,
            .Fill = New SvgColourServer(System.Drawing.Color.White),
            .Stroke = New SvgColourServer(System.Drawing.Color.Black),
            .StrokeWidth = 0.001,
            .StrokeLineCap = SvgStrokeLineCap.Round}

    End Function


    Public Overloads Function GetTransformedSVGElement() As SvgVisualElement Implements IDrawable.GetTransformedSVGElement

        Dim component As SvgVisualElement = DrawingToSVG().DeepCopy

        Return component.BakeTransforms(DrawableElement)

    End Function
End Class

