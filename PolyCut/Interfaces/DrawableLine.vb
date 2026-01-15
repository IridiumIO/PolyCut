Imports PolyCut.Shared

Imports Svg

Public Class DrawableLine : Inherits BaseDrawable : Implements IDrawable


    Public Overloads ReadOnly Property VisualName As String Implements IDrawable.VisualName
    Public Sub New(element As Line)
        DrawableElement = element
        VisualName = "Line"
        Name = VisualName
    End Sub

    Public Overloads Function DrawingToSVG() As SvgVisualElement Implements IDrawable.DrawingToSVG

        Dim ln = CType(DrawableElement, Line)

        Dim svgLine As New SvgLine With {
            .StartX = ln.X1,
            .StartY = ln.Y1,
            .EndX = ln.X2,
            .EndY = ln.Y2,
            .StrokeWidth = 0.001}


        Return svgLine

    End Function




    Public Overloads Function GetTransformedSVGElement() As SvgVisualElement Implements IDrawable.GetTransformedSVGElement

        Dim component As SvgVisualElement = DrawingToSVG().DeepCopy

        Return component.BakeTransforms(DrawableElement)

    End Function
End Class
