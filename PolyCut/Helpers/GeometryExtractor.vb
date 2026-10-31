
Imports PolyCut.Core
Imports PolyCut.[Shared]

Public Class GeometryExtractor

    Friend NotInheritable Class GeometrySnapshot
        Public Geometry As Geometry
        Public FillColor As String
        Public IsFilled As Boolean
    End Class

    ' Capture WPF state on the UI thread; only frozen geometry and values leave it.
    Friend Shared Function CaptureFromDrawable(drawable As IDrawable, cfg As ProcessorConfiguration, mainCanvas As UIElement) As List(Of GeometrySnapshot)
        If drawable Is Nothing OrElse mainCanvas Is Nothing Then
            Return New List(Of GeometrySnapshot)
        End If

        ' Handle nested groups by recursing through children
        Dim nestedGroup = TryCast(drawable, NestedDrawableGroup)
        If nestedGroup IsNot Nothing Then
            Dim results As New List(Of GeometrySnapshot)
            For Each child In nestedGroup.GroupChildren
                If child IsNot Nothing Then
                    results.AddRange(CaptureFromDrawable(child, cfg, mainCanvas))
                End If
            Next
            Return results
        End If

        If cfg.ExtractOneColour AndAlso Not RegistrationMarkHelper.IsRegistrationMark(drawable) Then
            Dim fillColor = GetFillColor(drawable, True)
            Dim strokeColor = If(drawable.Stroke IsNot Nothing, TryCast(drawable.Stroke, SolidColorBrush)?.Color, Nothing)
            'only want to extract this if its stroke or fill matches the extraction color
            If Not String.Equals(fillColor, cfg.ExtractionColor, StringComparison.OrdinalIgnoreCase) AndAlso
               Not (strokeColor.HasValue AndAlso String.Equals($"#FF{strokeColor.Value.R:X2}{strokeColor.Value.G:X2}{strokeColor.Value.B:X2}", cfg.ExtractionColor, StringComparison.OrdinalIgnoreCase)) Then
                Return New List(Of GeometrySnapshot)
            End If

        End If

        Dim geometry = DrawableGeometry.GetTransformedGeometry(drawable.DrawableElement, mainCanvas)
        If geometry Is Nothing Then Return New List(Of GeometrySnapshot)
        geometry.Freeze()
        Return New List(Of GeometrySnapshot) From {New GeometrySnapshot With {
            .Geometry = geometry,
            .FillColor = GetFillColor(drawable),
            .IsFilled = IsFillEnabled(drawable)
        }}
    End Function

    Friend Shared Function ProcessSnapshot(item As GeometrySnapshot, cfg As ProcessorConfiguration) As List(Of PathBasedElement)

        Dim flattened As PathGeometry = item.Geometry.GetFlattenedPathGeometry(cfg.Tolerance, ToleranceType.Absolute)
        Dim figures = BuildLinesFromGeometry(flattened, cfg.Tolerance)

        If cfg.ClipToBounds Then
            figures = ClipFiguresToWorkArea(figures, cfg.WorkAreaWidth, cfg.WorkAreaHeight)
        End If

        figures = NormalizeFiguresForCut(figures, cfg.Tolerance, item.IsFilled)
        If cfg.SimplifyToolPaths Then figures = CleanupFigures(figures, cfg.Tolerance, cfg.SimplifyMergeAngle, cfg.SmallFeatureMergeThreshold)
        If figures Is Nothing OrElse figures.Count = 0 Then Return New List(Of PathBasedElement)

        ' Nothing to check once clipped: the clip rectangle already guarantees containment
        If Not cfg.ClipToBounds AndAlso Not cfg.SkipBoundsCheck AndAlso Not IsFullyOnCanvas(figures.ComputeBounds(), cfg.WorkAreaWidth, cfg.WorkAreaHeight) Then
            Return New List(Of PathBasedElement)
        End If

        Dim pathElement = New PathBasedElement With {
            .Figures = figures,
            .FillColor = item.FillColor,
            .IsFilled = item.IsFilled
        }

        Return New List(Of PathBasedElement) From {pathElement}
    End Function

    Private Shared Function ClipFiguresToWorkArea(figures As List(Of List(Of GeoLine)), w As Double, h As Double) As List(Of List(Of GeoLine))
        Dim output As New List(Of List(Of GeoLine))
        If figures Is Nothing OrElse w <= 0 OrElse h <= 0 Then Return output

        Dim edges = {
            (kind:=0, value:=0.0, min:=True),
            (kind:=0, value:=w, min:=False),
            (kind:=1, value:=0.0, min:=True),
            (kind:=1, value:=h, min:=False)
        }

        For Each fig In figures
            If fig Is Nothing OrElse fig.Count = 0 Then Continue For

            Dim pts As New List(Of Point)(fig.Count + 1)

            fig.ForEach(Sub(ln) pts.Add(New Point(ln.X1, ln.Y1)))

            pts.Add(New Point(fig(fig.Count - 1).X2, fig(fig.Count - 1).Y2))

            Dim closed As Boolean = pts(0) = pts(pts.Count - 1)
            For Each e In edges
                pts = ClipHalfPlane(pts, e.kind, e.value, e.min)
                If pts.Count = 0 Then Exit For
            Next

            If pts.Count >= 2 AndAlso closed AndAlso pts(0) <> pts(pts.Count - 1) Then pts.Add(pts(0))
            If pts.Count < 2 Then Continue For

            Dim clipped As New List(Of GeoLine)(pts.Count - 1)
            For i = 0 To pts.Count - 2
                clipped.Add(New GeoLine(CSng(pts(i).X), CSng(pts(i).Y), CSng(pts(i + 1).X), CSng(pts(i + 1).Y)))
            Next

            If clipped.Count > 0 Then output.Add(clipped)
        Next

        Return output
    End Function

    Private Shared Function InsideHalfPlane(p As Point, kind As Integer, value As Double, min As Boolean) As Boolean
        Dim v As Double = If(kind = 0, p.X, p.Y)
        Return If(min, v >= value, v <= value)
    End Function

    Private Shared Function CrossBoundary(a As Point, b As Point, kind As Integer, value As Double) As Point
        If kind = 0 Then
            Dim t As Double = (value - a.X) / (b.X - a.X)
            Return New Point(value, a.Y + t * (b.Y - a.Y))
        Else
            Dim t As Double = (value - a.Y) / (b.Y - a.Y)
            Return New Point(a.X + t * (b.X - a.X), value)
        End If
    End Function

    Private Shared Function ClipHalfPlane(pts As List(Of Point), kind As Integer, value As Double, min As Boolean) As List(Of Point)
        Dim result As New List(Of Point)
        If pts.Count = 0 Then Return result

        Dim prev As Point = pts(pts.Count - 1)
        Dim prevIn As Boolean = InsideHalfPlane(prev, kind, value, min)

        For Each cur In pts
            Dim curIn As Boolean = InsideHalfPlane(cur, kind, value, min)

            If curIn Then
                If Not prevIn Then result.Add(CrossBoundary(prev, cur, kind, value))
                result.Add(cur)
            ElseIf prevIn Then
                result.Add(CrossBoundary(prev, cur, kind, value))
            End If

            prev = cur
            prevIn = curIn
        Next

        Return result
    End Function

    Private Shared Function GetFillColor(drawable As IDrawable, Optional includeAlpha As Boolean = False) As String
        If drawable Is Nothing Then Return Nothing

        Try
            Dim fillBrush = drawable.Fill
            If fillBrush Is Nothing Then Return Nothing

            Dim solidBrush = TryCast(fillBrush, SolidColorBrush)
            If solidBrush IsNot Nothing Then
                Dim c = solidBrush.Color
                If includeAlpha Then
                    Return String.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", c.A, c.R, c.G, c.B)
                Else
                    Return String.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B)
                End If
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


    Private Shared Function IsFullyOnCanvas(bounds As Rect, canvasW As Double, canvasH As Double) As Boolean
        If bounds.IsEmpty Then Return False
        Dim canvasRect As New Rect(0, 0, canvasW, canvasH)
        Return canvasRect.Contains(bounds) ' policy 2: fully inside
    End Function

    Private Shared Function CleanupFigures(figures As List(Of List(Of GeoLine)), tolerance As Double, mergeAngle As Double, smallFeatureThreshold As Double) As List(Of List(Of GeoLine))
        Dim output As New List(Of List(Of GeoLine))
        If figures Is Nothing Then Return output

        ' How close is "the same point"?
        Dim eps As Double = Math.Max(0.000001, tolerance * 0.5)
        Dim eps2 As Double = eps * eps

        Dim rad = Math.PI * Math.Clamp(mergeAngle, 0, 90) / 180.0

        For Each fig In figures
            If fig Is Nothing OrElse fig.Count = 0 Then Continue For

            Dim closed = Dist2(fig.Last().X2, fig.Last().Y2, fig(0).X1, fig(0).Y1) <= eps2
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

                cleaned.Add(ln)
            Next

            ' Removing a tiny closing segment must not leave a gap at the seam.
            If closed AndAlso cleaned.Count > 0 AndAlso
                Dist2(cleaned.Last().X2, cleaned.Last().Y2, cleaned(0).X1, cleaned(0).Y1) <= eps2 Then
                Dim last = cleaned.Last()
                cleaned(cleaned.Count - 1) = New GeoLine(last.X1, last.Y1, cleaned(0).X1, cleaned(0).Y1)
            End If

            Dim minimumCount = If(closed, 3, 1)
            If IsFinite(smallFeatureThreshold) AndAlso smallFeatureThreshold > 0 Then
                MergeSmallFeatures(cleaned, closed, minimumCount, eps2, rad, smallFeatureThreshold)
            End If
            Dim mergedSeam As Boolean
            Do
                Dim simplified As New List(Of GeoLine)(cleaned.Count)
                For i = 0 To cleaned.Count - 1
                    Dim current = cleaned(i)
                    ' Recheck the preceding join whenever a merge changes the direction.
                    While simplified.Count > 0 AndAlso simplified.Count + cleaned.Count - i > minimumCount
                        Dim previous = simplified.Last()
                        If Not CanMergeLines(previous, current, eps2, rad) Then Exit While
                        current = New GeoLine(previous.X1, previous.Y1, current.X2, current.Y2)
                        simplified.RemoveAt(simplified.Count - 1)
                    End While
                    simplified.Add(current)
                Next
                cleaned = simplified

                mergedSeam = closed AndAlso cleaned.Count > minimumCount AndAlso
                    CanMergeLines(cleaned.Last(), cleaned(0), eps2, rad)
                If mergedSeam Then
                    cleaned(0) = New GeoLine(cleaned.Last().X1, cleaned.Last().Y1, cleaned(0).X2, cleaned(0).Y2)
                    cleaned.RemoveAt(cleaned.Count - 1)
                End If
            Loop While mergedSeam

            ' Drop figures that collapsed to nothing
            If cleaned.Count > 0 Then output.Add(cleaned)
        Next

        Return output
    End Function

    Private Shared Sub MergeSmallFeatures(lines As List(Of GeoLine), closed As Boolean, minimumCount As Integer, eps2 As Double, angle As Double, threshold As Double)
        Dim merged As Boolean
        Do
            merged = False
            If lines.Count - 2 < minimumCount Then Exit Do

            For i = 0 To If(closed, lines.Count - 1, lines.Count - 3)
                Dim a = lines(i)
                Dim b = lines((i + 1) Mod lines.Count)
                Dim c = lines((i + 2) Mod lines.Count)

                If Dist2(a.X2, a.Y2, b.X1, b.Y1) > eps2 OrElse Dist2(b.X2, b.Y2, c.X1, c.Y1) > eps2 Then Continue For

                ' The long edges must resume the same direction around the excursion.
                If Not HaveSimilarDirection(a, c, angle) Then Continue For

                Dim replacement As New GeoLine(a.StartPoint, c.EndPoint)
                Dim dx = CDbl(replacement.X2) - replacement.X1
                Dim dy = CDbl(replacement.Y2) - replacement.Y1
                Dim length2 = dx * dx + dy * dy
                If length2 <= eps2 Then Continue For

                Dim firstProjection = ((CDbl(a.X2) - replacement.X1) * dx + (CDbl(a.Y2) - replacement.Y1) * dy) / length2
                Dim secondProjection = ((CDbl(b.X2) - replacement.X1) * dx + (CDbl(b.Y2) - replacement.Y1) * dy) / length2

                ' Preserve reversals and endpoints; both removed vertices must lie along the replacement.
                If firstProjection < 0 OrElse secondProjection < firstProjection OrElse secondProjection > 1 Then Continue For
                Dim firstDistance = Math.Abs(dx * (CDbl(a.Y2) - replacement.Y1) - dy * (CDbl(a.X2) - replacement.X1))
                Dim secondDistance = Math.Abs(dx * (CDbl(b.Y2) - replacement.Y1) - dy * (CDbl(b.X2) - replacement.X1))
                If Math.Max(firstDistance, secondDistance) > threshold * Math.Sqrt(length2) Then Continue For

                ' Rotate only when the three segments straddle the closing seam.
                If i + 2 >= lines.Count Then
                    Dim prefix = lines.GetRange(0, i)
                    lines.RemoveRange(0, i)
                    lines.AddRange(prefix)
                    i = 0
                End If
                lines(i) = replacement
                lines.RemoveRange(i + 1, 2)
                merged = True
                Exit For
            Next
        Loop While merged
    End Sub

    Private Shared Function CanMergeLines(a As GeoLine, b As GeoLine, eps2 As Double, angle As Double) As Boolean
        If Dist2(a.X2, a.Y2, b.X1, b.Y1) > eps2 Then Return False
        Return HaveSimilarDirection(a, b, angle)
    End Function

    Private Shared Function HaveSimilarDirection(a As GeoLine, b As GeoLine, angle As Double) As Boolean
        Dim ax = CDbl(a.X2) - a.X1
        Dim ay = CDbl(a.Y2) - a.Y1
        Dim bx = CDbl(b.X2) - b.X1
        Dim by = CDbl(b.Y2) - b.Y1
        Dim dot = ax * bx + ay * by
        ' Only merge forward motion just in case we intentionally want to reverse cutting direction nearly collinenarly
        Return dot > 0 AndAlso Math.Atan2(Math.Abs(ax * by - ay * bx), dot) <= angle
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
