
Imports System.Numerics

Imports MeasurePerformance.IL.Weaver

Imports PolyCut.Core
Imports PolyCut.[Shared]

Public Class GeometryExtractor

    Public Shared Function ExtractFromDrawable(drawable As IDrawable, cfg As ProcessorConfiguration, mainCanvas As UIElement) As List(Of IPathBasedElement)

        If drawable Is Nothing OrElse mainCanvas Is Nothing Then
            Return New List(Of IPathBasedElement)
        End If

        ' Handle nested groups by recursing through children
        Dim nestedGroup = TryCast(drawable, NestedDrawableGroup)
        If nestedGroup IsNot Nothing Then
            Dim results As New List(Of IPathBasedElement)
            For Each child In nestedGroup.GroupChildren
                If child IsNot Nothing Then
                    results.AddRange(ExtractFromDrawable(child, cfg, mainCanvas))
                End If
            Next
            Return results
        End If

        ' Handle simple (non-group) drawable
        Dim element = TryCast(drawable.DrawableElement, FrameworkElement)
        If element Is Nothing Then Return New List(Of IPathBasedElement)

        ' 1. Get geometry in element's local coordinate space
        Dim geometry = GetGeometryFromElement(element)
        If geometry Is Nothing Then Return New List(Of IPathBasedElement)

        ' 2. Get accumulated transform from element to main canvas. WHY THE FUCK DID I NOT JUST DO THIS MONTHS AGO
        Dim gt As GeneralTransform
        Try
            gt = element.TransformToVisual(mainCanvas)
        Catch
            Return New List(Of IPathBasedElement)
        End Try

        Dim t As Transform = If(TryCast(gt, Transform), GeneralToMatrixTransform(gt))

        ' 3. Flatten in local space first
        Dim flattened As PathGeometry = geometry.GetFlattenedPathGeometry(cfg.Tolerance, ToleranceType.Absolute)

        ' 4. Apply transform
        Dim transformed As PathGeometry = flattened.Clone()
        transformed.Transform = t

        ' 5. Flatten again after transform
        transformed = transformed.GetFlattenedPathGeometry(cfg.Tolerance, ToleranceType.Absolute)

        ' now build lines from transformed
        Dim isFilled = IsFillEnabled(drawable)

        Dim figures = PolyCut.Core.BuildLinesFromGeometry(transformed, cfg.Tolerance)

        'trim trash
        If Keyboard.IsKeyDown(Key.LeftCtrl) Then
            figures = CleanupFigures(figures, cfg.Tolerance)
        End If

        figures = NormalizeFiguresForCut(figures, cfg.Tolerance, isFilled)
        If figures Is Nothing OrElse figures.Count = 0 Then Return New List(Of IPathBasedElement)

        Dim b = figures.ComputeBounds()

        Dim skipBoundsCheck = Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift)

        If Not skipBoundsCheck AndAlso Not IsFullyOnCanvas(b, cfg.WorkAreaWidth, cfg.WorkAreaHeight) Then
            Return New List(Of IPathBasedElement)
        End If

        ' 6. Create IPathBasedElement
        Dim pathElement = CreatePathBasedElement(drawable, figures)
        If pathElement Is Nothing Then Return New List(Of IPathBasedElement)

        pathElement.FillColor = GetFillColor(drawable)
        pathElement.IsFilled = isFilled
        pathElement.Config = cfg

        Return New List(Of IPathBasedElement) From {pathElement}
    End Function

    Private Shared Function GeneralToMatrixTransform(gt As GeneralTransform) As MatrixTransform
        Dim p0 = gt.Transform(New Point(0, 0))
        Dim p1 = gt.Transform(New Point(1, 0))
        Dim p2 = gt.Transform(New Point(0, 1))

        Dim m As New Matrix(p1.X - p0.X, p1.Y - p0.Y, p2.X - p0.X, p2.Y - p0.Y, p0.X, p0.Y)

        Return New MatrixTransform(m)
    End Function


    Private Shared Function GetGeometryFromElement(element As FrameworkElement) As Geometry

        Select Case True
            Case TypeOf element Is Rectangle
                Dim r = DirectCast(element, Rectangle)
                Return New RectangleGeometry(New Rect(0, 0, r.ActualWidth, r.ActualHeight))

            Case TypeOf element Is Ellipse
                Dim e = DirectCast(element, Ellipse)
                Dim cx = e.ActualWidth / 2
                Dim cy = e.ActualHeight / 2
                Return New EllipseGeometry(New Point(cx, cy), cx, cy)

            Case TypeOf element Is Line
                Dim ln = DirectCast(element, Line)
                Return New LineGeometry(New Point(ln.X1, ln.Y1), New Point(ln.X2, ln.Y2))

            Case TypeOf element Is Path
                Dim p = DirectCast(element, Path)

                Dim g As Geometry = If(p.Data?.Clone(), Nothing)
                If g Is Nothing Then Return Nothing

                ' If Path is stretched by wrapper, apply  stretch explicitly
                If p.Stretch = Stretch.Fill Then
                    Dim wrapper = TryCast(p.Parent, ContentControl)
                    If wrapper IsNot Nothing Then
                        Dim b = g.Bounds
                        Dim w = wrapper.ActualWidth
                        Dim h = wrapper.ActualHeight

                        If b.Width > 0 AndAlso b.Height > 0 AndAlso w > 0 AndAlso h > 0 Then
                            Dim m As Matrix = Matrix.Identity
                            m.Translate(-b.X, -b.Y)
                            m.Scale(w / b.Width, h / b.Height)
                            g.Transform = New MatrixTransform(m)
                        End If
                    End If
                End If

                Return g


            Case TypeOf element Is TextBox
                Return GetTextBoxGeometry(DirectCast(element, TextBox))
        End Select

        Return Nothing
    End Function


    Private Shared Function GetTextBoxGeometry(tb As TextBox) As Geometry
        If tb Is Nothing OrElse tb.ActualWidth <= 0 OrElse tb.ActualHeight <= 0 Then
            Return Nothing
        End If

        Dim textToDraw As String = If(String.IsNullOrEmpty(tb.Text), " ", tb.Text)

        Dim r As Rect = tb.GetRectFromCharacterIndex(0, False)
        If r.IsEmpty OrElse Double.IsNaN(r.X) OrElse Double.IsNaN(r.Y) Then
            r = New Rect(0, 0, 0, 0)
        End If

        Dim dpi = VisualTreeHelper.GetDpi(tb)
        Dim ft As New FormattedText(
            textToDraw,
            Globalization.CultureInfo.CurrentCulture,
            tb.FlowDirection,
            New Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch),
            tb.FontSize,
            Brushes.Black,
            dpi.PixelsPerDip
        ) With {
            .Trimming = TextTrimming.None,
            .TextAlignment = tb.TextAlignment
        }

        Dim origin As New Point(r.X, r.Y)
        Return ft.BuildGeometry(origin)
    End Function


    Private Shared Function CreatePathBasedElement(drawable As IDrawable, figures As List(Of List(Of GeoLine))) As IPathBasedElement
        If figures Is Nothing OrElse figures.Count = 0 Then Return Nothing

        Dim element = drawable.DrawableElement

        Select Case element.GetType()
            Case GetType(Rectangle)
                Return New RectangleElement With {.Figures = figures}
            Case GetType(Ellipse)
                Return New EllipseElement With {.Figures = figures}
            Case GetType(Path)
                Return New PathElement With {.Figures = figures}
            Case GetType(Line)
                Return New LineElement With {.Figures = figures}
            Case GetType(TextBox)
                Return New TextElement With {.Figures = figures}
            Case Else
                Return New PathElement With {.Figures = figures}
        End Select
    End Function


    Private Shared Function GetFillColor(drawable As IDrawable) As String
        If drawable Is Nothing Then Return Nothing

        Try
            Dim fillBrush = drawable.Fill
            If fillBrush Is Nothing Then Return Nothing

            Dim solidBrush = TryCast(fillBrush, SolidColorBrush)
            If solidBrush IsNot Nothing Then
                Dim c = solidBrush.Color
                Return String.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B)
            End If

            Return "#000000"
        Catch
            Return Nothing
        End Try
    End Function

    Private Shared Function IsFillEnabled(drawable As IDrawable) As Boolean
        If drawable Is Nothing Then Return False

        Dim fillBrush = drawable.Fill
        If fillBrush Is Nothing Then Return False

        If fillBrush.Opacity <= 0 Then Return False

        Dim solid = TryCast(fillBrush, SolidColorBrush)
        Return solid Is Nothing OrElse solid.Color.A > 0
    End Function

    Private Shared Function NormalizeFiguresForCut(figures As List(Of List(Of GeoLine)), tolerance As Double, forceClose As Boolean) As List(Of List(Of GeoLine))
        Dim output As New List(Of List(Of GeoLine))
        If figures Is Nothing Then Return output

        Dim continuityTolerance = Math.Max(0.000001, tolerance * 2)

        For Each fig In figures
            If fig Is Nothing OrElse fig.Count = 0 Then Continue For

            Dim normalized As New List(Of GeoLine)

            Dim first = fig(0)
            normalized.Add(first)

            For i = 1 To fig.Count - 1
                Dim prev = normalized(normalized.Count - 1)
                Dim current = fig(i)

                Dim dx = current.X1 - prev.X2
                Dim dy = current.Y1 - prev.Y2
                Dim dist = Math.Sqrt(dx * dx + dy * dy)

                If dist <= continuityTolerance Then
                    current = New GeoLine(prev.X2, prev.Y2, current.X2, current.Y2)
                End If

                normalized.Add(current)
            Next

            If forceClose AndAlso normalized.Count > 0 Then
                Dim startL = normalized(0)
                Dim endL = normalized(normalized.Count - 1)

                Dim cdx = startL.X1 - endL.X2
                Dim cdy = startL.Y1 - endL.Y2
                Dim cdist = Math.Sqrt(cdx * cdx + cdy * cdy)

                If cdist <= continuityTolerance Then
                    normalized(normalized.Count - 1) = New GeoLine(endL.X1, endL.Y1, startL.X1, startL.Y1)
                Else
                    normalized.Add(New GeoLine(endL.X2, endL.Y2, startL.X1, startL.Y1))
                End If
            End If

            output.Add(normalized)
        Next

        Return output
    End Function


    Private Shared Function IsOnCanvas(bounds As Rect, canvasW As Double, canvasH As Double) As Boolean
        If bounds.IsEmpty Then Return False
        Dim canvasRect As New Rect(0, 0, canvasW, canvasH)
        Return canvasRect.IntersectsWith(bounds) ' policy 1: any part visible
    End Function

    Private Shared Function IsFullyOnCanvas(bounds As Rect, canvasW As Double, canvasH As Double) As Boolean
        If bounds.IsEmpty Then Return False
        Dim canvasRect As New Rect(0, 0, canvasW, canvasH)
        Return canvasRect.Contains(bounds) ' policy 2: fully inside
    End Function

    Private Shared Function CleanupFigures(figures As List(Of List(Of GeoLine)), tolerance As Double) As List(Of List(Of GeoLine))
        Dim output As New List(Of List(Of GeoLine))
        If figures Is Nothing Then Return output

        ' How close is "the same point"?
        Dim eps As Double = Math.Max(0.000001, tolerance * 0.5)
        Dim eps2 As Double = eps * eps

        ' Angle tolerance for collinearity merging (in radians).
        Dim sinTol As Double = 0.0001

        If Keyboard.IsKeyDown(Key.LeftShift) Then sinTol *= 100

        For Each fig In figures
            If fig Is Nothing OrElse fig.Count = 0 Then Continue For

            Dim cleaned As New List(Of GeoLine)

            For i = 0 To fig.Count - 1
                Dim ln = fig(i)

                ' Skip lines with NaN/Infinity (rare, but protects downstream)
                If Not IsFiniteLine(ln) Then Continue For

                ' If the segment itself is basically zero-length, drop it.
                If Dist2(ln.X1, ln.Y1, ln.X2, ln.Y2) <= eps2 Then Continue For

                If cleaned.Count = 0 Then
                    cleaned.Add(ln)
                    Continue For
                End If

                Dim prev = cleaned(cleaned.Count - 1)

                ' Snap current start to previous end if they're very close
                If Dist2(prev.X2, prev.Y2, ln.X1, ln.Y1) <= eps2 Then
                    ln = New GeoLine(prev.X2, prev.Y2, ln.X2, ln.Y2)
                End If

                ' If after snapping, current becomes degenerate, drop it
                If Dist2(ln.X1, ln.Y1, ln.X2, ln.Y2) <= eps2 Then
                    Continue For
                End If

                ' If prev is degenerate (can happen after earlier edits), replace it
                If Dist2(prev.X1, prev.Y1, prev.X2, prev.Y2) <= eps2 Then
                    cleaned(cleaned.Count - 1) = ln
                    Continue For
                End If

                ' Optional: merge adjacent collinear segments (helps remove micro-vertices)
                If Dist2(prev.X2, prev.Y2, ln.X1, ln.Y1) <= eps2 AndAlso prev.IsCollinearWith(ln, sinTol) Then
                    cleaned(cleaned.Count - 1) = New GeoLine(prev.X1, prev.Y1, ln.X2, ln.Y2)
                Else
                    cleaned.Add(ln)
                End If
            Next

            ' Drop figures that collapsed to nothing
            If cleaned.Count > 0 Then output.Add(cleaned)
        Next

        Return output
    End Function

    Private Shared Function Dist2(x1 As Double, y1 As Double, x2 As Double, y2 As Double) As Double
        Dim dx = x2 - x1
        Dim dy = y2 - y1
        Return dx * dx + dy * dy
    End Function

    Private Shared Function IsFiniteLine(ln As GeoLine) As Boolean
        Return IsFinite(ln.X1) AndAlso IsFinite(ln.Y1) AndAlso IsFinite(ln.X2) AndAlso IsFinite(ln.Y2)
    End Function

    Private Shared Function IsFinite(v As Double) As Boolean
        Return Not Double.IsNaN(v) AndAlso Not Double.IsInfinity(v)
    End Function



End Class
