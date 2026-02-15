
Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes

Imports MeasurePerformance.IL.Weaver

Imports PolyCut.[Shared]

Imports Svg

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

    <MeasurePerformance>
    Public Sub CompileFromSVGElement(element As SvgVisualElement, cfg As ProcessorConfiguration) Implements IPathBasedElement.CompileFromSVGElement

        Dim path = DirectCast(element, SvgPath)
        Config = cfg

        Dim fillcolor = ColorAndBrushHelpers.SVGPaintServerToString(element.Fill)

        Geo = Geometry.Parse(path.PathData.ToString).GetFlattenedPathGeometry(Config.Tolerance, ToleranceType.Absolute)
        Dim m As System.Drawing.Drawing2D.Matrix = element.Transforms.GetMatrix()
        Figures = BuildLinesFromGeometry(Geo, Config.Tolerance)
        Figures.ForEach(Sub(fig)
                            fig.TransformLinesInPlace(m)
                            fig.ForEach(Sub(ln) ln.Tag = fillcolor)
                        End Sub)

    End Sub



End Class