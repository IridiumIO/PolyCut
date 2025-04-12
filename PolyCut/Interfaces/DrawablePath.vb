Imports Svg

Public Class DrawablePath : Inherits BaseDrawable : Implements IDrawable


    Public Overloads ReadOnly Property VisualName As String Implements IDrawable.VisualName
    Public Sub New(element As Path)
        DrawableElement = element
        VisualName = "Path"
    End Sub

    Public Overloads Function DrawingToSVG() As SvgVisualElement Implements IDrawable.DrawingToSVG

        Dim ln = CType(DrawableElement, Path)

        Dim paths As Pathing.SvgPathSegmentList = SvgPathBuilder.Parse(ln.Data.ToString())

        Dim svgPath As New SvgPath With {
            .PathData = paths
        }

        Return svgPath

    End Function




    Public Overloads Function GetTransformedSVGElement() As SvgVisualElement Implements IDrawable.GetTransformedSVGElement

        Dim component As SvgVisualElement = DrawingToSVG().DeepCopy

        Return component.BakeTransforms(DrawableElement)

    End Function
End Class
