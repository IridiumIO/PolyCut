
Imports Svg
Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes

Public Class PathElement : Implements IPathBasedElement

    Public Property Lines As List(Of Line) Implements IPathBasedElement.Lines
    Public Property Geo As PathGeometry Implements IPathBasedElement.Geo
    Public Property Config As ProcessorConfiguration Implements IPathBasedElement.Config

    Public Sub CompileElement(element As SvgVisualElement, cfg As ProcessorConfiguration) Implements IPathBasedElement.CompileElement

        Dim path = DirectCast(element, SvgPath)
        Config = cfg

        Geo = Geometry.Parse(path.PathData.ToString).GetFlattenedPathGeometry(Config.Tolerance, ToleranceType.Absolute)

        Lines = BuildLinesFromGeometry(Geo, Config.Tolerance)
        Lines = TransformLines(Lines, element.Transforms.GetMatrix)

        Dim fillLs = Fill.FillLines(Lines, 2, 0)

        Lines = OffsetProcessor.ProcessOffsets(Lines, Config.Offset, Config.Overcut)

    End Sub



End Class