Imports Svg

Public Class DrawableLine : Implements IDrawable

    Public Property Name As String Implements IDrawable.Name
    Public Property Children As IEnumerable(Of IDrawable) Implements IDrawable.Children
    Public Property IsHidden As Boolean Implements IDrawable.IsHidden
    Public Property IsSelected As Boolean Implements IDrawable.IsSelected
    Public Property DrawableElement As FrameworkElement Implements IDrawable.DrawableElement

    Public Sub New(element As Line)
        DrawableElement = element
    End Sub

    Private Function DrawingToSVG() As SvgVisualElement

        Dim ln = CType(DrawableElement, Line)

        Dim svgLine As New SvgLine With {
            .StartX = ln.X1,
            .StartY = ln.Y1,
            .EndX = ln.X2,
            .EndY = ln.Y2}


        Return svgLine

    End Function




    Public Function GetTransformedSVGElement() As SvgVisualElement Implements IDrawable.GetTransformedSVGElement

        Dim component As SvgVisualElement = DrawingToSVG().DeepCopy

        Return component.BakeTransforms(DrawableElement)

    End Function
End Class
