
Imports Svg
Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes

Public Class PathElement : Implements IPathBasedElement

    Public ReadOnly Property FlattenedLines As List(Of Line) Implements IPathBasedElement.FlattenedLines
        Get
            Return Figures.SelectMany(Of Line)(Function(x) x).ToList
        End Get
    End Property
    Public Property Geo As PathGeometry Implements IPathBasedElement.Geo
    Public Property Config As ProcessorConfiguration Implements IPathBasedElement.Config
    Public Property Figures As List(Of List(Of Line)) Implements IPathBasedElement.Figures
    Public Property IsFilled As Boolean = False Implements IPathBasedElement.IsFilled
    Public Sub CompileFromSVGElement(element As SvgVisualElement, cfg As ProcessorConfiguration) Implements IPathBasedElement.CompileFromSVGElement

        Dim path = DirectCast(element, SvgPath)
        Config = cfg

        Dim fillcolor = SVGProcessor.SVGColorBullshitFixer(element.Fill)

        Geo = Geometry.Parse(path.PathData.ToString).GetFlattenedPathGeometry(Config.Tolerance, ToleranceType.Absolute)

        Figures = BuildLinesFromGeometry(Geo, Config.Tolerance)
        Figures = Figures.Select(Function(fig) TransformLines(fig, element.Transforms.GetMatrix).ToList).ToList()

        For Each fig In Figures
            For Each ln In fig
                ln.Tag = fillcolor
            Next
        Next

    End Sub



End Class