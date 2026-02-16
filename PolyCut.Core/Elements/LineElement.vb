Imports System.Windows.Media
Imports System.Windows.Shapes

Imports Svg

Public Class LineElement : Implements IPathBasedElement

    Public ReadOnly Property FlattenedLines As List(Of GeoLine) Implements IPathBasedElement.FlattenedLines
        Get
            Return Figures.SelectMany(Of GeoLine)(Function(x) x).ToList
        End Get
    End Property
    Public Property Geo As PathGeometry Implements IPathBasedElement.Geo
    Public Property Figures As New List(Of List(Of GeoLine)) Implements IPathBasedElement.Figures
    Public Property Config As ProcessorConfiguration Implements IPathBasedElement.Config
    Public Property IsFilled As Boolean = False Implements IPathBasedElement.IsFilled
    Public Sub CompileFromSVGElement(element As SvgVisualElement, cfg As ProcessorConfiguration) Implements IPathBasedElement.CompileFromSVGElement
        Dim ln = DirectCast(element, SvgLine)
        Config = cfg

        Figures.Add(New List(Of GeoLine) From {
                    New GeoLine(ln.StartX.Value, ln.StartY.Value, ln.EndX.Value, ln.EndY.Value)
                    })
        Figures = Figures.Select(Function(fig) TransformLines(fig, element.Transforms?.GetMatrix).ToList).ToList()

        For fi = 0 To Figures.Count - 1
            For li = 0 To Figures(fi).Count - 1
                Dim g = Figures(fi)(li)
                g = g.WithTag(Nothing)
                Figures(fi)(li) = g
            Next
        Next

    End Sub
End Class
