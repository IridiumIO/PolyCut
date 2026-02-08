Imports System.Numerics
Imports System.Windows

Module FillGeneratorShared

    Friend NotInheritable Class ShapeGridContext
        Public ReadOnly Edges As GeoLine()
        Public ReadOnly Grid As Dictionary(Of Long, List(Of Integer))
        Public ReadOnly InvCell As Double
        Public ReadOnly MaxGx As Integer

        Public ReadOnly Seen As Integer()
        Public Stamp As Integer

        Public Sub New(edges As GeoLine(), grid As Dictionary(Of Long, List(Of Integer)), invCell As Double, maxGx As Integer)
            Me.Edges = edges
            Me.Grid = grid
            Me.InvCell = invCell
            Me.MaxGx = maxGx
            Me.Seen = New Integer(edges.Length - 1) {}
            Me.Stamp = 0
        End Sub

        Public Function NextStamp() As Integer
            Stamp += 1
            If Stamp = Integer.MaxValue Then
                Stamp = 1
                Array.Clear(Seen, 0, Seen.Length)
            End If
            Return Stamp
        End Function
    End Class


    'TODO: ?switch to a spatial or quadtree for more dense/compex shapes
    Friend Function BuildShapeGrid(lines As List(Of GeoLine), density As Double) As ShapeGridContext
        Dim bounds As Rect = ComputeBounds(lines)

        Dim edges As GeoLine() = lines.ToArray()
        Dim edgeCount As Integer = edges.Length

        Dim shapeArea As Double = Math.Max(1.0, bounds.Width) * Math.Max(1.0, bounds.Height)

        ' density ./ area-derived cell size (scaled units)
        Dim cellSize As Double = Math.Max(density, Math.Sqrt(shapeArea / Math.Max(1, edgeCount)) * 2.0)
        Dim invCell As Double = 1.0 / cellSize

        Dim grid As New Dictionary(Of Long, List(Of Integer))(edgeCount * 2)
        Dim maxGx As Integer = Integer.MinValue

        For i = 0 To edgeCount - 1
            Dim e = edges(i)
            Dim gx0 = CInt(Math.Floor(Math.Min(e.X1, e.X2) * invCell))
            Dim gx1 = CInt(Math.Floor(Math.Max(e.X1, e.X2) * invCell))
            Dim gy0 = CInt(Math.Floor(Math.Min(e.Y1, e.Y2) * invCell))
            Dim gy1 = CInt(Math.Floor(Math.Max(e.Y1, e.Y2) * invCell))
            If gx1 > maxGx Then maxGx = gx1
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

        Return New ShapeGridContext(edges, grid, invCell, maxGx)
    End Function


    ' -------------------------
    ' Clipping
    ' -------------------------

    Friend Function ClipLinesAgainstShape(ctx As ShapeGridContext, line As GeoLine, isSegment As Boolean) As List(Of List(Of GeoLine))
        Dim mergeTol2 As Double = FillProcessor.MergeTolerance * FillProcessor.MergeTolerance

        Dim dir As Vector2 = line.EndPoint - line.StartPoint
        Dim dirLen2 As Double = dir.LengthSquared()
        If dirLen2 <= 0 OrElse ctx Is Nothing OrElse ctx.Edges Is Nothing OrElse ctx.Edges.Length = 0 Then
            Return New List(Of List(Of GeoLine))()
        End If

        Dim start As Vector2 = line.StartPoint

        ' Grid query range for this line's AABB. TODO use other AABB extension
        Dim eMinX As Double = Math.Min(CDbl(line.X1), CDbl(line.X2))
        Dim eMaxX As Double = Math.Max(CDbl(line.X1), CDbl(line.X2))
        Dim eMinY As Double = Math.Min(CDbl(line.Y1), CDbl(line.Y2))
        Dim eMaxY As Double = Math.Max(CDbl(line.Y1), CDbl(line.Y2))

        Dim qx0 = CInt(Math.Floor(eMinX * ctx.InvCell))
        Dim qx1 = CInt(Math.Floor(eMaxX * ctx.InvCell))
        Dim qy0 = CInt(Math.Floor(eMinY * ctx.InvCell))
        Dim qy1 = CInt(Math.Floor(eMaxY * ctx.InvCell))

        Dim stampInt = ctx.NextStamp()
        Dim hits As New List(Of (t As Double, p As Vector2))(16)

        For ix = qx0 To qx1
            For iy = qy0 To qy1
                Dim key = (CLng(ix) << 32) Xor (iy And &HFFFFFFFFL)
                Dim bucket As List(Of Integer) = Nothing
                If Not ctx.Grid.TryGetValue(key, bucket) Then Continue For

                For bi = 0 To bucket.Count - 1
                    Dim ei = bucket(bi)
                    If ctx.Seen(ei) = stampInt Then Continue For
                    ctx.Seen(ei) = stampInt

                    Dim edge = ctx.Edges(ei)
                    Dim hit = line.GetIntersectionPointWith(edge, IncludeCoincidentIntersection:=False, tolerance:=FillProcessor.IntersectionTolerance)
                    If Not hit.HasValue Then Continue For

                    Dim p As Vector2 = hit.Value
                    Dim t As Double = Vector2.Dot(p - start, dir) / dirLen2

                    If isSegment Then
                        If t >= -FillProcessor.IntersectionTolerance AndAlso t <= 1 + FillProcessor.IntersectionTolerance Then
                            hits.Add((t, p))
                        End If
                    Else
                        hits.Add((t, p))
                    End If
                Next
            Next
        Next

        If isSegment Then
            hits.Add((0.0, line.StartPoint))
            hits.Add((1.0, line.EndPoint))
        End If

        Return BuildSegmentsFromHits(hits, ctx, mergeTol2)
    End Function

    Friend Function BuildSegmentsFromHits(hits As List(Of (t As Double, p As Vector2)), ctx As ShapeGridContext, mergeTol2 As Double) As List(Of List(Of GeoLine))
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

            Dim midX As Double = (CDbl(a.X) + CDbl(b.X)) * 0.5
            Dim midY As Double = (CDbl(a.Y) + CDbl(b.Y)) * 0.5

            If IsInsideGrid(ctx, midX, midY) Then
                segments.Add(New List(Of GeoLine)(1) From {New GeoLine(a, b)})
            End If
        Next

        Return segments
    End Function



    ' -------------------------
    ' Point-in-polygon (grid version)
    ' -------------------------
    Friend Function IsInsideGrid(ctx As ShapeGridContext, px As Double, py As Double) As Boolean
        Dim stampId = ctx.NextStamp()

        Dim inside As Boolean = False
        Dim gy As Integer = CInt(Math.Floor(py * ctx.InvCell))
        Dim gxStart As Integer = CInt(Math.Floor(px * ctx.InvCell))

        For gx = gxStart To ctx.MaxGx
            Dim key = (CLng(gx) << 32) Xor (gy And &HFFFFFFFFL)
            Dim bucket As List(Of Integer) = Nothing
            If Not ctx.Grid.TryGetValue(key, bucket) Then Continue For

            For bi = 0 To bucket.Count - 1
                Dim ei = bucket(bi)
                If ctx.Seen(ei) = stampId Then Continue For
                ctx.Seen(ei) = stampId

                Dim e = ctx.Edges(ei)
                Dim y1 As Double = e.Y1, y2 As Double = e.Y2
                If y1 = y2 Then Continue For

                Dim yMin As Double, yMax As Double
                If y1 < y2 Then
                    yMin = y1
                    yMax = y2
                Else
                    yMin = y2
                    yMax = y1
                End If

                If py <= yMin OrElse py > yMax Then Continue For

                Dim xInt As Double = e.X1 + (py - y1) * (e.X2 - e.X1) / (y2 - y1)
                If xInt > px Then inside = Not inside
            Next
        Next

        Return inside
    End Function


    ' -------------------------
    ' Get Bounds
    ' -------------------------
    Friend Function ComputeBounds(lines As List(Of GeoLine)) As Rect
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

End Module
