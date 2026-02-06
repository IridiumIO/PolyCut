Imports System.Numerics
Imports System.Windows.Shapes

Imports MeasurePerformance.IL.Weaver

Public Class FillProcessor : Implements IProcessor

    ' -------------------------
    ' Constants / configuration
    ' -------------------------

    Private Const DefaultScalingFactor As Double = 100_000 ' 1mm -> 100m in scaled units

    ' Intersection tolerances
    Private Const IntersectionTolerance As Double = 0.000000001
    Private Const MergeTolerance As Double = 1.0

    ' Optimiser tuning (converts angular difference into a distance-like penalty - units: squared distance. Higher values = try harder to continue directioin)
    Private Const DirectionPreferenceWeight As Double = 5000.0



    ' -------------------------
    ' Entry point
    ' -------------------------
    Public Function Process(lines As List(Of Line), cfg As ProcessorConfiguration) As List(Of Line) Implements IProcessor.Process

        ' Respect per-element SVG fill presence when deciding to generate fills.
        Dim fillTag As Object = Nothing
        If lines IsNot Nothing AndAlso lines.Count > 0 Then fillTag = lines(0).Tag

        If Not ShouldGenerateFill(fillTag) Then Return lines
        If Not IsShapeClosed(lines) OrElse cfg.DrawingConfig.FillType = FillType.None Then Return lines

        Dim spacingNullable As Double? = ComputeSpacingFromTag(fillTag, cfg)
        If Not spacingNullable.HasValue Then Return lines

        Dim spacing As Double = spacingNullable.Value
        Dim spacingScaled As Double = spacing * DefaultScalingFactor

        Dim outlineGeo As List(Of GeoLine) = ToGeoLines(lines, DefaultScalingFactor)

        Dim processedGeo As List(Of GeoLine) = GenerateFill(outlineGeo, spacingScaled, cfg.DrawingConfig.FillType, cfg.DrawingConfig.ShadingAngle)

        If cfg.OptimisedToolPath Then
            processedGeo = OptimiseFills(processedGeo, outlineGeo, cfg.DrawingConfig.AllowDrawingOverOutlines)
        End If

        If cfg.DrawingConfig.KeepOutlines Then
            If cfg.DrawingConfig.OutlinesBeforeFill Then
                processedGeo.InsertRange(0, outlineGeo)
            Else
                processedGeo.AddRange(outlineGeo)
            End If
        End If

        Return ToWpfLines(processedGeo, DefaultScalingFactor)
    End Function



    ' -------------------------
    ' Fill type generation
    ' -------------------------
    <MeasurePerformance>
    Private Shared Function GenerateFill(outline As List(Of GeoLine), spacingScaled As Double, fillType As FillType, angle As Double) As List(Of GeoLine)
        Dim result As New List(Of GeoLine)

        Select Case fillType
            Case FillType.Spiral
                result.AddRange(GenerateSpiralFill(outline, spacingScaled, angle))
            Case FillType.Radial
                result.AddRange(GenerateRadialFill(outline, spacingScaled, angle))

            Case FillType.CrossHatch
                result.AddRange(GenerateHatchFill(outline, spacingScaled, angle))
                result.AddRange(GenerateHatchFill(outline, spacingScaled, angle + 90))

            Case FillType.TriangularHatch
                result.AddRange(GenerateTriangleAlignedHatchFill(outline, spacingScaled, angle))
                result.AddRange(GenerateTriangleAlignedHatchFill(outline, spacingScaled, angle + 60))
                result.AddRange(GenerateTriangleAlignedHatchFill(outline, spacingScaled, angle + 120))

            Case FillType.DiamondCrossHatch
                result.AddRange(GenerateSquareAlignedHatchFill(outline, spacingScaled, 0))
                result.AddRange(GenerateSquareAlignedHatchFill(outline, spacingScaled, 45))
                result.AddRange(GenerateSquareAlignedHatchFill(outline, spacingScaled, 90))
                result.AddRange(GenerateSquareAlignedHatchFill(outline, spacingScaled, 135))

            Case Else 'regular hatch
                result.AddRange(GenerateHatchFill(outline, spacingScaled, angle))
        End Select

        Return result
    End Function



    ' -------------------------
    ' Hatch/Crosshatch fill
    ' -------------------------
    Public Shared Function GenerateHatchFill(lines As List(Of GeoLine), density As Double, fillangle As Double) As List(Of GeoLine)
        Dim fills As New List(Of GeoLine)
        If lines Is Nothing OrElse lines.Count = 0 OrElse density <= 0 Then Return fills

        Dim traverseAngleRad = Math.PI * fillangle / 180 + (Math.PI / 2)
        Dim fillAngleRad = Math.PI * fillangle / 180

        Dim bounds As Bounds2D = ComputeBounds(lines)
        Dim centerX = (bounds.MinX + bounds.MaxX) * 0.5
        Dim centerY = (bounds.MinY + bounds.MaxY) * 0.5

        Dim dx = bounds.MaxX - bounds.MinX
        Dim dy = bounds.MaxY - bounds.MinY

        Dim maxExtent = Math.Sqrt(dx * dx + dy * dy)
        Dim scaleFactor = 10 * Math.Max(dx, dy)

        Dim cosTrav = Math.Cos(traverseAngleRad)
        Dim sinTrav = Math.Sin(traverseAngleRad)
        Dim cosFill = Math.Cos(fillAngleRad)
        Dim sinFill = Math.Sin(fillAngleRad)

        For traversePosition = -maxExtent To maxExtent Step density
            Dim sx = centerX + traversePosition * cosTrav
            Dim sy = centerY + traversePosition * sinTrav

            Dim ray As New GeoLine(
                X1:=sx - scaleFactor * cosFill,
                Y1:=sy - scaleFactor * sinFill,
                X2:=sx + scaleFactor * cosFill,
                Y2:=sy + scaleFactor * sinFill
            )

            fills.AddRange(ClipLineAgainstShape(lines, ray, isSegment:=False))
        Next

        Return fills
    End Function


    Private Shared Function GenerateTriangleAlignedHatchFill(lines As List(Of GeoLine), baseSpacing As Double, fillangle As Double) As List(Of GeoLine)

        If lines Is Nothing OrElse lines.Count = 0 OrElse baseSpacing <= 0 Then Return New List(Of GeoLine)

        ' Triangular lattice: perpendicular spacing = S * sin(60) = S * √3/2
        Const Sin60 As Double = 0.86602540378444
        Dim densityEff As Double = baseSpacing * Sin60

        Return GenerateAlignedHatchFillCore(lines, densityEff, fillangle)
    End Function


    Private Shared Function GenerateSquareAlignedHatchFill(lines As List(Of GeoLine), baseSpacing As Double, fillangle As Double) As List(Of GeoLine)

        If lines Is Nothing OrElse lines.Count = 0 OrElse baseSpacing <= 0 Then Return New List(Of GeoLine)

        Dim fillAngleRad = Math.PI * fillangle / 180.0
        Dim cosFill = Math.Cos(fillAngleRad)
        Dim sinFill = Math.Sin(fillAngleRad)

        ' Square lattice factor: 0/90 => 1, 45/135 => 1/√2
        Dim factor As Double = Math.Max(Math.Abs(sinFill), Math.Abs(cosFill))
        Dim densityEff As Double = baseSpacing * factor
        If densityEff <= 0 Then densityEff = baseSpacing

        Return GenerateAlignedHatchFillCore(lines, densityEff, fillangle)
    End Function


    Private Shared Function GenerateAlignedHatchFillCore(lines As List(Of GeoLine), densityEff As Double, fillangle As Double) As List(Of GeoLine)

        Dim fills As New List(Of GeoLine)
        If lines Is Nothing OrElse lines.Count = 0 OrElse densityEff <= 0 Then Return fills

        Dim fillAngleRad = Math.PI * fillangle / 180.0

        ' Fill direction u
        Dim cosFill = Math.Cos(fillAngleRad)
        Dim sinFill = Math.Sin(fillAngleRad)

        ' Traverse normal n
        Dim nx As Double = -sinFill
        Dim ny As Double = cosFill

        Dim bounds As Bounds2D = ComputeBounds(lines)
        Dim centerX = (bounds.MinX + bounds.MaxX) * 0.5
        Dim centerY = (bounds.MinY + bounds.MaxY) * 0.5

        Dim dx = bounds.MaxX - bounds.MinX
        Dim dy = bounds.MaxY - bounds.MinY
        Dim maxExtent = Math.Sqrt(dx * dx + dy * dy)
        Dim scaleFactor = 10.0 * Math.Max(dx, dy)

        ' Anchor at center 
        Dim anchorX As Double = centerX
        Dim anchorY As Double = centerY

        ' Traverse coordinate of center and anchor
        Dim tCenter As Double = centerX * nx + centerY * ny
        Dim tAnchor As Double = anchorX * nx + anchorY * ny

        Dim tMin As Double = tCenter - maxExtent
        Dim tMax As Double = tCenter + maxExtent

        Dim kMin As Integer = CInt(Math.Floor((tMin - tAnchor) / densityEff))
        Dim kMax As Integer = CInt(Math.Ceiling((tMax - tAnchor) / densityEff))
        If kMin > kMax Then
            Dim tmp = kMin : kMin = kMax : kMax = tmp
        End If

        For k As Integer = kMin To kMax
            Dim off As Double = k * densityEff
            Dim sx As Double = anchorX + off * nx
            Dim sy As Double = anchorY + off * ny

            Dim ray As New GeoLine(
            X1:=sx - scaleFactor * cosFill,
            Y1:=sy - scaleFactor * sinFill,
            X2:=sx + scaleFactor * cosFill,
            Y2:=sy + scaleFactor * sinFill
        )

            fills.AddRange(ClipLineAgainstShape(lines, ray, isSegment:=False))
        Next

        Return fills
    End Function



    ' -------------------------
    ' Spiral fill
    ' -------------------------
    Public Shared Function GenerateSpiralFill(lines As List(Of GeoLine), density As Double, fillangle As Double) As List(Of GeoLine)
        Dim fills As New List(Of GeoLine)
        If lines Is Nothing OrElse lines.Count = 0 OrElse density <= 0 Then Return fills

        Dim bounds As Bounds2D = ComputeBounds(lines)
        Dim centerX = (bounds.MinX + bounds.MaxX) / 2
        Dim centerY = (bounds.MinY + bounds.MaxY) / 2
        Dim center As New Vector2(CSng(centerX), CSng(centerY))

        ' Determine max radius from center to cover the shape (kept as before)
        Dim maxRadius As Double = 0
        For Each ln In lines
            Dim d1 = Vector2.Distance(center, ln.StartPoint)
            Dim d2 = Vector2.Distance(center, ln.EndPoint)
            Dim localMax = If(d1 > d2, d1, d2)
            If localMax > maxRadius Then maxRadius = localMax
        Next

        ' Archimedean spiral r = b * theta
        Dim b As Double = density / (2 * Math.PI)
        Dim thetaOffset As Double = Math.PI * fillangle / 180

        Dim theta As Double = 0
        Dim maxRadiusExtended = maxRadius + density

        Dim points As New List(Of Vector2)

        While True
            Dim r = b * theta
            If r > maxRadiusExtended Then Exit While

            Dim ang = theta + thetaOffset
            Dim x = center.X + CSng(r * Math.Cos(ang))
            Dim y = center.Y + CSng(r * Math.Sin(ang))
            points.Add(New Vector2(x, y))

            Dim ds = Math.Sqrt(r * r + b * b)
            Dim stepLength = density / 2
            Dim dTheta = stepLength / ds
            dTheta = Math.Clamp(dTheta, 0.02, 0.5)
            theta += dTheta
        End While

        ' Clip spiral segments to the shape
        For i As Integer = 0 To points.Count - 2
            Dim segment As New GeoLine(points(i), points(i + 1))
            fills.AddRange(ClipLineAgainstShape(lines, segment, isSegment:=True))
        Next

        Return fills
    End Function



    ' -------------------------
    ' Radial fill
    ' -------------------------
    Public Shared Function GenerateRadialFill(lines As List(Of GeoLine), spacing As Double, angleDeg As Double) As List(Of GeoLine)
        Dim fills As New List(Of GeoLine)
        If lines Is Nothing OrElse lines.Count = 0 OrElse spacing <= 0 Then Return fills

        Dim bounds As Bounds2D = ComputeBounds(lines)
        Dim cx As Double = (bounds.MinX + bounds.MaxX) * 0.5
        Dim cy As Double = (bounds.MinY + bounds.MaxY) * 0.5
        Dim center As New Vector2(CSng(cx), CSng(cy))

        ' Radius large enough to cover the whole shape
        Dim dx = bounds.MaxX - bounds.MinX
        Dim dy = bounds.MaxY - bounds.MinY
        Dim radius As Double = 0.5 * Math.Sqrt(dx * dx + dy * dy)

        If radius <= 0 Then Return fills

        ' Make rays long enough that the segment definitely crosses the outline
        Dim rayLen As Double = radius * 3.0

        Dim dTheta As Double = spacing / radius
        dTheta = Math.Clamp(dTheta, 0.01, 0.5)

        Dim theta0 As Double = Math.PI * angleDeg / 180.0

        ' Cover full circle.  include endpoint so pattern is stable.
        Dim steps As Integer = Math.Max(1, CInt(Math.Ceiling((2.0 * Math.PI) / dTheta)))

        For i As Integer = 0 To steps - 1
            Dim theta As Double = theta0 + i * (2.0 * Math.PI / steps)

            Dim ux As Double = Math.Cos(theta)
            Dim uy As Double = Math.Sin(theta)

            Dim p1 As New Vector2(CSng(cx - rayLen * ux), CSng(cy - rayLen * uy))
            Dim p2 As New Vector2(CSng(cx + rayLen * ux), CSng(cy + rayLen * uy))

            Dim ray As New GeoLine(p1, p2)

            fills.AddRange(ClipLineAgainstShape(lines, ray, isSegment:=False))
        Next

        Return fills
    End Function



    ' -------------------------
    ' Clipping
    ' -------------------------
    Private Shared Function ClipLineAgainstShape(shapeBoundaries As List(Of GeoLine), line As GeoLine, isSegment As Boolean) As List(Of GeoLine)
        Dim mergeTol2 As Double = MergeTolerance * MergeTolerance

        Dim dir As Vector2 = line.EndPoint - line.StartPoint
        Dim dirLen2 As Double = dir.LengthSquared()
        If dirLen2 <= 0 OrElse shapeBoundaries Is Nothing OrElse shapeBoundaries.Count = 0 Then Return New List(Of GeoLine)

        Dim hits As New List(Of (t As Double, p As Vector2))(shapeBoundaries.Count \ 2 + If(isSegment, 2, 0))
        Dim start As Vector2 = line.StartPoint

        For Each edge In shapeBoundaries
            Dim hit = line.GetIntersectionPointWith(edge, IncludeCoincidentIntersection:=False, tolerance:=IntersectionTolerance)
            If Not hit.HasValue Then Continue For

            Dim p As Vector2 = hit.Value
            Dim t As Double = Vector2.Dot(p - start, dir) / dirLen2

            If isSegment Then
                If t >= -IntersectionTolerance AndAlso t <= 1 + IntersectionTolerance Then hits.Add((t, p))
            Else : hits.Add((t, p))
            End If
        Next

        If isSegment Then
            hits.Add((0.0, line.StartPoint))
            hits.Add((1.0, line.EndPoint))
        End If

        Return BuildSegmentsFromHits(hits, shapeBoundaries, mergeTol2)
    End Function


    Private Shared Function BuildSegmentsFromHits(hits As List(Of (t As Double, p As Vector2)), shapeBoundaries As List(Of GeoLine), mergeTol2 As Double) As List(Of GeoLine)
        If hits Is Nothing OrElse hits.Count < 2 Then Return New List(Of GeoLine)

        hits.Sort(Function(a, b) a.t.CompareTo(b.t)) ' Sort along the ray/segment.

        ' Merge near-duplicate intersections (vertex hits).
        Dim pts As New List(Of Vector2)(hits.Count)
        Dim last As Vector2 = hits(0).p
        pts.Add(last)

        For i As Integer = 1 To hits.Count - 1
            Dim p As Vector2 = hits(i).p
            If Vector2.DistanceSquared(last, p) > mergeTol2 Then
                pts.Add(p)
                last = p
            End If
        Next

        If pts.Count < 2 Then Return New List(Of GeoLine)

        ' Build potential intervals and keep those whose midpoint is inside (even-odd).
        Dim segments As New List(Of GeoLine)(Math.Max(0, pts.Count - 1))

        For i As Integer = 0 To pts.Count - 2
            Dim a As Vector2 = pts(i)
            Dim b As Vector2 = pts(i + 1)

            If Vector2.DistanceSquared(a, b) <= mergeTol2 Then Continue For

            Dim mid As New Vector2((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5)

            If IsPointInsideEvenOdd(mid, shapeBoundaries) Then
                segments.Add(a.LineTo(b))
            End If
        Next

        Return segments
    End Function



    ' -------------------------
    ' Point-in-polygon
    ' -------------------------
    Private Shared Function IsPointInsideEvenOdd(p As Vector2, edges As List(Of GeoLine)) As Boolean
        Dim inside As Boolean = False
        Dim py As Double = p.Y
        Dim px As Double = p.X

        For Each e In edges
            Dim x1 As Double = e.X1
            Dim y1 As Double = e.Y1
            Dim x2 As Double = e.X2
            Dim y2 As Double = e.Y2

            ' Skip horizontal edges
            If y1 = y2 Then Continue For

            Dim yMin As Double, yMax As Double
            If y1 < y2 Then
                yMin = y1 : yMax = y2
            Else
                yMin = y2 : yMax = y1
            End If

            If py <= yMin OrElse py > yMax Then Continue For
            Dim xInt As Double = x1 + (py - y1) * (x2 - x1) / (y2 - y1)
            If xInt > px Then inside = Not inside
        Next

        Return inside
    End Function



    ' -------------------------
    ' Optimiser 
    ' -------------------------
    Public Shared Function OptimiseFills(lines As List(Of GeoLine), geometrybounds As List(Of GeoLine), allowTravelInOutlines As Boolean, Optional preferDirection As Boolean = True, Optional startPoint As Nullable(Of Vector2) = Nothing) As List(Of GeoLine)

        Dim workingLines As New List(Of GeoLine)(lines)
        Dim currentPoint As Vector2
        If startPoint.HasValue Then
            currentPoint = startPoint.Value
        ElseIf workingLines.Count > 0 Then
            currentPoint = workingLines(0).StartPoint
        Else
            currentPoint = New Vector2(0, 0)
        End If

        Dim optimisedLines As New List(Of GeoLine)

        ' Keep track of the previous drawn direction so we can prefer candidates that continue the same vector.
        Dim previousDirection As Vector2 = New Vector2(0, 0)
        Dim havePreviousDirection As Boolean = False


        While workingLines.Count > 0
            Dim bestLine As GeoLine = Nothing
            Dim bestCost As Double = Double.MaxValue
            Dim bestIsReversed As Boolean = False

            ' Find the best next line using a combined metric: distance^2 + (directionPenalty)
            For Each line In workingLines

                Dim startDistance As Double = Vector2.DistanceSquared(currentPoint, line.StartPoint)
                Dim endDistance As Double = Vector2.DistanceSquared(currentPoint, line.EndPoint)

                ' assume we'll travel to the nearer endpoint
                Dim chosenDistance As Double
                Dim candidateDir As Vector2

                If startDistance <= endDistance Then
                    chosenDistance = startDistance
                    candidateDir = line.EndPoint - line.StartPoint
                Else
                    chosenDistance = endDistance
                    candidateDir = line.StartPoint - line.EndPoint
                End If

                Dim directionPenalty As Double = 0.0

                If preferDirection AndAlso havePreviousDirection AndAlso candidateDir.LengthSquared() > 0 Then
                    Dim candNorm As Vector2 = Vector2.Normalize(candidateDir)
                    Dim prevNorm As Vector2 = Vector2.Normalize(previousDirection)
                    ' dot in [-1,1]; We use absolute dot so opposite-direction (same line vector reversed) also counts as aligned.
                    Dim dot As Double = Vector2.Dot(prevNorm, candNorm)
                    If dot < -1.0 Then dot = -1.0
                    If dot > 1.0 Then dot = 1.0

                    Dim alignment As Double = Math.Abs(dot) ' 1.0 means perfect alignment, 0 means perpendicular
                    ' penalty decreases with better alignment
                    directionPenalty = (1.0 - alignment) * DirectionPreferenceWeight
                End If

                Dim totalCost As Double = chosenDistance + directionPenalty

                If totalCost < bestCost Then
                    bestCost = totalCost
                    bestLine = line
                    bestIsReversed = If(startDistance <= endDistance, False, True)
                End If

            Next

            ' If bestLine is nothing then break (shouldn't happen)
            If bestLine Is Nothing Then Exit While

            ' Compute fractional travel line
            Dim fractionalLine As New GeoLine(currentPoint, If(bestIsReversed, bestLine.EndPoint, bestLine.StartPoint))

            If allowTravelInOutlines AndAlso fractionalLine.IsLineOnAnyLine(geometrybounds, 100) Then
                optimisedLines.Add(fractionalLine)
            End If

            ' Append the selected line (respecting chosen orientation)
            If bestIsReversed Then
                optimisedLines.Add(bestLine.Reverse())
                ' Update currentPoint and previousDirection
                previousDirection = bestLine.StartPoint - bestLine.EndPoint
                currentPoint = bestLine.StartPoint
            Else
                optimisedLines.Add(bestLine)
                previousDirection = bestLine.EndPoint - bestLine.StartPoint
                currentPoint = bestLine.EndPoint
            End If

            havePreviousDirection = previousDirection.LengthSquared() > 0

            workingLines.Remove(bestLine)

        End While

        Return optimisedLines

    End Function



    ' -------------------------
    ' Closure test
    ' -------------------------
    Public Shared Function IsShapeClosed(lines As List(Of Line)) As Boolean

        For i = 0 To lines.Count - 1
            For j = i To lines.Count - 1
                If i = j Then Continue For
                If lines(i).StartPoint = lines(j).EndPoint Then Return True
            Next
        Next
        Return False
    End Function



    ' -------------------------
    ' Tag policy 
    ' -------------------------
    Private Function ShouldGenerateFill(fillTag As Object) As Boolean
        If fillTag Is Nothing Then Return False
        If TypeOf fillTag Is Boolean Then Return CType(fillTag, Boolean)
        Return True
    End Function

    Private Function ComputeSpacingFromTag(fillTag As Object, cfg As ProcessorConfiguration) As Double?
        ' Map tag (Color #RRGGBB) -> spacing value between MinStrokeWidth and MaxStrokeWidth.
        Dim minW = cfg.DrawingConfig.MinStrokeWidth
        Dim maxW = cfg.DrawingConfig.MaxStrokeWidth
        If maxW < minW Then
            Dim tmp = minW : minW = maxW : maxW = tmp
        End If

        Dim threshold As Double = Math.Clamp(cfg.DrawingConfig.ShadingThreshold, 0, 1)

        ' Defaults
        Dim spacing As Double = minW


        If TypeOf fillTag Is String Then
            Dim s = CType(fillTag, String)
            If s.StartsWith("#") AndAlso s.Length = 7 Then
                Try
                    Dim r = Convert.ToInt32(s.Substring(1, 2), 16)
                    Dim g = Convert.ToInt32(s.Substring(3, 2), 16)
                    Dim b = Convert.ToInt32(s.Substring(5, 2), 16)
                    Dim brightness = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0
                    brightness = Math.Round(brightness, 3)
                    If brightness < threshold Then Return Nothing

                    ' Map brightness to spacing: brighter => wider spacing (lighter)
                    spacing = minW + brightness * (maxW - minW)
                    Return spacing
                Catch
                    Return (minW + maxW) / 2
                End Try
            Else
                ' non-hex paint (gradient/pattern) -> fallback to mid
                Dim brightness = 0.5
                If brightness < threshold Then Return Nothing
                Return (minW + maxW) / 2
            End If
        ElseIf TypeOf fillTag Is Boolean AndAlso CType(fillTag, Boolean) = True Then
            ' Unknown colour but filled: use dense (min)
            Return minW
        End If

        Return Nothing
    End Function



    ' -------------------------
    ' Conversions + bounds
    ' -------------------------
    Private Shared Function ToGeoLines(lines As List(Of Line), scale As Double) As List(Of GeoLine)
        Dim result As New List(Of GeoLine)(If(lines?.Count, 0))
        If lines Is Nothing Then Return result

        For Each ln In lines
            result.Add(New GeoLine(ln.X1 * scale, ln.Y1 * scale, ln.X2 * scale, ln.Y2 * scale))
        Next

        Return result
    End Function

    Private Shared Function ToWpfLines(lines As List(Of GeoLine), scale As Double) As List(Of Line)
        Dim result As New List(Of Line)(If(lines?.Count, 0))
        If lines Is Nothing Then Return result

        For Each ln In lines
            result.Add(New Line With {
                .X1 = ln.X1 / scale,
                .Y1 = ln.Y1 / scale,
                .X2 = ln.X2 / scale,
                .Y2 = ln.Y2 / scale
            })
        Next

        Return result
    End Function


    Private Structure Bounds2D
        Public ReadOnly MinX As Double
        Public ReadOnly MinY As Double
        Public ReadOnly MaxX As Double
        Public ReadOnly MaxY As Double

        Public Sub New(minX As Double, minY As Double, maxX As Double, maxY As Double)
            Me.MinX = minX
            Me.MinY = minY
            Me.MaxX = maxX
            Me.MaxY = maxY
        End Sub
    End Structure

    Private Shared Function ComputeBounds(lines As List(Of GeoLine)) As Bounds2D
        Dim minX As Double = Double.PositiveInfinity
        Dim minY As Double = Double.PositiveInfinity
        Dim maxX As Double = Double.NegativeInfinity
        Dim maxY As Double = Double.NegativeInfinity

        If lines Is Nothing OrElse lines.Count = 0 Then
            Return New Bounds2D(0, 0, 0, 0)
        End If

        For Each ln In lines
            Dim x1 = ln.X1
            Dim y1 = ln.Y1
            Dim x2 = ln.X2
            Dim y2 = ln.Y2

            If x1 < minX Then minX = x1
            If x2 < minX Then minX = x2
            If y1 < minY Then minY = y1
            If y2 < minY Then minY = y2

            If x1 > maxX Then maxX = x1
            If x2 > maxX Then maxX = x2
            If y1 > maxY Then maxY = y1
            If y2 > maxY Then maxY = y2
        Next

        Return New Bounds2D(minX, minY, maxX, maxY)
    End Function


End Class
