

Imports System.IO
Imports System.Runtime.CompilerServices
Imports System.Xml

Imports PolyCut.[Shared]

Imports Svg
Imports Svg.Transforms

Public Interface ISvgImportService
    Function ParseFromFile(path As String) As IEnumerable(Of IDrawable)
    Function ParseFromDocument(svgDoc As Svg.SvgDocument, Optional sourceName As String = Nothing) As IEnumerable(Of IDrawable)
End Interface

Public Class SVGImportService : Implements ISvgImportService

    Private Const FLATTENING_TOLERANCE As Double = 0.05

    Public Function ParseFromFile(path As String) As IEnumerable(Of IDrawable) Implements ISvgImportService.ParseFromFile
        Dim doc = SvgDocument.Open(path)
        Return ParseFromDocument(doc, path)
    End Function

    Public Function ParseFromDocument(svgDoc As SvgDocument, Optional sourceName As String = Nothing) As IEnumerable(Of IDrawable) Implements ISvgImportService.ParseFromDocument
        Dim rootMatrix As Matrix = CalculateDocumentTransform(svgDoc)

        Dim results As New List(Of IDrawable)

        For Each child As SvgElement In svgDoc.Children
            ProcessElement(child, rootMatrix, results, svgDoc)
        Next

        Return results
    End Function

    Private Function CalculateDocumentTransform(svgDoc As SvgDocument) As Matrix
        Dim m As Matrix = Matrix.Identity

        Dim docWidthMM As Double = ConvertToMM(svgDoc.Width.Value, svgDoc.Width.Type)
        Dim docHeightMM As Double = ConvertToMM(svgDoc.Height.Value, svgDoc.Height.Type)

        If svgDoc.ViewBox.Width > 0 AndAlso svgDoc.ViewBox.Height > 0 AndAlso docWidthMM > 0 AndAlso docHeightMM > 0 Then

            Dim vbW As Double = svgDoc.ViewBox.Width
            Dim vbH As Double = svgDoc.ViewBox.Height

            Dim sx As Double = docWidthMM / vbW
            Dim sy As Double = docHeightMM / vbH

            ' SVG default preserveAspectRatio: xMidYMid meet (uniform scale)
            Dim s As Double = Math.Min(sx, sy)

            ' size of the scaled viewBox in mm
            Dim scaledW As Double = vbW * s
            Dim scaledH As Double = vbH * s

            ' default alignment xMidYMid: center the leftover space
            Dim extraX As Double = (docWidthMM - scaledW) / 2.0
            Dim extraY As Double = (docHeightMM - scaledH) / 2.0

            ' IMPORTANT: translate viewBox min first, THEN scale, THEN add centering offsets
            m.Translate(-svgDoc.ViewBox.MinX, -svgDoc.ViewBox.MinY)
            m.Scale(s, s)
            m.Translate(extraX, extraY)

            Return m
        End If

        ' No viewBox: treat units as document units
        Dim unitScale As Double = ConvertSVGScaleToMM(svgDoc.Width.Type)
        m.Scale(unitScale, unitScale)
        Return m
    End Function

    Private Function CalculateStrokeDimensions(bounds As Rect, strokeWidth As Single, matrix As Matrix) As (totalWidth As Double, totalHeight As Double, strokeOffset As Double, transformedStroke As Double)
        Dim matrixScale As Double = Math.Sqrt(matrix.M11 * matrix.M11 + matrix.M12 * matrix.M12)
        Dim strokeThickness As Double = If(strokeWidth > 0, strokeWidth * matrixScale, 0)
        Return (bounds.Width + strokeThickness, bounds.Height + strokeThickness, strokeThickness / 2.0, strokeThickness)
    End Function

    Private Sub AssignDrawableName(drawable As IDrawable, elementId As String)
        If Not String.IsNullOrEmpty(elementId) Then
            drawable.Name = elementId
        End If
    End Sub

    Private Function TransformPathGeometryPreserveOpenClosed(pg As PathGeometry, m As Matrix) As PathGeometry
        If pg Is Nothing Then Return Nothing
        If m.IsIdentity Then Return pg.Clone()

        Dim result As New PathGeometry() With {.FillRule = pg.FillRule}

        For Each fig In pg.Figures
            Dim startLocal As Point = fig.StartPoint
            Dim curLocal As Point = startLocal

            Dim newFig As New PathFigure() With {
                .StartPoint = m.Transform(startLocal),
                .IsClosed = fig.IsClosed,
                .IsFilled = fig.IsFilled
            }

            For Each seg In fig.Segments

                If TypeOf seg Is LineSegment Then
                    Dim ls = DirectCast(seg, LineSegment)
                    newFig.Segments.Add(New LineSegment(m.Transform(ls.Point), ls.IsStroked))
                    curLocal = ls.Point

                ElseIf TypeOf seg Is PolyLineSegment Then
                    Dim pls = DirectCast(seg, PolyLineSegment)
                    Dim pts As New PointCollection(pls.Points.Select(Function(p) m.Transform(p)))
                    newFig.Segments.Add(New PolyLineSegment(pts, pls.IsStroked))
                    If pls.Points.Count > 0 Then curLocal = pls.Points(pls.Points.Count - 1)

                ElseIf TypeOf seg Is BezierSegment Then
                    Dim bs = DirectCast(seg, BezierSegment)
                    newFig.Segments.Add(New BezierSegment(
                    m.Transform(bs.Point1),
                    m.Transform(bs.Point2),
                    m.Transform(bs.Point3),
                    bs.IsStroked))
                    curLocal = bs.Point3

                ElseIf TypeOf seg Is PolyBezierSegment Then
                    Dim pbs = DirectCast(seg, PolyBezierSegment)
                    Dim pts As New PointCollection(pbs.Points.Select(Function(p) m.Transform(p)))
                    newFig.Segments.Add(New PolyBezierSegment(pts, pbs.IsStroked))
                    If pbs.Points.Count > 0 Then curLocal = pbs.Points(pbs.Points.Count - 1)

                ElseIf TypeOf seg Is QuadraticBezierSegment Then
                    Dim qs = DirectCast(seg, QuadraticBezierSegment)
                    newFig.Segments.Add(New QuadraticBezierSegment(
                    m.Transform(qs.Point1),
                    m.Transform(qs.Point2),
                    qs.IsStroked))
                    curLocal = qs.Point2

                ElseIf TypeOf seg Is PolyQuadraticBezierSegment Then
                    Dim pqs = DirectCast(seg, PolyQuadraticBezierSegment)
                    Dim pts As New PointCollection(pqs.Points.Select(Function(p) m.Transform(p)))
                    newFig.Segments.Add(New PolyQuadraticBezierSegment(pts, pqs.IsStroked))
                    If pqs.Points.Count > 0 Then curLocal = pqs.Points(pqs.Points.Count - 1)

                ElseIf TypeOf seg Is ArcSegment Then
                    Dim a = DirectCast(seg, ArcSegment)

                    Dim ptsX As PointCollection = SampleArcPoints(curLocal, a, m, FLATTENING_TOLERANCE)

                    If ptsX Is Nothing Then
                        newFig.Segments.Add(New LineSegment(m.Transform(a.Point), a.IsStroked))
                    Else
                        newFig.Segments.Add(New PolyLineSegment(ptsX, a.IsStroked))
                    End If

                    curLocal = a.Point


                Else
                    Dim tmp = PathGeometry.CreateFromGeometry(pg)
                    tmp.Transform = New MatrixTransform(m)
                    Return tmp.GetFlattenedPathGeometry(FLATTENING_TOLERANCE, ToleranceType.Absolute)
                End If


            Next


            result.Figures.Add(newFig)
        Next

        Return result
    End Function



    Private Function SampleArcPoints(startLocal As Point, arc As ArcSegment, m As Matrix, Optional tolerance As Double = 0.05) As PointCollection

        If tolerance <= 0 Then tolerance = 0.05

        Dim rx = Math.Abs(arc.Size.Width)
        Dim ry = Math.Abs(arc.Size.Height)
        If rx <= 0 OrElse ry <= 0 Then Return Nothing
        If startLocal = arc.Point Then Return Nothing

        ' Build geometry containing only this arc. IsFilled is needed otherwise GetFlattenedPathGeometry ignores it.
        ' ASK ME HOW LONG IT TOOK TO FIGURE THIS OUT.
        Dim fig As New PathFigure With {.StartPoint = startLocal, .IsClosed = False, .IsFilled = True}
        fig.Segments.Add(arc)

        Dim geom As New PathGeometry(New PathFigure() {fig})
        Dim flat As PathGeometry = geom.GetFlattenedPathGeometry(tolerance, ToleranceType.Absolute)

        ' Extract flattened points (excluding start point)
        Dim pts As New PointCollection()
        Dim startX As Point = m.Transform(startLocal)

        For Each f As PathFigure In flat.Figures
            For Each s As PathSegment In f.Segments
                Dim ls = TryCast(s, LineSegment)
                If ls IsNot Nothing Then
                    Dim p As Point = m.Transform(ls.Point)
                    If p <> startX Then pts.Add(p)
                    Continue For
                End If

                Dim ps = TryCast(s, PolyLineSegment)
                If ps IsNot Nothing Then
                    For Each lp As Point In ps.Points
                        Dim p As Point = m.Transform(lp)
                        If p <> startX Then pts.Add(p)
                    Next
                End If
            Next
        Next

        Return If(pts.Count = 0, Nothing, pts)
    End Function


    Private Function TransformAndFlattenGeometry(geometry As Geometry, matrix As Matrix, Optional tolerance As Double = 0) As PathGeometry
        Dim pathGeom As PathGeometry
        If TypeOf geometry Is PathGeometry Then
            pathGeom = CType(geometry, PathGeometry)
        Else
            pathGeom = PathGeometry.CreateFromGeometry(geometry)
        End If

        pathGeom.Transform = New MatrixTransform(matrix)
        Return If(tolerance > 0, pathGeom.GetFlattenedPathGeometry(tolerance, ToleranceType.Absolute), pathGeom.GetFlattenedPathGeometry())
    End Function

    Private Sub ProcessElement(elem As SvgElement, parentMatrix As Matrix, results As List(Of IDrawable), svgDoc As SvgDocument)
        Dim currentMatrix As Matrix = parentMatrix
        currentMatrix = ApplySvgTransforms(elem, currentMatrix)

        If TypeOf elem Is SvgGroup Then
            Dim group = CType(elem, SvgGroup)
            For Each child In group.Children
                ProcessElement(child, currentMatrix, results, svgDoc)
            Next
        ElseIf TypeOf elem Is SvgPath Then
            Dim drawable = ConvertPath(CType(elem, SvgPath), currentMatrix, svgDoc)
            If drawable IsNot Nothing Then results.Add(drawable)
        ElseIf TypeOf elem Is SvgRectangle Then
            Dim drawable = ConvertRectangle(CType(elem, SvgRectangle), currentMatrix, svgDoc)
            If drawable IsNot Nothing Then results.Add(drawable)
        ElseIf TypeOf elem Is SvgEllipse Then
            Dim drawable = ConvertEllipse(CType(elem, SvgEllipse), currentMatrix, svgDoc)
            If drawable IsNot Nothing Then results.Add(drawable)
        ElseIf TypeOf elem Is SvgCircle Then
            Dim drawable = ConvertCircle(CType(elem, SvgCircle), currentMatrix, svgDoc)
            If drawable IsNot Nothing Then results.Add(drawable)
        ElseIf TypeOf elem Is SvgLine Then
            Dim drawable = ConvertLine(CType(elem, SvgLine), currentMatrix)
            If drawable IsNot Nothing Then results.Add(drawable)
        ElseIf TypeOf elem Is SvgText Then
            Dim drawable = ConvertText(CType(elem, SvgText), currentMatrix, svgDoc)
            If drawable IsNot Nothing Then results.Add(drawable)
        ElseIf TypeOf elem Is SvgPolyline Then
            Dim drawable = ConvertPolyline(CType(elem, SvgPolyline), currentMatrix, svgDoc)
            If drawable IsNot Nothing Then results.Add(drawable)
        ElseIf TypeOf elem Is SvgPolygon Then
            Dim drawable = ConvertPolygon(CType(elem, SvgPolygon), currentMatrix, svgDoc)
            If drawable IsNot Nothing Then results.Add(drawable)
        End If
    End Sub

    Private Function GetClipPathGeometry(svgDoc As SvgDocument, clipPathUri As Uri, matrix As Matrix) As Geometry
        If clipPathUri Is Nothing Then Return Nothing

        Try
            Dim clipPathId = clipPathUri.ToString().TrimStart("#"c)
            Dim clipPathElement = svgDoc.GetElementById(clipPathId)

            If clipPathElement Is Nothing OrElse Not TypeOf clipPathElement Is SvgClipPath Then Return Nothing

            Dim clipPath = CType(clipPathElement, SvgClipPath)
            Dim combinedGeometry As Geometry = Nothing

            For Each child In clipPath.Children
                Dim childGeometry = ExtractGeometryFromElement(child)

                If childGeometry IsNot Nothing Then
                    ' Apply any transforms on the clip path child element
                    Dim childMatrix = ApplySvgTransforms(child, matrix)
                    If Not childMatrix.IsIdentity Then
                        ' Transform and flatten the geometry so the transform is "baked in"
                        ' This is necessary for Geometry.Combine to work correctly
                        childGeometry = TransformAndFlattenGeometry(childGeometry, childMatrix, FLATTENING_TOLERANCE)
                    End If

                    If combinedGeometry Is Nothing Then
                        combinedGeometry = childGeometry
                    Else
                        combinedGeometry = Geometry.Combine(combinedGeometry, childGeometry, GeometryCombineMode.Union, Nothing)
                    End If
                End If
            Next

            Return combinedGeometry
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    Private Function ExtractGeometryFromElement(element As SvgElement) As Geometry
        Try
            If TypeOf element Is SvgPath Then
                Dim svgPath = CType(element, SvgPath)
                Return Geometry.Parse(svgPath.PathData.ToString())
            ElseIf TypeOf element Is SvgRectangle Then
                Dim svgRect = CType(element, SvgRectangle)
                Return New RectangleGeometry(New Rect(svgRect.X, svgRect.Y, svgRect.Width, svgRect.Height))
            ElseIf TypeOf element Is SvgCircle Then
                Dim svgCircle = CType(element, SvgCircle)
                Return New EllipseGeometry(New Point(svgCircle.CenterX, svgCircle.CenterY), svgCircle.Radius, svgCircle.Radius)
            ElseIf TypeOf element Is SvgEllipse Then
                Dim svgEllipse = CType(element, SvgEllipse)
                Return New EllipseGeometry(New Point(svgEllipse.CenterX, svgEllipse.CenterY), svgEllipse.RadiusX, svgEllipse.RadiusY)
            ElseIf TypeOf element Is SvgPolygon Then
                Dim svgPolygon = CType(element, SvgPolygon)
                Return CreatePathGeometryFromPoints(svgPolygon.Points, True)
            ElseIf TypeOf element Is SvgPolyline Then
                Dim svgPolyline = CType(element, SvgPolyline)
                Return CreatePathGeometryFromPoints(svgPolyline.Points, False)
            ElseIf TypeOf element Is SvgLine Then
                Dim svgLine = CType(element, SvgLine)
                Dim pathGeom As New PathGeometry()
                Dim figure As New PathFigure() With {
                    .StartPoint = New Point(svgLine.StartX, svgLine.StartY),
                    .IsClosed = False
                }
                figure.Segments.Add(New LineSegment(New Point(svgLine.EndX, svgLine.EndY), True))
                pathGeom.Figures.Add(figure)
                Return pathGeom
            ElseIf TypeOf element Is SvgText Then
                ' Convert text to path geometry for clipping
                Dim svgText = CType(element, SvgText)
                Return BuildTextPathGeometry(svgText)
            End If
        Catch ex As Exception
            Return Nothing
        End Try
        Return Nothing
    End Function

    Private Function CreatePathGeometryFromPoints(points As SvgPointCollection, isClosed As Boolean) As Geometry
        If points Is Nothing OrElse points.Count < 2 Then Return Nothing

        Dim pathGeom As New PathGeometry()
        Dim figure As New PathFigure() With {
            .StartPoint = New Point(points(0), points(1)),
            .IsClosed = isClosed
        }

        For i As Integer = 2 To points.Count - 1 Step 2
            If i + 1 < points.Count Then
                figure.Segments.Add(New LineSegment(New Point(points(i), points(i + 1)), True))
            End If
        Next

        pathGeom.Figures.Add(figure)
        Return pathGeom
    End Function


    Private Function ApplyClipPath(geometry As Geometry, clipGeometry As Geometry) As Geometry
        If clipGeometry Is Nothing Then Return geometry
        Try
            Return Geometry.Combine(geometry, clipGeometry, GeometryCombineMode.Intersect, Nothing)
        Catch ex As Exception
            Return geometry
        End Try
    End Function

    Private Function ApplySvgTransforms(elem As SvgElement, parentAccumulated As Matrix) As Matrix
        If elem.Transforms Is Nothing OrElse elem.Transforms.Count = 0 Then Return parentAccumulated

        ' Build the element's local transform from identity
        Dim local As Matrix = Matrix.Identity

        For Each tr In elem.Transforms
            Dim tm As Matrix = Matrix.Identity

            If TypeOf tr Is SvgMatrix Then
                Dim m = DirectCast(tr, SvgMatrix)
                tm = New Matrix(m.Points(0), m.Points(1), m.Points(2), m.Points(3), m.Points(4), m.Points(5))

            ElseIf TypeOf tr Is SvgTranslate Then
                Dim t = DirectCast(tr, SvgTranslate)
                tm.Translate(t.X, t.Y)

            ElseIf TypeOf tr Is SvgScale Then
                Dim s = DirectCast(tr, SvgScale)
                tm.Scale(s.X, s.Y)

            ElseIf TypeOf tr Is SvgRotate Then
                Dim r = DirectCast(tr, SvgRotate)
                If r.CenterX <> 0 OrElse r.CenterY <> 0 Then
                    tm.RotateAt(r.Angle, r.CenterX, r.CenterY)
                Else
                    tm.Rotate(r.Angle)
                End If

            End If

            ' SVG transform list applies left-to-right
            local = Matrix.Multiply(tm, local)
        Next

        ' IMPORTANT: child(local) first, then parent group/doc
        Return Matrix.Multiply(local, parentAccumulated)
    End Function


    Private Function IsValidBounds(r As Rect, Optional requireNonZero As Boolean = True) As Boolean
        Dim notEmpty = Not r.IsEmpty
        Dim notNaN = Not (Double.IsNaN(r.X) OrElse Double.IsNaN(r.Y) OrElse Double.IsNaN(r.Width) OrElse Double.IsNaN(r.Height))
        If requireNonZero Then
            Return notEmpty AndAlso notNaN AndAlso r.Width > 0 AndAlso r.Height > 0
        End If
        Return notEmpty AndAlso notNaN
    End Function


    Private Function FinaliseDrawableElement(fe As FrameworkElement, boundsWorld As Rect, svgVisual As SvgVisualElement, matrixForStroke As Matrix, elementId As String) As IDrawable

        If fe Is Nothing Then Return Nothing
        If Not IsValidBounds(boundsWorld, requireNonZero:=False) Then Return Nothing

        Dim strokeWidthValue As Single = GetStrokeWidthOrZero(svgVisual)
        Dim dims = CalculateStrokeDimensions(boundsWorld, strokeWidthValue, matrixForStroke)

        Return FinaliseDrawableElement(fe, boundsWorld, svgVisual, elementId, dims)

    End Function

    Private Function FinaliseDrawableElement(ByRef fe As FrameworkElement, boundsWorld As Rect, svgVisual As SvgVisualElement, elementId As String, dims As (totalWidth As Double, totalHeight As Double, strokeOffset As Double, transformedStroke As Double)) As IDrawable
        Dim shp As Shape = TryCast(fe, Shape)
        If shp IsNot Nothing Then
            ApplyStrokeAndFill(shp, svgVisual, dims.transformedStroke)
        End If

        fe.Width = dims.totalWidth
        fe.Height = dims.totalHeight
        Canvas.SetLeft(fe, boundsWorld.X - dims.strokeOffset)
        Canvas.SetTop(fe, boundsWorld.Y - dims.strokeOffset)

        Dim drawable As IDrawable = BaseDrawable.DrawableFactory(fe)
        AssignDrawableName(drawable, elementId)
        Return drawable
    End Function


    Private Function GetStrokeWidthOrZero(svg As SvgVisualElement) As Single
        Try
            If svg Is Nothing OrElse svg.StrokeWidth = Nothing Then Return 0.0F
            Return svg.StrokeWidth.Value
        Catch
            Return 0.0F
        End Try
    End Function

    Private Function CreateBakedPathDrawableFromGeometry(srcGeometry As Geometry, matrix As Matrix, svgElement As SvgVisualElement, elementId As String) As IDrawable


        Dim flattened As PathGeometry = TransformAndFlattenGeometry(srcGeometry, matrix, FLATTENING_TOLERANCE)
        Dim bounds As Rect = flattened.Bounds
        If bounds.IsEmpty OrElse bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return Nothing

        Dim translated = flattened.Clone()
        translated.Transform = New TranslateTransform(-bounds.X, -bounds.Y)
        translated = translated.GetFlattenedPathGeometry(FLATTENING_TOLERANCE, ToleranceType.Absolute)

        Dim wpfPath As New Shapes.Path With {
            .Data = translated,
            .Stretch = Stretch.None
        }

        Return FinaliseDrawableElement(wpfPath, bounds, svgElement, matrix, elementId)
    End Function

    Private Function IsAxisAlignedScaleTranslate(m As Matrix, Optional eps As Double = 0.0000001) As Boolean
        ' No rotation/shear. Allows non-uniform scale + translate.
        Return Math.Abs(m.M12) <= eps AndAlso Math.Abs(m.M21) <= eps
    End Function



    Private Function BakeClipGeometry(ByRef geometry As Geometry, svgElement As SvgVisualElement, svgDoc As SvgDocument) As Boolean
        If svgElement.ClipPath IsNot Nothing Then
            Dim clipGeometry = GetClipPathGeometry(svgDoc, svgElement.ClipPath, Matrix.Identity)
            If clipGeometry IsNot Nothing Then
                geometry = ApplyClipPath(geometry, clipGeometry)
                Return True
            End If
        End If
        Return False
    End Function


    Private Function GeneratePathFromFlattened(flattenedGeometry As PathGeometry, bounds As Rect) As Shapes.Path

        Dim translatedGeometry = flattenedGeometry.Clone()
        translatedGeometry.Transform = New TranslateTransform(-bounds.X, -bounds.Y)
        translatedGeometry = translatedGeometry.GetFlattenedPathGeometry(FLATTENING_TOLERANCE, ToleranceType.Absolute)

        Dim wpfPath As New Shapes.Path With {
            .Data = translatedGeometry,
            .Stretch = Stretch.None
        }
        Return wpfPath

    End Function



    Private Function ConvertPath(svgPath As SvgPath, matrix As Matrix, svgDoc As SvgDocument) As IDrawable
        Try

            Dim geometry As Geometry = Geometry.Parse(svgPath.PathData.ToString())
            Dim wasClipped As Boolean = BakeClipGeometry(geometry, svgPath, svgDoc)


            Dim pg As PathGeometry = If(TypeOf geometry Is PathGeometry, DirectCast(geometry, PathGeometry), PathGeometry.CreateFromGeometry(geometry))
            pg = pg.Clone() 'cursed bullshit

            'Bake the SVG transform matrix into the geometry POINTS
            Dim transformed As PathGeometry = TransformPathGeometryPreserveOpenClosed(pg, matrix)

            'Bounds in canvas coords (post-transform)
            Dim bounds As Rect = transformed.Bounds
            If Not IsValidBounds(bounds) Then Return Nothing
            If transformed.Figures Is Nothing OrElse transformed.Figures.Count = 0 Then Return Nothing

            'Normalize to local (0,0) so Canvas.Left/Top is the world position
            Dim t As Matrix = Matrix.Identity
            t.Translate(-bounds.X, -bounds.Y)
            Dim normalized As PathGeometry = TransformPathGeometryPreserveOpenClosed(transformed, t)

            Dim wpfPath As New Shapes.Path With {
                .Data = normalized,
                .Stretch = Stretch.None
            }

            Return FinaliseDrawableElement(wpfPath, bounds, svgPath, matrix, svgPath.ID)


        Catch ex As Exception
            Return Nothing
        End Try
    End Function



    Private Function ConvertRectangle(svgRect As SvgRectangle, matrix As Matrix, svgDoc As SvgDocument) As IDrawable
        Try
            Dim rectGeometry As Geometry = New RectangleGeometry(New Rect(svgRect.X, svgRect.Y, svgRect.Width, svgRect.Height))
            Dim wasClipped As Boolean = BakeClipGeometry(rectGeometry, svgRect, svgDoc)

            If Not IsAxisAlignedScaleTranslate(matrix) Then Return CreateBakedPathDrawableFromGeometry(rectGeometry, matrix, svgRect, svgRect.ID)

            Dim flattenedGeometry = TransformAndFlattenGeometry(rectGeometry, matrix)
            If flattenedGeometry Is Nothing OrElse flattenedGeometry.Figures.Count = 0 Then Return Nothing
            Dim bounds = flattenedGeometry.Bounds
            If Not IsValidBounds(bounds) Then Return Nothing

            Dim shp = If(wasClipped, GeneratePathFromFlattened(flattenedGeometry, bounds), New Rectangle())

            Return FinaliseDrawableElement(shp, bounds, svgRect, matrix, svgRect.ID)

        Catch ex As Exception
            Return Nothing
        End Try
    End Function



    Private Function ConvertEllipse(svgEllipse As SvgEllipse, matrix As Matrix, svgDoc As SvgDocument) As IDrawable
        Try
            Dim ellipseGeometry As Geometry = New EllipseGeometry(New Point(svgEllipse.CenterX, svgEllipse.CenterY), svgEllipse.RadiusX, svgEllipse.RadiusY)
            Dim wasClipped As Boolean = BakeClipGeometry(ellipseGeometry, svgEllipse, svgDoc)


            If Not IsAxisAlignedScaleTranslate(matrix) Then
                Return CreateBakedPathDrawableFromGeometry(ellipseGeometry, matrix, svgEllipse, svgEllipse.ID)
            End If

            Dim flattenedGeometry = TransformAndFlattenGeometry(ellipseGeometry, matrix)
            If flattenedGeometry Is Nothing OrElse flattenedGeometry.Figures.Count = 0 Then Return Nothing
            Dim bounds = flattenedGeometry.Bounds
            If Not IsValidBounds(bounds) Then Return Nothing

            Dim shp = If(wasClipped, GeneratePathFromFlattened(flattenedGeometry, bounds), New Ellipse())

            Return FinaliseDrawableElement(shp, bounds, svgEllipse, matrix, svgEllipse.ID)

        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    Private Function ConvertCircle(svgCircle As SvgCircle, matrix As Matrix, svgDoc As SvgDocument) As IDrawable
        Try
            Dim ellipseGeometry As Geometry = New EllipseGeometry(New Point(svgCircle.CenterX, svgCircle.CenterY), svgCircle.Radius, svgCircle.Radius)
            Dim wasClipped As Boolean = BakeClipGeometry(ellipseGeometry, svgCircle, svgDoc)

            Dim flattenedGeometry = TransformAndFlattenGeometry(ellipseGeometry, matrix)
            Dim bounds = flattenedGeometry.Bounds
            If Not IsValidBounds(bounds) Then Return Nothing

            Dim shp As Shape = If(wasClipped, GeneratePathFromFlattened(flattenedGeometry, bounds), New Ellipse())

            Return FinaliseDrawableElement(shp, bounds, svgCircle, matrix, svgCircle.ID)

        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    Private Function ConvertPolygon(svgPolygon As SvgPolygon, matrix As Matrix, svgDoc As SvgDocument) As IDrawable
        Try
            Dim polygonGeometry As Geometry = CreatePathGeometryFromPoints(svgPolygon.Points, True)
            Dim wasClipped As Boolean = BakeClipGeometry(polygonGeometry, svgPolygon, svgDoc)

            ' Polygons always need to be converted to paths since they're defined by points
            Dim flattenedGeometry = TransformAndFlattenGeometry(polygonGeometry, matrix, FLATTENING_TOLERANCE)
            Dim bounds = flattenedGeometry.Bounds
            If Not IsValidBounds(bounds) Then Return Nothing

            Dim flattenedPath = GeneratePathFromFlattened(flattenedGeometry, bounds)

            Return FinaliseDrawableElement(flattenedPath, bounds, svgPolygon, matrix, svgPolygon.ID)

        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    Private Function ConvertPolyline(svgPolyline As SvgPolyline, matrix As Matrix, svgDoc As SvgDocument) As IDrawable
        Try
            Dim polylineGeometry As Geometry = CreatePathGeometryFromPoints(svgPolyline.Points, False)
            If polylineGeometry Is Nothing Then Return Nothing

            Dim wasClipped As Boolean = BakeClipGeometry(polylineGeometry, svgPolyline, svgDoc)
            Dim pathGeom As PathGeometry = PathGeometry.CreateFromGeometry(polylineGeometry)

            Dim transformedGeometry = TransformPathGeometryPreserveOpenClosed(pathGeom, matrix)
            Dim bounds = transformedGeometry.Bounds
            If Not IsValidBounds(bounds) Then Return Nothing

            ' Normalize to local (0,0)
            Dim t As Matrix = Matrix.Identity
            t.Translate(-bounds.X, -bounds.Y)
            Dim normalized = TransformPathGeometryPreserveOpenClosed(transformedGeometry, t)

            Dim wpfPath As New Shapes.Path With {
                .Data = normalized,
                .Stretch = Stretch.None
            }

            Return FinaliseDrawableElement(wpfPath, bounds, svgPolyline, matrix, svgPolyline.ID)

        Catch ex As Exception
            Return Nothing
        End Try
    End Function


    Private Function ConvertLine(svgLine As SvgLine, matrix As Matrix) As IDrawable
        Try
            ' Transform endpoints into world space
            Dim p1 As Point = matrix.Transform(New Point(svgLine.StartX, svgLine.StartY))
            Dim p2 As Point = matrix.Transform(New Point(svgLine.EndX, svgLine.EndY))

            'World-space bounds of the (unstroked) segment
            Dim minX As Double = Math.Min(p1.X, p2.X)
            Dim minY As Double = Math.Min(p1.Y, p2.Y)
            Dim maxX As Double = Math.Max(p1.X, p2.X)
            Dim maxY As Double = Math.Max(p1.Y, p2.Y)

            Dim bounds As New Rect(minX, minY, maxX - minX, maxY - minY)
            If Not IsValidBounds(bounds) Then Return Nothing

            Dim strokeWidthValue As Single = GetStrokeWidthOrZero(svgLine)
            Dim dims = CalculateStrokeDimensions(bounds, strokeWidthValue, matrix)
            Dim strokeOffset As Double = dims.strokeOffset

            'Normalize endpoints into LOCAL wrapper space
            '    (so the wrapper measures correctly and nothing drsws outside it)
            Dim localP1 As New Point((p1.X - bounds.X) + strokeOffset, (p1.Y - bounds.Y) + strokeOffset)
            Dim localP2 As New Point((p2.X - bounds.X) + strokeOffset, (p2.Y - bounds.Y) + strokeOffset)

            Dim wpfLine As New Line With {
                .X1 = localP1.X,
                .Y1 = localP1.Y,
                .X2 = localP2.X,
                .Y2 = localP2.Y,
                .Width = dims.totalWidth,
                .Height = dims.totalHeight
            }

            Return FinaliseDrawableElement(wpfLine, bounds, svgLine, svgLine.ID, dims)

        Catch ex As Exception
            Return Nothing
        End Try
    End Function


    Private Function ConvertText(svgText As SvgText, matrix As Matrix, svgDoc As SvgDocument) As IDrawable
        Try
            Dim text As String = ExtractTextContent(svgText)
            If String.IsNullOrEmpty(text) Then Return Nothing

            Dim pathGeometry = BuildTextPathGeometry(svgText)
            If pathGeometry Is Nothing Then Return Nothing

            Dim wasClipped As Boolean = BakeClipGeometry(pathGeometry, svgText, svgDoc)


            Dim flattenedGeometry = TransformAndFlattenGeometry(pathGeometry, matrix, FLATTENING_TOLERANCE)
            Dim bounds = flattenedGeometry.Bounds

            If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return Nothing

            Dim wpfPath = GeneratePathFromFlattened(flattenedGeometry, bounds)

            Return FinaliseDrawableElement(wpfPath, bounds, svgText, matrix, svgText.ID)

        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    Private Function BuildTextPathGeometry(svgText As SvgText) As PathGeometry
        Dim pathData = svgText.Path(Nothing).PathData
        Dim points = pathData.Points.ToList()
        Dim types = pathData.Types.ToList()

        For Each child In svgText.Children
            Dim tBase = TryCast(child, SvgTextBase)
            If tBase IsNot Nothing Then
                Dim tBasePath = tBase.Path(Nothing).PathData
                points.AddRange(tBasePath.Points)
                types.AddRange(tBasePath.Types)
            End If
        Next

        If points.Count = 0 Then Return Nothing

        Dim pathGeometry As New PathGeometry()
        Dim currentFigure As PathFigure = Nothing

        For i As Integer = 0 To points.Count - 1
            Dim point = points(i)
            Dim pointType = CType(types(i), System.Drawing.Drawing2D.PathPointType)

            If pointType = System.Drawing.Drawing2D.PathPointType.Start OrElse currentFigure Is Nothing Then
                currentFigure = New PathFigure() With {.StartPoint = New Point(point.X, point.Y), .IsClosed = False}
                pathGeometry.Figures.Add(currentFigure)
            ElseIf (pointType And System.Drawing.Drawing2D.PathPointType.Line) = System.Drawing.Drawing2D.PathPointType.Line Then
                currentFigure.Segments.Add(New LineSegment(New Point(point.X, point.Y), True))
            ElseIf (pointType And System.Drawing.Drawing2D.PathPointType.Bezier) = System.Drawing.Drawing2D.PathPointType.Bezier Then
                If i + 2 < points.Count Then
                    currentFigure.Segments.Add(New BezierSegment(
                        New Point(points(i).X, points(i).Y),
                        New Point(points(i + 1).X, points(i + 1).Y),
                        New Point(points(i + 2).X, points(i + 2).Y), True))
                    i += 2
                End If
            End If

            If (pointType And System.Drawing.Drawing2D.PathPointType.CloseSubpath) = System.Drawing.Drawing2D.PathPointType.CloseSubpath Then
                currentFigure.IsClosed = True
            End If
        Next

        Return pathGeometry
    End Function

    Private Function ExtractTextContent(svgText As SvgText) As String
        Dim sb As New System.Text.StringBuilder()

        If Not String.IsNullOrEmpty(svgText.Text) Then
            sb.Append(svgText.Text)
        End If

        If svgText.Children IsNot Nothing Then
            For Each child In svgText.Children
                If TypeOf child Is SvgTextSpan Then
                    Dim span = CType(child, SvgTextSpan)
                    If Not String.IsNullOrEmpty(span.Text) Then
                        sb.Append(span.Text)
                    End If
                ElseIf TypeOf child Is SvgText Then
                    Dim nestedText = CType(child, SvgText)
                    sb.Append(ExtractTextContent(nestedText))
                End If
            Next
        End If

        Dim result = sb.ToString().Trim()
        Return result
    End Function

    Private Sub ApplyStrokeAndFill(shape As Shape, svgElement As SvgVisualElement, transformedStrokeThickness As Double)
        If svgElement.Fill IsNot Nothing Then
            If svgElement.Fill.ToString() = "none" OrElse svgElement.Fill.GetType().Name.Contains("None") Then
                shape.Fill = Nothing
            ElseIf TypeOf svgElement.Fill Is SvgColourServer Then
                Dim color = CType(svgElement.Fill, SvgColourServer).Colour
                shape.Fill = New SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B))
            Else
                shape.Fill = Brushes.Transparent
            End If
        Else
            shape.Fill = Brushes.Transparent
        End If

        If svgElement.Stroke IsNot Nothing AndAlso TypeOf svgElement.Stroke Is SvgColourServer Then

            If svgElement.Stroke.ToString() = "none" OrElse svgElement.Stroke.GetType().Name.Contains("None") Then
                shape.Stroke = Nothing
                shape.StrokeThickness = 0
            Else
                Dim color = CType(svgElement.Stroke, SvgColourServer).Colour
                shape.Stroke = New SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B))
                shape.StrokeThickness = If(transformedStrokeThickness > 0, transformedStrokeThickness, 1)
            End If

        Else
            shape.Stroke = Nothing
            shape.StrokeThickness = 0
        End If

        Select Case svgElement.StrokeLineCap
            Case SvgStrokeLineCap.Round
                shape.StrokeStartLineCap = PenLineCap.Round
                shape.StrokeEndLineCap = PenLineCap.Round
            Case SvgStrokeLineCap.Square
                shape.StrokeStartLineCap = PenLineCap.Square
                shape.StrokeEndLineCap = PenLineCap.Square
            Case Else
                shape.StrokeStartLineCap = PenLineCap.Flat
                shape.StrokeEndLineCap = PenLineCap.Flat
        End Select

        Select Case svgElement.StrokeLineJoin
            Case SvgStrokeLineJoin.Round
                shape.StrokeLineJoin = PenLineJoin.Round
            Case SvgStrokeLineJoin.Bevel
                shape.StrokeLineJoin = PenLineJoin.Bevel
            Case Else
                shape.StrokeLineJoin = PenLineJoin.Miter
        End Select
    End Sub

    Private Function ConvertToMM(value As Single, unitType As SvgUnitType) As Double
        Return value * ConvertSVGScaleToMM(unitType)
    End Function

    Public Shared Function ConvertSVGScaleToMM(unitType As Svg.SvgUnitType) As Double
        Select Case unitType
            Case Svg.SvgUnitType.Centimeter
                Return 10
            Case Svg.SvgUnitType.Inch
                Return 25.4
            Case Svg.SvgUnitType.Millimeter
                Return 1
            Case Svg.SvgUnitType.Pixel
                Return 0.264583333333333
            Case Svg.SvgUnitType.Percentage
                Return 0.264583333333333
            Case Svg.SvgUnitType.Point
                Return 0.352777777777778
            Case Svg.SvgUnitType.Pica
                Return 4.23333333333333
            Case Else
                Application.GetService(Of SnackbarService).GenerateCaution("Unknown SVG Unit Type: " & unitType.ToString, "Scaling may not be correct")
                Return 0.264583333333333
        End Select
    End Function

    Public Shared Function SVGDocumentToString(svgdocument As SvgDocument) As String
        Using sw As New StringWriter()
            Using writer As XmlWriter = XmlWriter.Create(sw, New XmlWriterSettings With {.Encoding = Text.Encoding.UTF8})
                svgdocument.Write(writer)
            End Using
            Return sw.ToString()
        End Using
    End Function


End Class
