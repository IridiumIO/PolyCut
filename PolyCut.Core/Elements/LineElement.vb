Imports System.Windows.Media
Imports System.Windows.Shapes

Imports Svg

Public Class LineElement : Implements IPathBasedElement

    Public ReadOnly Property FlattenedLines As List(Of Line) Implements IPathBasedElement.FlattenedLines
    Public Property Geo As PathGeometry Implements IPathBasedElement.Geo
    Public Property Figures As New List(Of List(Of Line)) Implements IPathBasedElement.Figures
    Public Property Config As ProcessorConfiguration Implements IPathBasedElement.Config
    Public Property IsFilled As Boolean = False Implements IPathBasedElement.IsFilled
    Public Sub CompileFromSVGElement(element As SvgVisualElement, cfg As ProcessorConfiguration) Implements IPathBasedElement.CompileFromSVGElement
        Dim ln = DirectCast(element, SvgLine)
        Config = cfg

        Figures.Add(New List(Of Line) From {
                    New Line With {
                    .X1 = ln.StartX.Value,
                    .Y1 = ln.StartY.Value,
                    .X2 = ln.EndX.Value,
                    .Y2 = ln.EndY.Value
                    }})
        Figures = Figures.Select(Function(fig) TransformLines(fig, element.Transforms?.GetMatrix).ToList).ToList()
        For Each fig In Figures
            For Each lsn In fig
                lsn.Tag = Nothing
            Next
        Next

    End Sub
End Class
