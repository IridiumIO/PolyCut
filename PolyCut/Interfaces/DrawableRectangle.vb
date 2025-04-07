Imports Svg
Imports Svg.Transforms

Public Class DrawableRectangle : Implements IDrawable

    Public Property Name As String Implements IDrawable.Name
    Public Property Children As IEnumerable(Of IDrawable) Implements IDrawable.Children
    Public Property IsHidden As Boolean Implements IDrawable.IsHidden
    Public Property IsSelected As Boolean Implements IDrawable.IsSelected
    Public Property DrawableElement As FrameworkElement Implements IDrawable.DrawableElement

    Public Sub New(element As Rectangle)
        DrawableElement = element
    End Sub

    Private Function DrawingToSVG() As SvgVisualElement

        Dim rt = CType(DrawableElement, Rectangle)

        Return New SvgRectangle With {
            .X = 0,
            .Y = 0,
            .Width = rt.ActualWidth,
            .Height = rt.ActualHeight,  'Why do I need this stuff below?
            .FillOpacity = 0.001,
            .Fill = New SvgColourServer(System.Drawing.Color.White),
            .Stroke = New SvgColourServer(System.Drawing.Color.Black),
            .StrokeLineCap = SvgStrokeLineCap.Round}

    End Function


    Public Function GetTransformedSVGElement() As SvgVisualElement Implements IDrawable.GetTransformedSVGElement

        Dim component As SvgVisualElement = DrawingToSVG().DeepCopy

        Return component.BakeTransforms(DrawableElement)

    End Function
End Class

