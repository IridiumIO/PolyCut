
Imports System.Globalization
Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes

Imports PolyCut.[Shared]

Imports Svg

Public Class TextElement : Implements IPathBasedElement

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
        Dim text = DirectCast(element, SvgText)
        Config = cfg

        Dim fillcolor = ColorAndBrushHelpers.SVGPaintServerToString(element.Fill)

        Figures = GenerateFigures(text)
        Dim m = element.Transforms.GetMatrix()

        Dim pgl = Figures.Select(Function(fig)
                                     fig.TransformLinesInPlace(m)
                                     Return LinesToPathGeometry(fig)
                                 End Function).ToList()


        Geo = If(cfg.AutoUnionText,
                 UnionGeometries(pgl),
                 New PathGeometry(pgl.SelectMany(Function(pg) pg.Figures), FillRule.Nonzero, Nothing))

        Figures = BuildLinesFromGeometry(Geo, cfg.Tolerance)
        Figures.ForEach(Sub(fig) fig.ForEach(Sub(ln) ln.Tag = fillcolor))

    End Sub

    Public Shared Function UnionGeometries(pgl As List(Of PathGeometry)) As PathGeometry
        If pgl Is Nothing OrElse pgl.Count = 0 Then Return New PathGeometry()

        Dim items = pgl.Select(Function(g, i) (g, i, b:=If(g Is Nothing, Rect.Empty, g.Bounds))).Where(Function(t) t.g IsNot Nothing).ToArray()

        Dim marked = items.Select(Function(a) (a.g, isHole:=items.Any(Function(b) b.i <> a.i AndAlso b.b.Contains(a.b) AndAlso b.g.FillContains(a.g)))).ToArray()

        Dim keep = marked.Where(Function(x) Not x.isHole).Select(Function(x) DirectCast(x.g, Geometry)).ToList()
        Dim holes = marked.Where(Function(x) x.isHole).Select(Function(x) x.g).ToList()

        Dim unioned As Geometry = BalancedCombine(keep, GeometryCombineMode.Union)
        holes.ForEach(Sub(h) unioned = Geometry.Combine(unioned, h, GeometryCombineMode.Exclude, Nothing))

        Return If(TryCast(unioned, PathGeometry), unioned.GetFlattenedPathGeometry())
    End Function

    Private Shared Function BalancedCombine(items As List(Of Geometry), mode As GeometryCombineMode) As Geometry
        If items Is Nothing OrElse items.Count = 0 Then Return New PathGeometry()
        If items.Count = 1 Then Return items(0)

        Dim current As New List(Of Geometry)(items)

        While current.Count > 1
            Dim nextRound As New List(Of Geometry)((current.Count + 1) \ 2)

            Dim i As Integer = 0
            While i < current.Count
                If i = current.Count - 1 Then
                    nextRound.Add(current(i))
                    Exit While
                End If

                Dim combined = Geometry.Combine(current(i), current(i + 1), mode, Nothing)
                nextRound.Add(combined)
                i += 2
            End While

            current = nextRound
        End While

        Return current(0)
    End Function

    Private Function GenerateFigures(text As SvgText) As List(Of List(Of Line))

        Dim tp = text.Path(Nothing).PathData.Points.ToList
        Dim tt = text.Path(Nothing).PathData.Types.ToList


        For Each child In text.Children

            Dim tBase = DirectCast(child, SvgTextBase)
            If tBase Is Nothing Then Continue For
            Dim tBasePath As System.Drawing.Drawing2D.PathData = tBase.Path(Nothing).PathData

            tp.AddRange(tBasePath.Points)
            tt.AddRange(tBasePath.Types)

        Next

        Dim geoPath As New System.Drawing.Drawing2D.GraphicsPath(tp.ToArray, tt.ToArray)
        geoPath.Flatten(New System.Drawing.Drawing2D.Matrix, Config.Tolerance * 10)

        Dim subpaths As New List(Of List(Of Line))

        Dim currentSubpath As New List(Of Line)

        For i As Integer = 0 To geoPath.PathPoints.Length - 1

            Dim point = geoPath.PathPoints(i)
            Dim type As System.Drawing.Drawing2D.PathPointType = geoPath.PathTypes(i)

            If type = 0 Then
                currentSubpath = New List(Of Line)
            End If

            If type.HasFlag(System.Drawing.Drawing2D.PathPointType.Line) Then
                Dim line As New Line With {
                            .X1 = geoPath.PathPoints(i - 1).X,
                            .Y1 = geoPath.PathPoints(i - 1).Y,
                            .X2 = point.X,
                            .Y2 = point.Y
                        }
                currentSubpath.Add(line)
            End If

            If type.HasFlag(System.Drawing.Drawing2D.PathPointType.CloseSubpath) Then
                Dim line As New Line With {
                            .X1 = point.X,
                            .Y1 = point.Y,
                            .X2 = currentSubpath.First.X1,
                            .Y2 = currentSubpath.First.Y1
                        }
                currentSubpath.Add(line)
                subpaths.Add(currentSubpath)
            End If

        Next


        Return subpaths
    End Function


End Class
