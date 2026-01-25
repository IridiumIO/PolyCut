

Imports System.IO
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
                Return CreatePolygonGeometry(svgPolygon.Points)
            ElseIf TypeOf element Is SvgPolyline Then
                Dim svgPolyline = CType(element, SvgPolyline)
                Return CreatePolylineGeometry(svgPolyline.Points)
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

    Private Function CreatePolygonGeometry(points As SvgPointCollection) As Geometry
        If points Is Nothing OrElse points.Count < 2 Then Return Nothing

        Dim pathGeom As New PathGeometry()
        Dim figure As New PathFigure() With {
            .StartPoint = New Point(points(0), points(1)),
            .IsClosed = True
        }

        For i As Integer = 2 To points.Count - 1 Step 2
            If i + 1 < points.Count Then
                figure.Segments.Add(New LineSegment(New Point(points(i), points(i + 1)), True))
            End If
        Next

        pathGeom.Figures.Add(figure)
        Return pathGeom
    End Function

    Private Function CreatePolylineGeometry(points As SvgPointCollection) As Geometry
        If points Is Nothing OrElse points.Count < 2 Then Return Nothing

        Dim pathGeom As New PathGeometry()
        Dim figure As New PathFigure() With {
            .StartPoint = New Point(points(0), points(1)),
            .IsClosed = False
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
            local = Matrix.Multiply(local, tm)
        Next

        ' IMPORTANT: child(local) first, then parent group/doc
        Return Matrix.Multiply(local, parentAccumulated)
    End Function

    Private Function ConvertPath(svgPath As SvgPath, matrix As Matrix, svgDoc As SvgDocument) As IDrawable
        Try
            Dim geometry As Geometry = Geometry.Parse(svgPath.PathData.ToString())

            ' Apply clipping path if present (in SVG coordinate space, before transform)
            If svgPath.ClipPath IsNot Nothing Then
                Dim clipGeometry = GetClipPathGeometry(svgDoc, svgPath.ClipPath, Matrix.Identity)
                If clipGeometry IsNot Nothing Then
                    geometry = ApplyClipPath(geometry, clipGeometry)
                End If
            End If

            Dim flattenedGeometry = TransformAndFlattenGeometry(geometry, matrix, FLATTENING_TOLERANCE)
            Dim bounds = flattenedGeometry.Bounds

            ' Reject geometries with invalid bounds (NaN / Infinite) or explicitly empty
            If Double.IsNaN(bounds.X) OrElse Double.IsNaN(bounds.Y) OrElse Double.IsNaN(bounds.Width) OrElse Double.IsNaN(bounds.Height) Then
                Return Nothing
            End If
            If flattenedGeometry.IsEmpty() Then
                Return Nothing
            End If

            Dim strokeWidthValue As Single = 0

            Try
                If svgPath.StrokeWidth <> Nothing Then strokeWidthValue = svgPath.StrokeWidth.Value
            Catch
                strokeWidthValue = 0
            End Try

            ' Use stroke-inclusive dimensions for the emptiness check so stroked lines are kept
            Dim dimensions = CalculateStrokeDimensions(bounds, strokeWidthValue, matrix)
            If dimensions.totalWidth <= 0 OrElse dimensions.totalHeight <= 0 Then
                ' If stroke didn't produce area, allow if geometry actually contains segments (e.g. lines)
                If flattenedGeometry.Figures Is Nothing OrElse flattenedGeometry.Figures.Count = 0 Then
                    Return Nothing
                End If
            End If

            Dim translatedGeometry = flattenedGeometry.Clone()
            translatedGeometry.Transform = New TranslateTransform(-bounds.X, -bounds.Y)
            translatedGeometry = translatedGeometry.GetFlattenedPathGeometry(FLATTENING_TOLERANCE, ToleranceType.Absolute)


            Dim wpfPath As New Shapes.Path With {
                .Data = translatedGeometry,
                .Stretch = Stretch.None,
                .Width = dimensions.totalWidth,
                .Height = dimensions.totalHeight
            }

            ApplyStrokeAndFill(wpfPath, svgPath, dimensions.transformedStroke)
            Canvas.SetLeft(wpfPath, bounds.X - dimensions.strokeOffset)
            Canvas.SetTop(wpfPath, bounds.Y - dimensions.strokeOffset)

            Dim drawable As New DrawablePath(wpfPath)
            AssignDrawableName(drawable, svgPath.ID)
            Return drawable
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    Private Function ConvertRectangle(svgRect As SvgRectangle, matrix As Matrix, svgDoc As SvgDocument) As IDrawable
        Try
            Dim rectGeometry As Geometry = New RectangleGeometry(New Rect(svgRect.X, svgRect.Y, svgRect.Width, svgRect.Height))
            Dim wasClipped As Boolean = False

            ' Apply clipping path if present
            If svgRect.ClipPath IsNot Nothing Then
                Dim clipGeometry = GetClipPathGeometry(svgDoc, svgRect.ClipPath, Matrix.Identity)
                If clipGeometry IsNot Nothing Then
                    rectGeometry = ApplyClipPath(rectGeometry, clipGeometry)
                    wasClipped = True
                End If
            End If

            Dim flattenedGeometry = TransformAndFlattenGeometry(rectGeometry, matrix)
            Dim bounds = flattenedGeometry.Bounds

            If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return Nothing

            Dim dimensions = CalculateStrokeDimensions(bounds, svgRect.StrokeWidth.Value, matrix)

            ' If clipped, return as Path instead of Rectangle
            If wasClipped Then
                Dim translatedGeometry = flattenedGeometry.Clone()
                translatedGeometry.Transform = New TranslateTransform(-bounds.X, -bounds.Y)
                translatedGeometry = translatedGeometry.GetFlattenedPathGeometry(FLATTENING_TOLERANCE, ToleranceType.Absolute)

                Dim wpfPath As New Shapes.Path With {
                    .Data = translatedGeometry,
                    .Stretch = Stretch.None,
                    .Width = dimensions.totalWidth,
                    .Height = dimensions.totalHeight
                }

                ApplyStrokeAndFill(wpfPath, svgRect, dimensions.transformedStroke)
                Canvas.SetLeft(wpfPath, bounds.X - dimensions.strokeOffset)
                Canvas.SetTop(wpfPath, bounds.Y - dimensions.strokeOffset)

                Dim drawable As New DrawablePath(wpfPath)
                AssignDrawableName(drawable, svgRect.ID)
                Return drawable
            Else
                Dim wpfRect As New Rectangle With {
                    .Width = dimensions.totalWidth,
                    .Height = dimensions.totalHeight
                }

                ApplyStrokeAndFill(wpfRect, svgRect, dimensions.transformedStroke)
                Canvas.SetLeft(wpfRect, bounds.X - dimensions.strokeOffset)
                Canvas.SetTop(wpfRect, bounds.Y - dimensions.strokeOffset)

                Dim drawable As New DrawableRectangle(wpfRect)
                AssignDrawableName(drawable, svgRect.ID)
                Return drawable
            End If
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    Private Function ConvertEllipse(svgEllipse As SvgEllipse, matrix As Matrix, svgDoc As SvgDocument) As IDrawable
        Try
            Dim ellipseGeometry As Geometry = New EllipseGeometry(New Point(svgEllipse.CenterX, svgEllipse.CenterY), svgEllipse.RadiusX, svgEllipse.RadiusY)
            Dim wasClipped As Boolean = False

            ' Apply clipping path if present
            If svgEllipse.ClipPath IsNot Nothing Then
                Dim clipGeometry = GetClipPathGeometry(svgDoc, svgEllipse.ClipPath, Matrix.Identity)
                If clipGeometry IsNot Nothing Then
                    ellipseGeometry = ApplyClipPath(ellipseGeometry, clipGeometry)
                    wasClipped = True
                End If
            End If

            Dim flattenedGeometry = TransformAndFlattenGeometry(ellipseGeometry, matrix)
            Dim bounds = flattenedGeometry.Bounds

            If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return Nothing

            Dim dimensions = CalculateStrokeDimensions(bounds, svgEllipse.StrokeWidth.Value, matrix)

            ' If clipped, return as Path instead of Ellipse
            If wasClipped Then
                Dim translatedGeometry = flattenedGeometry.Clone()
                translatedGeometry.Transform = New TranslateTransform(-bounds.X, -bounds.Y)
                translatedGeometry = translatedGeometry.GetFlattenedPathGeometry(FLATTENING_TOLERANCE, ToleranceType.Absolute)

                Dim wpfPath As New Shapes.Path With {
                    .Data = translatedGeometry,
                    .Stretch = Stretch.None,
                    .Width = dimensions.totalWidth,
                    .Height = dimensions.totalHeight
                }

                ApplyStrokeAndFill(wpfPath, svgEllipse, dimensions.transformedStroke)
                Canvas.SetLeft(wpfPath, bounds.X - dimensions.strokeOffset)
                Canvas.SetTop(wpfPath, bounds.Y - dimensions.strokeOffset)

                Dim drawable As New DrawablePath(wpfPath)
                AssignDrawableName(drawable, svgEllipse.ID)
                Return drawable
            Else
                Dim wpfEllipse As New Ellipse With {
                    .Width = dimensions.totalWidth,
                    .Height = dimensions.totalHeight
                }

                ApplyStrokeAndFill(wpfEllipse, svgEllipse, dimensions.transformedStroke)
                Canvas.SetLeft(wpfEllipse, bounds.X - dimensions.strokeOffset)
                Canvas.SetTop(wpfEllipse, bounds.Y - dimensions.strokeOffset)

                Dim drawable As New DrawableEllipse(wpfEllipse)
                AssignDrawableName(drawable, svgEllipse.ID)
                Return drawable
            End If
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    Private Function ConvertCircle(svgCircle As SvgCircle, matrix As Matrix, svgDoc As SvgDocument) As IDrawable
        Try
            Dim ellipseGeometry As Geometry = New EllipseGeometry(New Point(svgCircle.CenterX, svgCircle.CenterY), svgCircle.Radius, svgCircle.Radius)
            Dim wasClipped As Boolean = False

            ' Apply clipping path if present
            If svgCircle.ClipPath IsNot Nothing Then
                Dim clipGeometry = GetClipPathGeometry(svgDoc, svgCircle.ClipPath, Matrix.Identity)
                If clipGeometry IsNot Nothing Then
                    ellipseGeometry = ApplyClipPath(ellipseGeometry, clipGeometry)
                    wasClipped = True
                End If
            End If

            Dim flattenedGeometry = TransformAndFlattenGeometry(ellipseGeometry, matrix)
            Dim bounds = flattenedGeometry.Bounds

            If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return Nothing

            Dim dimensions = CalculateStrokeDimensions(bounds, svgCircle.StrokeWidth.Value, matrix)

            ' If clipped, return as Path instead of Ellipse
            If wasClipped Then
                Dim translatedGeometry = flattenedGeometry.Clone()
                translatedGeometry.Transform = New TranslateTransform(-bounds.X, -bounds.Y)
                translatedGeometry = translatedGeometry.GetFlattenedPathGeometry(FLATTENING_TOLERANCE, ToleranceType.Absolute)

                Dim wpfPath As New Shapes.Path With {
                    .Data = translatedGeometry,
                    .Stretch = Stretch.None,
                    .Width = dimensions.totalWidth,
                    .Height = dimensions.totalHeight
                }

                ApplyStrokeAndFill(wpfPath, svgCircle, dimensions.transformedStroke)
                Canvas.SetLeft(wpfPath, bounds.X - dimensions.strokeOffset)
                Canvas.SetTop(wpfPath, bounds.Y - dimensions.strokeOffset)

                Dim drawable As New DrawablePath(wpfPath)
                AssignDrawableName(drawable, svgCircle.ID)
                Return drawable
            Else
                Dim wpfEllipse As New Ellipse With {
                    .Width = dimensions.totalWidth,
                    .Height = dimensions.totalHeight
                }

                ApplyStrokeAndFill(wpfEllipse, svgCircle, dimensions.transformedStroke)
                Canvas.SetLeft(wpfEllipse, bounds.X - dimensions.strokeOffset)
                Canvas.SetTop(wpfEllipse, bounds.Y - dimensions.strokeOffset)

                Dim drawable As New DrawableEllipse(wpfEllipse)
                AssignDrawableName(drawable, svgCircle.ID)
                Return drawable
            End If
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    Private Function ConvertLine(svgLine As SvgLine, matrix As Matrix) As IDrawable
        Try
            Dim pt1 = matrix.Transform(New Point(svgLine.StartX, svgLine.StartY))
            Dim pt2 = matrix.Transform(New Point(svgLine.EndX, svgLine.EndY))

            Dim wpfLine As New Line With {
                .X1 = pt1.X,
                .Y1 = pt1.Y,
                .X2 = pt2.X,
                .Y2 = pt2.Y
            }

            ApplyStrokeAndFill(wpfLine, svgLine, 0)

            Dim drawable As New DrawableLine(wpfLine)
            AssignDrawableName(drawable, svgLine.ID)
            Return drawable
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

            ' Apply clipping path if present
            If svgText.ClipPath IsNot Nothing Then
                Dim clipGeometry = GetClipPathGeometry(svgDoc, svgText.ClipPath, Matrix.Identity)
                If clipGeometry IsNot Nothing Then
                    Dim textGeometry As Geometry = pathGeometry
                    textGeometry = ApplyClipPath(textGeometry, clipGeometry)
                    pathGeometry = If(TypeOf textGeometry Is PathGeometry, CType(textGeometry, PathGeometry), PathGeometry.CreateFromGeometry(textGeometry))
                End If
            End If


            Dim flattenedGeometry = TransformAndFlattenGeometry(pathGeometry, matrix, FLATTENING_TOLERANCE)
            Dim bounds = flattenedGeometry.Bounds

            If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return Nothing

            Dim translatedGeometry = flattenedGeometry.Clone()
            translatedGeometry.Transform = New TranslateTransform(-bounds.X, -bounds.Y)
            translatedGeometry = translatedGeometry.GetFlattenedPathGeometry(FLATTENING_TOLERANCE, ToleranceType.Absolute)

            Dim dimensions = CalculateStrokeDimensions(bounds, svgText.StrokeWidth.Value, matrix)

            Dim wpfPath As New Shapes.Path With {
                .Data = translatedGeometry,
                .Stretch = Stretch.None,
                .Width = dimensions.totalWidth,
                .Height = dimensions.totalHeight
            }

            ApplyStrokeAndFill(wpfPath, svgText, dimensions.transformedStroke)
            Canvas.SetLeft(wpfPath, bounds.X - dimensions.strokeOffset)
            Canvas.SetTop(wpfPath, bounds.Y - dimensions.strokeOffset)

            Dim drawable As New DrawablePath(wpfPath)
            AssignDrawableName(drawable, svgText.ID)
            Return drawable
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
