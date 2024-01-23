
Imports System.Globalization
Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes
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

    Public Sub CompileFromSVGElement(element As SvgVisualElement, cfg As ProcessorConfiguration) Implements IPathBasedElement.CompileFromSVGElement
        Dim text = DirectCast(element, SvgText)
        Config = cfg

        Figures = GenerateFigures(text)
        Figures = Figures.Select(Function(fig) TransformLines(fig, element.Transforms.GetMatrix).ToList).ToList()

        Dim pgl As List(Of PathGeometry) = Figures.Select(Function(fig) LinesToPathGeometry(fig)).ToList

        If cfg.AutoUnionText Then
            Geo = UnionGeometries(pgl)
        Else
            Geo = New PathGeometry
            For Each pg In pgl
                For Each fig In pg.Figures
                    Geo.Figures.Add(fig)
                Next
            Next
        End If


        Figures = BuildLinesFromGeometry(Geo, cfg.Tolerance)

    End Sub


    Public Shared Function UnionGeometries(pgl As List(Of PathGeometry)) As PathGeometry

        Dim unionedGeos As New PathGeometry

        Dim containedGeometries As New List(Of PathGeometry)

        For i = 0 To pgl.Count - 1
            Dim iscontained As Boolean = False
            For j = 0 To pgl.Count - 1
                If i <> j AndAlso pgl(j).FillContains(pgl(i)) Then
                    containedGeometries.Add(pgl(i))
                    iscontained = True
                    Exit For
                End If
            Next

            If Not iscontained Then
                unionedGeos = Geometry.Combine(unionedGeos, pgl(i), GeometryCombineMode.Union, Nothing)
            Else
                containedGeometries.Add(pgl(i))
            End If

        Next

        'We apply the exclude operation to the unioned geometries to remove the contained geometries. This handles things like holes in the O/A/g etc.
        For Each geometryToExclude In containedGeometries
            unionedGeos = Geometry.Combine(unionedGeos, geometryToExclude, GeometryCombineMode.Exclude, Nothing)
        Next

        Return unionedGeos
    End Function

    Public Shared Function LinesToPathGeometry(lines As List(Of Line)) As PathGeometry
        Dim geometry As New PathGeometry()
        Dim figure As PathFigure = Nothing

        For i As Integer = 0 To lines.Count - 1
            Dim line = lines(i)

            If figure Is Nothing Then
                figure = New PathFigure() With {
                .StartPoint = New Point(line.X1, line.Y1),
                .IsClosed = False
            }
                geometry.Figures.Add(figure)
            End If

            figure.Segments.Add(New LineSegment() With {
            .Point = New Point(line.X2, line.Y2)
        })

            If i < lines.Count - 1 AndAlso Not (line.X2 = lines(i + 1).X1 AndAlso line.Y2 = lines(i + 1).Y1) Then
                figure = Nothing
            End If
        Next

        Return geometry
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
