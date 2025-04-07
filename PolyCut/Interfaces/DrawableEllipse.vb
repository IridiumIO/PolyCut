Imports Svg
Imports Svg.Transforms

Public Class DrawableEllipse : Implements IDrawable

    Public Property Name As String Implements IDrawable.Name
    Public Property Children As IEnumerable(Of IDrawable) Implements IDrawable.Children
    Public Property IsHidden As Boolean Implements IDrawable.IsHidden
    Public Property IsSelected As Boolean Implements IDrawable.IsSelected
    Public Property DrawableElement As FrameworkElement Implements IDrawable.DrawableElement

    Public Sub New(element As Ellipse)
        DrawableElement = element
    End Sub

    Private Function DrawingToSVG() As SvgVisualElement

        Dim rt = CType(DrawableElement, Ellipse)

        Dim svgE = New SvgEllipse With {
            .CenterX = rt.ActualWidth / 2,
            .CenterY = rt.ActualHeight / 2,
            .RadiusX = DrawableElement.ActualWidth / 2,
            .RadiusY = DrawableElement.ActualHeight / 2,  'Why do I need this stuff below?
            .FillOpacity = 0.001,
            .Fill = New SvgColourServer(System.Drawing.Color.White),
            .Stroke = New SvgColourServer(System.Drawing.Color.Black),
            .StrokeLineCap = SvgStrokeLineCap.Round}

        Return svgE

    End Function


    Public Function GetTransformedSVGElement() As SvgVisualElement Implements IDrawable.GetTransformedSVGElement

        Dim component As SvgVisualElement = DrawingToSVG().DeepCopy


        Dim sx = component.BakeTransforms(DrawableElement)
        Return sx

    End Function
End Class

