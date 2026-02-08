Imports System.Numerics
Imports System.Windows
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

        Dim processedGeo As List(Of List(Of GeoLine)) = GenerateFill(outlineGeo, spacingScaled, cfg.DrawingConfig.FillType, cfg.DrawingConfig.ShadingAngle, cfg)

        Dim newGeo As List(Of GeoLine) = Nothing



        If cfg.OptimisedToolPath Then
            newGeo = OptimiseFills_Newest(processedGeo, outlineGeo, cfg.DrawingConfig.AllowDrawingOverOutlines, cfg)
        Else
            newGeo = processedGeo.SelectMany(Function(x) x).ToList()
        End If

        If cfg.DrawingConfig.KeepOutlines Then
            If cfg.DrawingConfig.OutlinesBeforeFill Then
                newGeo.InsertRange(0, outlineGeo)
            Else
                newGeo.AddRange(outlineGeo)
            End If
        End If

        Return ToWpfLines(newGeo, DefaultScalingFactor)
    End Function



    ' -------------------------
    ' Fill type generation
    ' -------------------------
    <MeasurePerformance>
    Private Shared Function GenerateFill(outline As List(Of GeoLine), spacingScaled As Double, fillType As FillType, angle As Double, cfg As ProcessorConfiguration) As List(Of List(Of GeoLine))
        Dim result As New List(Of List(Of GeoLine))

        Select Case fillType
            Case FillType.Spiral
                result.AddRange(GenerateSpiralFill(outline, spacingScaled, angle, cfg))
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
    Public Shared Function GenerateHatchFill(lines As List(Of GeoLine), density As Double, fillangle As Double) As List(Of List(Of GeoLine))
        Dim segments As New List(Of List(Of GeoLine))()
        If lines Is Nothing OrElse lines.Count = 0 OrElse density <= 0 Then Return segments

        Dim traverseAngleRad = Math.PI * fillangle / 180 + (Math.PI / 2)
        Dim fillAngleRad = Math.PI * fillangle / 180

        Dim bounds As Rect = ComputeBounds(lines)
        Dim centerX = (bounds.Left + bounds.Right) * 0.5
        Dim centerY = (bounds.Top + bounds.Bottom) * 0.5

        Dim dx = bounds.Right - bounds.Left
        Dim dy = bounds.Bottom - bounds.Top

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

            segments.AddRange(ClipLinesAgainstShape(lines, ray, isSegment:=False))
        Next

        Return segments
    End Function


    Private Shared Function GenerateTriangleAlignedHatchFill(lines As List(Of GeoLine), baseSpacing As Double, fillangle As Double) As List(Of List(Of GeoLine))

        If lines Is Nothing OrElse lines.Count = 0 OrElse baseSpacing <= 0 Then Return New List(Of List(Of GeoLine))()

        ' Triangular lattice: perpendicular spacing = S * sin(60) = S * √3/2
        Const Sin60 As Double = 0.86602540378444
        Dim densityEff As Double = baseSpacing * Sin60

        Return GenerateAlignedHatchFillCore(lines, densityEff, fillangle)
    End Function


    Private Shared Function GenerateSquareAlignedHatchFill(lines As List(Of GeoLine), baseSpacing As Double, fillangle As Double) As List(Of List(Of GeoLine))

        If lines Is Nothing OrElse lines.Count = 0 OrElse baseSpacing <= 0 Then Return New List(Of List(Of GeoLine))()

        Dim fillAngleRad = Math.PI * fillangle / 180.0
        Dim cosFill = Math.Cos(fillAngleRad)
        Dim sinFill = Math.Sin(fillAngleRad)

        ' Square lattice factor: 0/90 => 1, 45/135 => 1/√2
        Dim factor As Double = Math.Max(Math.Abs(sinFill), Math.Abs(cosFill))
        Dim densityEff As Double = baseSpacing * factor
        If densityEff <= 0 Then densityEff = baseSpacing

        Return GenerateAlignedHatchFillCore(lines, densityEff, fillangle)
    End Function


    Private Shared Function GenerateAlignedHatchFillCore(lines As List(Of GeoLine), densityEff As Double, fillangle As Double) As List(Of List(Of GeoLine))

        Dim fills As New List(Of List(Of GeoLine))
        If lines Is Nothing OrElse lines.Count = 0 OrElse densityEff <= 0 Then Return fills

        Dim fillAngleRad = Math.PI * fillangle / 180.0

        ' Fill direction u
        Dim cosFill = Math.Cos(fillAngleRad)
        Dim sinFill = Math.Sin(fillAngleRad)

        ' Traverse normal n
        Dim nx As Double = -sinFill
        Dim ny As Double = cosFill

        Dim bounds As Rect = ComputeBounds(lines)
        Dim centerX = (bounds.Left + bounds.Right) * 0.5
        Dim centerY = (bounds.Top + bounds.Bottom) * 0.5

        Dim dx = bounds.Right - bounds.Left
        Dim dy = bounds.Bottom - bounds.Top
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

            fills.AddRange(ClipLinesAgainstShape(lines, ray, isSegment:=False))
        Next

        Return fills
    End Function



    ' -------------------------
    ' Spiral fill
    ' -------------------------
    Public Shared Function GenerateSpiralFill(lines As List(Of GeoLine), density As Double, fillangle As Double, cfg As ProcessorConfiguration) As List(Of List(Of GeoLine))
        Dim outSegs As New List(Of List(Of GeoLine))()
        If lines Is Nothing OrElse lines.Count = 0 OrElse density <= 0 Then Return outSegs

        Dim snapTol2 As Double = density * density
        Dim flatTol As Double = Math.Max(0.001, cfg.Tolerance) * DefaultScalingFactor
        Dim mergeTol2 As Double = MergeTolerance * MergeTolerance

        Dim bounds As Rect = ComputeBounds(lines)
        Dim centerX = (bounds.Left + bounds.Right) / 2
        Dim centerY = (bounds.Top + bounds.Bottom) / 2
        Dim center As New Vector2(CSng(centerX), CSng(centerY))

        Dim maxRadius As Double = 0
        For Each ln In lines
            Dim d1 = Vector2.Distance(center, ln.StartPoint)
            Dim d2 = Vector2.Distance(center, ln.EndPoint)
            If d1 > maxRadius Then maxRadius = d1
            If d2 > maxRadius Then maxRadius = d2
        Next

        Dim b As Double = density / (2 * Math.PI)
        Dim bSq As Double = b * b
        Dim thetaOffset As Double = Math.PI * fillangle / 180
        Dim maxRadiusExtended = maxRadius + density

        ' Build spatial grid index over boundary edges 
        Dim edgeCount As Integer = lines.Count
        Dim edges As GeoLine() = lines.ToArray()

        Dim shapeArea As Double = Math.Max(1.0, bounds.Width) * Math.Max(1.0, bounds.Height)
        Dim cellSize As Double = Math.Max(density, Math.Sqrt(shapeArea / Math.Max(1, edgeCount)) * 2.0)
        Dim invCell As Double = 1.0 / cellSize

        Dim grid As New Dictionary(Of Long, List(Of Integer))(edgeCount * 2)
        Dim gridMaxGx As Integer = Integer.MinValue

        For i = 0 To edgeCount - 1
            Dim e = edges(i)
            Dim gx0 = CInt(Math.Floor(Math.Min(e.X1, e.X2) * invCell))
            Dim gx1 = CInt(Math.Floor(Math.Max(e.X1, e.X2) * invCell))
            Dim gy0 = CInt(Math.Floor(Math.Min(e.Y1, e.Y2) * invCell))
            Dim gy1 = CInt(Math.Floor(Math.Max(e.Y1, e.Y2) * invCell))
            If gx1 > gridMaxGx Then gridMaxGx = gx1
            For ix = gx0 To gx1
                For iy = gy0 To gy1
                    Dim key = (CLng(ix) << 32) Xor (iy And &HFFFFFFFFL)
                    Dim bucket As List(Of Integer) = Nothing
                    If Not grid.TryGetValue(key, bucket) Then
                        bucket = New List(Of Integer)(8)
                        grid(key) = bucket
                    End If
                    bucket.Add(i)
                Next
            Next
        Next

        Dim seen(edgeCount - 1) As Integer
        Dim stamp As Integer = 0

        ' Shape AABB for quick edge rejection
        Dim bMinX As Double = bounds.Left, bMaxX As Double = bounds.Right
        Dim bMinY As Double = bounds.Top, bMaxY As Double = bounds.Bottom

        Dim isInside As Boolean = IsPointInsideEvenOdd(center, lines)

        Dim theta As Double = 0
        Dim prevPoint As Vector2 = Nothing
        Dim havePrev As Boolean = False
        Dim currentSeg As List(Of GeoLine) = Nothing

        While True
            Dim r = b * theta
            If r > maxRadiusExtended Then Exit While

            Dim ang = theta + thetaOffset
            Dim p As New Vector2(
                center.X + CSng(r * Math.Cos(ang)),
                center.Y + CSng(r * Math.Sin(ang)))

            If havePrev Then
                Dim eMinX As Double = Math.Min(CDbl(prevPoint.X), CDbl(p.X))
                Dim eMaxX As Double = Math.Max(CDbl(prevPoint.X), CDbl(p.X))
                Dim eMinY As Double = Math.Min(CDbl(prevPoint.Y), CDbl(p.Y))
                Dim eMaxY As Double = Math.Max(CDbl(prevPoint.Y), CDbl(p.Y))

                If eMaxX < bMinX OrElse eMinX > bMaxX OrElse eMaxY < bMinY OrElse eMinY > bMaxY Then
                    ' Edge entirely outside shape AABB — definitely outside
                    isInside = False
                    If currentSeg IsNot Nothing AndAlso currentSeg.Count > 0 Then
                        outSegs.Add(currentSeg)
                        currentSeg = Nothing
                    End If
                Else
                    ' Find intersections using spatial grid
                    stamp += 1
                    If stamp = Integer.MaxValue Then
                        stamp = 1
                        Array.Clear(seen, 0, seen.Length)
                    End If

                    Dim spiralEdge As New GeoLine(prevPoint, p)
                    Dim sDir As Vector2 = p - prevPoint
                    Dim sDirLen2 As Double = sDir.LengthSquared()

                    Dim hits As New List(Of (t As Double, pt As Vector2))(4)
                    Dim qx0 = CInt(Math.Floor(eMinX * invCell))
                    Dim qx1 = CInt(Math.Floor(eMaxX * invCell))
                    Dim qy0 = CInt(Math.Floor(eMinY * invCell))
                    Dim qy1 = CInt(Math.Floor(eMaxY * invCell))

                    For ix = qx0 To qx1
                        For iy = qy0 To qy1
                            Dim key = (CLng(ix) << 32) Xor (iy And &HFFFFFFFFL)
                            Dim bucket As List(Of Integer) = Nothing
                            If Not grid.TryGetValue(key, bucket) Then Continue For
                            For bi = 0 To bucket.Count - 1
                                Dim ei = bucket(bi)
                                If seen(ei) = stamp Then Continue For
                                seen(ei) = stamp
                                Dim hit = spiralEdge.GetIntersectionPointWith(edges(ei), False, IntersectionTolerance)
                                If Not hit.HasValue Then Continue For
                                Dim hp = hit.Value
                                Dim t As Double = Vector2.Dot(hp - prevPoint, sDir) / sDirLen2
                                If t >= -IntersectionTolerance AndAlso t <= 1 + IntersectionTolerance Then
                                    hits.Add((t, hp))
                                End If
                            Next
                        Next
                    Next

                    If hits.Count = 0 Then
                        ' No boundary crossings = state unchanged, hot path
                        If isInside AndAlso sDirLen2 > mergeTol2 Then
                            If currentSeg Is Nothing Then
                                currentSeg = New List(Of GeoLine)(128)
                            ElseIf currentSeg.Count > 0 AndAlso
                                   currentSeg(currentSeg.Count - 1).EndPoint.DistanceToSquaredG(prevPoint) > snapTol2 Then
                                outSegs.Add(currentSeg)
                                currentSeg = New List(Of GeoLine)(128)
                            End If
                            currentSeg.Add(spiralEdge)
                        ElseIf Not isInside Then
                            If currentSeg IsNot Nothing AndAlso currentSeg.Count > 0 Then
                                outSegs.Add(currentSeg)
                                currentSeg = Nothing
                            End If
                        End If
                    Else
                        ' Has crossings - sort and build segments, then reset state via grid-accelerated PIP
                        hits.Sort(Function(a, hb) a.t.CompareTo(hb.t))

                        ' Build deduplicated crossing points
                        Dim pts As New List(Of Vector2)(hits.Count + 2)
                        pts.Add(prevPoint)
                        Dim lastPt As Vector2 = prevPoint
                        Dim crossings As Integer = 0
                        For Each h In hits
                            If Vector2.DistanceSquared(lastPt, h.pt) > mergeTol2 Then
                                pts.Add(h.pt)
                                lastPt = h.pt
                                crossings += 1
                            End If
                        Next
                        If Vector2.DistanceSquared(lastPt, p) > mergeTol2 Then
                            pts.Add(p)
                        End If

                        ' Emit inside segments using tracked state
                        Dim segments As New List(Of List(Of GeoLine))()
                        Dim state As Boolean = isInside
                        For i = 0 To pts.Count - 2
                            If state AndAlso Vector2.DistanceSquared(pts(i), pts(i + 1)) > mergeTol2 Then
                                segments.Add(New List(Of GeoLine)(1) From {New GeoLine(pts(i), pts(i + 1))})
                            End If
                            If i < crossings Then state = Not state
                        Next

                        StitchClippedPieces(segments, currentSeg, outSegs, snapTol2)

                        ' Reset tracked state with grid-accelerated PIP at endpoint
                        isInside = IsInsideGrid(CDbl(p.X), CDbl(p.Y), edges, grid, invCell, seen, stamp, gridMaxGx)
                    End If
                End If
            End If

            prevPoint = p
            havePrev = True

            '  adaptive stepping
            Dim rSq As Double = r * r
            Dim rSqPlusBSq As Double = rSq + bSq
            Dim ds As Double = Math.Sqrt(rSqPlusBSq)
            Dim rho As Double = (rSqPlusBSq * ds) / (rSq + 2.0 * bSq)
            Dim maxChord As Double = 2.0 * Math.Sqrt(2.0 * rho * flatTol)

            Dim dTheta As Double = If(ds > 0, maxChord / ds, 0.5)
            dTheta = Math.Clamp(dTheta, 0.01, 0.5)

            theta += dTheta
        End While

        If currentSeg IsNot Nothing AndAlso currentSeg.Count > 0 Then
            outSegs.Add(currentSeg)
        End If

        Debug.WriteLine(outSegs.Select(Function(s) s.Count).Sum().ToString() & " spiral fill segments generated.")
        Return outSegs
    End Function


    Private Shared Function IsInsideGrid(px As Double, py As Double, edges As GeoLine(), grid As Dictionary(Of Long, List(Of Integer)), invCell As Double, seen As Integer(), ByRef stamp As Integer, maxGx As Integer) As Boolean
        stamp += 1
        If stamp = Integer.MaxValue Then
            stamp = 1
            Array.Clear(seen, 0, seen.Length)
        End If

        Dim inside As Boolean = False
        Dim gy As Integer = CInt(Math.Floor(py * invCell))
        Dim gxStart As Integer = CInt(Math.Floor(px * invCell))

        For gx = gxStart To maxGx
            Dim key = (CLng(gx) << 32) Xor (gy And &HFFFFFFFFL)
            Dim bucket As List(Of Integer) = Nothing
            If Not grid.TryGetValue(key, bucket) Then Continue For

            For bi = 0 To bucket.Count - 1
                Dim ei = bucket(bi)
                If seen(ei) = stamp Then Continue For
                seen(ei) = stamp

                Dim e = edges(ei)
                Dim y1 As Double = e.Y1, y2 As Double = e.Y2
                If y1 = y2 Then Continue For

                Dim yMin As Double, yMax As Double
                If y1 < y2 Then yMin = y1 : yMax = y2 Else yMin = y2 : yMax = y1
                If py <= yMin OrElse py > yMax Then Continue For

                Dim xInt As Double = e.X1 + (py - y1) * (e.X2 - e.X1) / (y2 - y1)
                If xInt > px Then inside = Not inside
            Next
        Next

        Return inside
    End Function


    Private Shared Sub StitchClippedPieces(clipped As List(Of List(Of GeoLine)), ByRef currentSeg As List(Of GeoLine), outSegs As List(Of List(Of GeoLine)), snapTol2 As Double)
        If clipped.Count = 0 Then
            If currentSeg IsNot Nothing AndAlso currentSeg.Count > 0 Then
                outSegs.Add(currentSeg)
                currentSeg = Nothing
            End If
            Return
        End If

        For Each piece In clipped
            If piece Is Nothing OrElse piece.Count = 0 Then Continue For

            If currentSeg Is Nothing Then
                currentSeg = New List(Of GeoLine)(128)
                currentSeg.AddRange(piece)
                Continue For
            End If

            Dim curEnd As Vector2 = currentSeg(currentSeg.Count - 1).EndPoint
            Dim pieceStart As Vector2 = piece(0).StartPoint

            If curEnd.DistanceToSquaredG(pieceStart) <= snapTol2 Then
                currentSeg.AddRange(piece)
            Else
                outSegs.Add(currentSeg)
                currentSeg = New List(Of GeoLine)(128)
                currentSeg.AddRange(piece)
            End If
        Next
    End Sub



    ' -------------------------
    ' Radial fill
    ' -------------------------
    Public Shared Function GenerateRadialFill(lines As List(Of GeoLine), spacing As Double, angleDeg As Double) As List(Of List(Of GeoLine))
        Dim fills As New List(Of List(Of GeoLine))
        If lines Is Nothing OrElse lines.Count = 0 OrElse spacing <= 0 Then Return fills

        Dim bounds As Rect = ComputeBounds(lines)
        Dim cx As Double = (bounds.Left + bounds.Right) * 0.5
        Dim cy As Double = (bounds.Top + bounds.Bottom) * 0.5
        Dim center As New Vector2(CSng(cx), CSng(cy))

        ' Radius large enough to cover the whole shape
        Dim dx = bounds.Right - bounds.Left
        Dim dy = bounds.Bottom - bounds.Top
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

            fills.AddRange(ClipLinesAgainstShape(lines, ray, isSegment:=False))
        Next

        Return fills
    End Function



    ' -------------------------
    ' Clipping
    ' -------------------------
    Private Shared Function ClipLinesAgainstShape(shapeBoundaries As List(Of GeoLine), line As GeoLine, isSegment As Boolean) As List(Of List(Of GeoLine))
        Dim mergeTol2 As Double = MergeTolerance * MergeTolerance

        Dim dir As Vector2 = line.EndPoint - line.StartPoint
        Dim dirLen2 As Double = dir.LengthSquared()
        If dirLen2 <= 0 OrElse shapeBoundaries Is Nothing OrElse shapeBoundaries.Count = 0 Then
            Return New List(Of List(Of GeoLine))()
        End If

        Dim hits As New List(Of (t As Double, p As Vector2))(shapeBoundaries.Count \ 2 + If(isSegment, 2, 0))
        Dim start As Vector2 = line.StartPoint

        For Each edge In shapeBoundaries
            Dim hit = line.GetIntersectionPointWith(edge, IncludeCoincidentIntersection:=False, tolerance:=IntersectionTolerance)
            If Not hit.HasValue Then Continue For

            Dim p As Vector2 = hit.Value
            Dim t As Double = Vector2.Dot(p - start, dir) / dirLen2

            If isSegment Then
                If t >= -IntersectionTolerance AndAlso t <= 1 + IntersectionTolerance Then hits.Add((t, p))
            Else
                hits.Add((t, p))
            End If
        Next

        If isSegment Then
            hits.Add((0.0, line.StartPoint))
            hits.Add((1.0, line.EndPoint))
        End If

        Return BuildSegmentsFromHits(hits, shapeBoundaries, mergeTol2)
    End Function


    Private Shared Function BuildSegmentsFromHits(hits As List(Of (t As Double, p As Vector2)), shapeBoundaries As List(Of GeoLine), mergeTol2 As Double) As List(Of List(Of GeoLine))
        If hits Is Nothing OrElse hits.Count < 2 Then Return New List(Of List(Of GeoLine))()

        hits.Sort(Function(a, b) a.t.CompareTo(b.t))

        ' Merge near-duplicate intersections
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

        If pts.Count < 2 Then Return New List(Of List(Of GeoLine))()

        Dim segments As New List(Of List(Of GeoLine))()

        For i As Integer = 0 To pts.Count - 2
            Dim a As Vector2 = pts(i)
            Dim b As Vector2 = pts(i + 1)

            If Vector2.DistanceSquared(a, b) <= mergeTol2 Then Continue For

            Dim mid As New Vector2((a.X + b.X) * 0.5F, (a.Y + b.Y) * 0.5F)
            If IsPointInsideEvenOdd(mid, shapeBoundaries) Then
                segments.Add(New List(Of GeoLine)(1) From {New GeoLine(a, b)})
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

    'Public Shared Function OptimiseFills_Old(lines As List(Of GeoLine), geometrybounds As List(Of GeoLine), allowTravelInOutlines As Boolean, Optional preferDirection As Boolean = True, Optional startPoint As Nullable(Of Vector2) = Nothing) As List(Of GeoLine)

    '    Dim workingLines As New List(Of GeoLine)(lines)
    '    Dim currentPoint As Vector2
    '    If startPoint.HasValue Then
    '        currentPoint = startPoint.Value
    '    ElseIf workingLines.Count > 0 Then
    '        currentPoint = workingLines(0).StartPoint
    '    Else
    '        currentPoint = New Vector2(0, 0)
    '    End If

    '    Dim optimisedLines As New List(Of GeoLine)

    '    ' Keep track of the previous drawn direction so we can prefer candidates that continue the same vector.
    '    Dim previousDirection As Vector2 = New Vector2(0, 0)
    '    Dim havePreviousDirection As Boolean = False


    '    While workingLines.Count > 0
    '        Dim bestLine As GeoLine = Nothing
    '        Dim bestCost As Double = Double.MaxValue
    '        Dim bestIsReversed As Boolean = False

    '        ' Find the best next line using a combined metric: distance^2 + (directionPenalty)
    '        For Each line In workingLines

    '            Dim startDistance As Double = Vector2.DistanceSquared(currentPoint, line.StartPoint)
    '            Dim endDistance As Double = Vector2.DistanceSquared(currentPoint, line.EndPoint)

    '            ' assume we'll travel to the nearer endpoint
    '            Dim chosenDistance As Double
    '            Dim candidateDir As Vector2

    '            If startDistance <= endDistance Then
    '                chosenDistance = startDistance
    '                candidateDir = line.EndPoint - line.StartPoint
    '            Else
    '                chosenDistance = endDistance
    '                candidateDir = line.StartPoint - line.EndPoint
    '            End If

    '            Dim directionPenalty As Double = 0.0

    '            If preferDirection AndAlso havePreviousDirection AndAlso candidateDir.LengthSquared() > 0 Then
    '                Dim candNorm As Vector2 = Vector2.Normalize(candidateDir)
    '                Dim prevNorm As Vector2 = Vector2.Normalize(previousDirection)
    '                ' dot in [-1,1]; We use absolute dot so opposite-direction (same line vector reversed) also counts as aligned.
    '                Dim dot As Double = Vector2.Dot(prevNorm, candNorm)
    '                If dot < -1.0 Then dot = -1.0
    '                If dot > 1.0 Then dot = 1.0

    '                Dim alignment As Double = Math.Abs(dot) ' 1.0 means perfect alignment, 0 means perpendicular
    '                ' penalty decreases with better alignment
    '                directionPenalty = (1.0 - alignment) * DirectionPreferenceWeight
    '            End If

    '            Dim totalCost As Double = chosenDistance + directionPenalty

    '            If totalCost < bestCost Then
    '                bestCost = totalCost
    '                bestLine = line
    '                bestIsReversed = If(startDistance <= endDistance, False, True)
    '            End If

    '        Next

    '        ' If bestLine is nothing then break (shouldn't happen)
    '        If bestLine Is Nothing Then Exit While

    '        ' Compute fractional travel line
    '        Dim fractionalLine As New GeoLine(currentPoint, If(bestIsReversed, bestLine.EndPoint, bestLine.StartPoint))

    '        If allowTravelInOutlines AndAlso fractionalLine.IsLineOnAnyLine(geometrybounds, 100) Then
    '            optimisedLines.Add(fractionalLine)
    '        End If

    '        ' Append the selected line (respecting chosen orientation)
    '        If bestIsReversed Then
    '            optimisedLines.Add(bestLine.Reverse())
    '            ' Update currentPoint and previousDirection
    '            previousDirection = bestLine.StartPoint - bestLine.EndPoint
    '            currentPoint = bestLine.StartPoint
    '        Else
    '            optimisedLines.Add(bestLine)
    '            previousDirection = bestLine.EndPoint - bestLine.StartPoint
    '            currentPoint = bestLine.EndPoint
    '        End If

    '        havePreviousDirection = previousDirection.LengthSquared() > 0

    '        workingLines.Remove(bestLine)

    '    End While

    '    Return optimisedLines

    'End Function



    'Public Shared Function OptimiseFills(lines As List(Of GeoLine), geometrybounds As List(Of GeoLine), allowTravelInOutlines As Boolean, cfg As ProcessorConfiguration, Optional preferDirection As Boolean = True, Optional startPoint As Nullable(Of Vector2) = Nothing) As List(Of GeoLine)

    '    If lines Is Nothing OrElse lines.Count = 0 Then Return New List(Of GeoLine)

    '    ' Step 1: Group connected lines into fill segments
    '    Dim segments As List(Of List(Of GeoLine)) = BuildFillSegments(lines)

    '    If segments.Count = 0 Then Return New List(Of GeoLine)

    '    ' Step 2: Optimize segment order (much faster than optimizing individual lines)
    '    Dim currentPoint As Vector2
    '    If startPoint.HasValue Then
    '        currentPoint = startPoint.Value
    '    ElseIf segments(0).Count > 0 Then
    '        currentPoint = segments(0)(0).StartPoint
    '    Else
    '        currentPoint = New Vector2(0, 0)
    '    End If

    '    Dim optimisedLines As New List(Of GeoLine)(lines.Count)
    '    Dim workingSegments As New HashSet(Of List(Of GeoLine))(segments)

    '    ' Keep track of previous direction for segment ordering
    '    Dim previousDirection As Vector2 = New Vector2(0, 0)
    '    Dim havePreviousDirection As Boolean = False
    '    Dim prevNorm As Vector2 = New Vector2(0, 0)

    '    While workingSegments.Count > 0
    '        Dim bestSegment As List(Of GeoLine) = Nothing
    '        Dim bestCost As Double = Double.MaxValue
    '        Dim bestReverse As Boolean = False
    '        Dim foundExactMatch As Boolean = False

    '        ' Find the best next segment to draw
    '        For Each segment In workingSegments
    '            If segment.Count = 0 Then Continue For

    '            Dim segStart As Vector2 = segment(0).StartPoint
    '            Dim segEnd As Vector2 = segment(segment.Count - 1).EndPoint

    '            Dim startDist As Double = Vector2.DistanceSquared(currentPoint, segStart)
    '            Dim endDist As Double = Vector2.DistanceSquared(currentPoint, segEnd)

    '            ' Prioritize exact matches (segment connects directly)
    '            If startDist < 1.0 Then
    '                bestSegment = segment
    '                bestReverse = False
    '                foundExactMatch = True
    '                Exit For
    '            ElseIf endDist < 1.0 Then
    '                bestSegment = segment
    '                bestReverse = True
    '                foundExactMatch = True
    '                Exit For
    '            End If

    '            ' Choose orientation and calculate cost
    '            Dim chosenDist As Double
    '            Dim segmentDir As Vector2
    '            Dim shouldReverse As Boolean

    '            If startDist <= endDist Then
    '                chosenDist = startDist
    '                segmentDir = segEnd - segStart
    '                shouldReverse = False
    '            Else
    '                chosenDist = endDist
    '                segmentDir = segStart - segEnd
    '                shouldReverse = True
    '            End If

    '            Dim directionPenalty As Double = 0.0

    '            If preferDirection AndAlso havePreviousDirection AndAlso segmentDir.LengthSquared() > 0 Then
    '                Dim segNorm As Vector2 = Vector2.Normalize(segmentDir)
    '                Dim dot As Double = Math.Clamp(Vector2.Dot(prevNorm, segNorm), -1.0, 1.0)
    '                Dim alignment As Double = Math.Abs(dot)
    '                directionPenalty = (1.0 - alignment) * DirectionPreferenceWeight
    '            End If

    '            Dim totalCost As Double = chosenDist + directionPenalty

    '            If totalCost < bestCost Then
    '                bestCost = totalCost
    '                bestSegment = segment
    '                bestReverse = shouldReverse
    '            End If
    '        Next

    '        If bestSegment Is Nothing Then Exit While

    '        ' Add travel line if needed
    '        Dim travelStart As Vector2 = If(bestReverse, bestSegment(bestSegment.Count - 1).EndPoint, bestSegment(0).StartPoint)

    '        If Not foundExactMatch AndAlso allowTravelInOutlines Then
    '            Dim fractionalLine As New GeoLine(currentPoint, travelStart)
    '            If fractionalLine.IsLineOnAnyLine(geometrybounds, 100) Then
    '                optimisedLines.Add(fractionalLine)
    '            End If
    '        End If

    '        ' Add entire segment (reversed if necessary)
    '        If bestReverse Then
    '            For i As Integer = bestSegment.Count - 1 To 0 Step -1
    '                optimisedLines.Add(bestSegment(i).Reverse())
    '            Next
    '            currentPoint = bestSegment(0).StartPoint
    '            previousDirection = bestSegment(0).StartPoint - bestSegment(bestSegment.Count - 1).EndPoint
    '        Else
    '            For Each line In bestSegment
    '                optimisedLines.Add(line)
    '            Next
    '            currentPoint = bestSegment(bestSegment.Count - 1).EndPoint
    '            previousDirection = bestSegment(bestSegment.Count - 1).EndPoint - bestSegment(0).StartPoint
    '        End If

    '        ' Update cached direction
    '        If previousDirection.LengthSquared() > 0 Then
    '            prevNorm = Vector2.Normalize(previousDirection)
    '            havePreviousDirection = True
    '        End If

    '        workingSegments.Remove(bestSegment)
    '    End While

    '    Return optimisedLines
    'End Function

    '' Helper function to group connected lines into segments
    'Private Shared Function BuildFillSegments(lines As List(Of GeoLine)) As List(Of List(Of GeoLine))
    '    Dim segments As New List(Of List(Of GeoLine))
    '    Dim remaining As New HashSet(Of GeoLine)(lines)

    '    Const ConnectionTolerance As Double = 1.0 ' Squared distance for "connected"

    '    While remaining.Count > 0
    '        Dim segment As New List(Of GeoLine)
    '        Dim currentLine As GeoLine = remaining.First()
    '        remaining.Remove(currentLine)
    '        segment.Add(currentLine)

    '        Dim currentEnd As Vector2 = currentLine.EndPoint
    '        Dim foundConnection As Boolean = True

    '        ' Keep extending the segment while we find connected lines
    '        While foundConnection AndAlso remaining.Count > 0
    '            foundConnection = False

    '            ' Look for a line that starts where this segment ends
    '            For Each candidate In remaining
    '                If Vector2.DistanceSquared(currentEnd, candidate.StartPoint) < ConnectionTolerance Then
    '                    segment.Add(candidate)
    '                    currentEnd = candidate.EndPoint
    '                    remaining.Remove(candidate)
    '                    foundConnection = True
    '                    Exit For
    '                End If
    '            Next
    '        End While

    '        segments.Add(segment)
    '    End While

    '    Return segments
    'End Function
    <MeasurePerformance>
    Public Shared Function OptimiseFills_Newest(segments As List(Of List(Of GeoLine)), geometrybounds As List(Of GeoLine), allowTravelInOutlines As Boolean, cfg As ProcessorConfiguration, Optional preferDirection As Boolean = True, Optional startPoint As Nullable(Of Vector2) = Nothing) As List(Of GeoLine)

        If segments Is Nothing OrElse segments.Count = 0 Then Return New List(Of GeoLine)()

        ' Tolerances in scaled units (mm * 100000)
        Dim spacingMm As Double = Math.Max(0.0001, cfg.DrawingConfig.MaxStrokeWidth)
        Dim snapTol2 As Double = Math.Max(0.01, spacingMm * 0.05) * DefaultScalingFactor
        snapTol2 *= snapTol2

        Dim outlineTol As Double = Math.Max(0.05, spacingMm * 0.2) * DefaultScalingFactor
        Dim maxWalkDist As Double = Math.Max(spacingMm * 5.0, 1.0) * DefaultScalingFactor
        Dim maxWalkDist2 As Double = maxWalkDist * maxWalkDist

        ' Initialize starting point
        Dim currentPoint As Vector2
        If startPoint.HasValue Then
            currentPoint = startPoint.Value
        ElseIf segments(0) IsNot Nothing AndAlso segments(0).Count > 0 Then
            currentPoint = segments(0)(0).StartPoint
        Else
            currentPoint = New Vector2(0, 0)
        End If

        ' Precompute segment data and count remaining in a single pass
        Dim m As Integer = segments.Count
        Dim segData(m - 1) As (start As Vector2, endPt As Vector2, startDir As Vector2, endDir As Vector2)
        Dim used(m - 1) As Boolean
        Dim totalLines As Integer = 0
        Dim remaining As Integer = 0

        For i = 0 To m - 1
            Dim seg = segments(i)
            If seg Is Nothing OrElse seg.Count = 0 Then
                segData(i) = (New Vector2(0, 0), New Vector2(0, 0), New Vector2(0, 0), New Vector2(0, 0))
                used(i) = True
            Else
                Dim first = seg(0)
                Dim last = seg(seg.Count - 1)
                segData(i) = (first.StartPoint, last.EndPoint, first.EndPoint - first.StartPoint, last.EndPoint - last.StartPoint)
                totalLines += seg.Count
                remaining += 1
            End If
        Next

        Dim result As New List(Of GeoLine)(totalLines + m \ 2)
        Dim prevDir As Vector2 = New Vector2(0, 0)
        Dim prevDirSq As Double = 0

        While remaining > 0
            Dim bestIdx As Integer = -1
            Dim bestReverse As Boolean = False
            Dim bestCost As Double = Double.MaxValue
            Dim foundExact As Boolean = False

            For i = 0 To m - 1
                If used(i) Then Continue For

                Dim dStart As Double = currentPoint.DistanceToSquaredG(segData(i).start)
                Dim dEnd As Double = currentPoint.DistanceToSquaredG(segData(i).endPt)

                If dStart <= snapTol2 Then
                    bestIdx = i : bestReverse = False : foundExact = True : Exit For
                ElseIf dEnd <= snapTol2 Then
                    bestIdx = i : bestReverse = True : foundExact = True : Exit For
                End If

                Dim dist As Double
                Dim dirToUse As Vector2
                Dim shouldReverse As Boolean

                If dStart <= dEnd Then
                    dist = dStart : dirToUse = segData(i).startDir : shouldReverse = False
                Else
                    dist = dEnd : dirToUse = -segData(i).endDir : shouldReverse = True
                End If

                Dim cost As Double = dist
                If preferDirection AndAlso prevDirSq > 0 Then
                    Dim candDirSq As Double = dirToUse.LengthSquared()
                    If candDirSq > 0 Then
                        Dim dot As Double = Vector2.Dot(prevDir, dirToUse)
                        cost += (1.0 - (dot * dot) / (prevDirSq * candDirSq)) * DirectionPreferenceWeight
                    End If
                End If

                If cost < bestCost Then
                    bestCost = cost : bestIdx = i : bestReverse = shouldReverse
                End If
            Next

            If bestIdx < 0 Then Exit While

            ' Travel along outline if needed
            If allowTravelInOutlines AndAlso Not foundExact Then
                Dim nextStart As Vector2 = If(bestReverse, segData(bestIdx).endPt, segData(bestIdx).start)
                Dim d2 As Double = currentPoint.DistanceToSquaredG(nextStart)
                If d2 > 0 AndAlso d2 <= maxWalkDist2 Then
                    Dim travelPath = WalkOutline(currentPoint, nextStart, geometrybounds, outlineTol, maxWalkDist)
                    If travelPath IsNot Nothing Then result.AddRange(travelPath)
                End If
            End If

            ' Emit segment
            Dim chosenSeg = segments(bestIdx)
            If bestReverse Then
                For k = chosenSeg.Count - 1 To 0 Step -1
                    result.Add(chosenSeg(k).Reverse())
                Next
                currentPoint = segData(bestIdx).start
                prevDir = segData(bestIdx).startDir
            Else
                result.AddRange(chosenSeg)
                currentPoint = segData(bestIdx).endPt
                prevDir = segData(bestIdx).endDir
            End If

            prevDirSq = prevDir.LengthSquared()
            used(bestIdx) = True
            remaining -= 1
        End While

        Return result
    End Function


    Private Shared Function WalkOutline(fromP As Vector2, toP As Vector2, bounds As List(Of GeoLine), tol As Double, maxDist As Double) As List(Of GeoLine)
        If bounds Is Nothing OrElse bounds.Count = 0 Then Return Nothing

        Dim tol2 As Double = tol * tol
        Dim n As Integer = bounds.Count

        ' Find closest outline segment for each point by projection
        Dim fromIdx As Integer = -1
        Dim toIdx As Integer = -1
        Dim projFrom As Vector2 = fromP
        Dim projTo As Vector2 = toP
        Dim bestFromD2 As Double = tol2
        Dim bestToD2 As Double = tol2

        For i = 0 To n - 1
            Dim seg = bounds(i)
            Dim dx As Double = seg.X2 - seg.X1
            Dim dy As Double = seg.Y2 - seg.Y1
            Dim len2 As Double = dx * dx + dy * dy

            Dim tParam As Double
            If len2 < 0.0000000001 Then
                tParam = 0.0
            Else
                Dim invLen2 As Double = 1.0 / len2

                tParam = Math.Clamp(((fromP.X - seg.X1) * dx + (fromP.Y - seg.Y1) * dy) * invLen2, 0.0, 1.0)
                Dim pf As New Vector2(CSng(seg.X1 + tParam * dx), CSng(seg.Y1 + tParam * dy))
                Dim df2 As Double = fromP.DistanceToSquaredG(pf)
                If df2 < bestFromD2 Then
                    bestFromD2 = df2 : fromIdx = i : projFrom = pf
                End If

                tParam = Math.Clamp(((toP.X - seg.X1) * dx + (toP.Y - seg.Y1) * dy) * invLen2, 0.0, 1.0)
            End If

            Dim pt As New Vector2(CSng(seg.X1 + tParam * dx), CSng(seg.Y1 + tParam * dy))
            Dim dt2 As Double = toP.DistanceToSquaredG(pt)
            If dt2 < bestToD2 Then
                bestToD2 = dt2 : toIdx = i : projTo = pt
            End If
        Next

        If fromIdx < 0 OrElse toIdx < 0 Then Return Nothing

        Dim directDist As Double = Math.Sqrt(projFrom.DistanceToSquaredG(projTo))
        If directDist < tol * 0.1 Then Return Nothing

        ' Same segment: direct line between original points
        If fromIdx = toIdx Then
            Return New List(Of GeoLine) From {New GeoLine(fromP, toP)}
        End If

        ' Try both walk directions — WalkDirection validates connectivity per-step
        Dim fwd = WalkDirection(projFrom, projTo, bounds, fromIdx, toIdx, tol2, True, maxDist, directDist)
        Dim bwd = WalkDirection(projFrom, projTo, bounds, fromIdx, toIdx, tol2, False, maxDist, directDist)

        ' Pick shorter path
        Dim path As List(Of GeoLine)
        If fwd Is Nothing Then
            path = bwd
        ElseIf bwd Is Nothing Then
            path = fwd
        Else
            Dim lenFwd As Double = 0
            For Each seg In fwd : lenFwd += seg.Length : Next
            Dim lenBwd As Double = 0
            For Each seg In bwd : lenBwd += seg.Length : Next
            path = If(lenFwd <= lenBwd, fwd, bwd)
        End If

        ' Snap endpoints to original from/to points to close projection gaps
        If path IsNot Nothing AndAlso path.Count > 0 Then
            Dim lastIdx As Integer = path.Count - 1
            path(0) = New GeoLine(fromP, path(0).EndPoint)
            path(lastIdx) = New GeoLine(path(lastIdx).StartPoint, toP)
        End If

        Return path
    End Function


    Private Shared Function WalkDirection(projFrom As Vector2, projTo As Vector2, bounds As List(Of GeoLine), fromIdx As Integer, toIdx As Integer, tol2 As Double, forward As Boolean, maxDist As Double, directDist As Double) As List(Of GeoLine)
        Dim path As New List(Of GeoLine)
        Dim maxRatio As Double = If(directDist < 1000, 8.0, 4.0)
        Dim walked As Double = 0
        Dim n As Integer = bounds.Count
        Dim minSegLen2 As Double = tol2 * 0.0001

        Dim p As Vector2 = projFrom
        Dim idx As Integer = fromIdx

        For step_i As Integer = 0 To n
            If idx = toIdx Then
                Dim d2 As Double = p.DistanceToSquaredG(projTo)
                If d2 > tol2 Then
                    walked += Math.Sqrt(d2)
                    If walked > maxDist OrElse walked > directDist * maxRatio Then Return Nothing
                    path.Add(New GeoLine(p, projTo))
                End If
                Return path
            End If

            Dim seg As GeoLine = bounds(idx)
            Dim exitVertex As Vector2

            If step_i = 0 Then
                exitVertex = If(forward, seg.EndPoint, seg.StartPoint)
            ElseIf p.DistanceToSquaredG(seg.StartPoint) < p.DistanceToSquaredG(seg.EndPoint) Then
                exitVertex = seg.EndPoint
            Else
                exitVertex = seg.StartPoint
            End If

            Dim segLen2 As Double = p.DistanceToSquaredG(exitVertex)
            walked += Math.Sqrt(segLen2)
            If walked > maxDist OrElse walked > directDist * maxRatio Then Return Nothing

            If segLen2 > minSegLen2 Then
                path.Add(New GeoLine(p, exitVertex))
            End If

            Dim nextIdx As Integer = If(forward, (idx + 1) Mod n, (idx - 1 + n) Mod n)

            ' Verify connectivity to next segment
            Dim nextSeg As GeoLine = bounds(nextIdx)
            If exitVertex.DistanceToSquaredG(nextSeg.StartPoint) > tol2 AndAlso
               exitVertex.DistanceToSquaredG(nextSeg.EndPoint) > tol2 Then
                Return Nothing
            End If

            p = exitVertex
            idx = nextIdx
        Next

        Return Nothing
    End Function


    ' -------------------------
    ' Closure test
    ' -------------------------
    Public Shared Function IsShapeClosed(lines As List(Of Line)) As Boolean

        'TODO: Test this then implement it!~!!!!
        '-----------------------------------
        '-----------------------------------

        'If lines Is Nothing OrElse lines.Count < 2 Then Return False

        '' Build a set of all EndPoints, then check if any StartPoint matches
        'Dim endPoints As New HashSet(Of Point)
        'For Each ln In lines
        '    endPoints.Add(ln.EndPoint())
        'Next

        'For Each ln In lines
        '    If endPoints.Contains(ln.StartPoint()) Then Return True
        'Next

        'Return False


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



    Private Shared Function ComputeBounds(lines As List(Of GeoLine)) As Rect
        If lines Is Nothing OrElse lines.Count = 0 Then Return New Rect(0, 0, 0, 0)

        Dim minX = Double.PositiveInfinity
        Dim minY = Double.PositiveInfinity
        Dim maxX = Double.NegativeInfinity
        Dim maxY = Double.NegativeInfinity

        For Each ln In lines
            Dim x1 = ln.X1, y1 = ln.Y1
            Dim x2 = ln.X2, y2 = ln.Y2

            minX = Math.Min(minX, Math.Min(x1, x2))
            minY = Math.Min(minY, Math.Min(y1, y2))
            maxX = Math.Max(maxX, Math.Max(x1, x2))
            maxY = Math.Max(maxY, Math.Max(y1, y2))
        Next

        Return New Rect(minX, minY, maxX - minX, maxY - minY)
    End Function


End Class
