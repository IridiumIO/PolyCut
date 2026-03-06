Imports System.Numerics

Imports MeasurePerformance.IL.Weaver

Public Class FillOptimiser



    <MeasurePerformance>
    Public Shared Function Optimise(segments As List(Of List(Of GeoLine)), geometrybounds As List(Of GeoLine), allowTravelInOutlines As Boolean, cfg As ProcessorConfiguration, Optional preferDirection As Boolean = True, Optional startPoint As Nullable(Of Vector2) = Nothing) As List(Of GeoLine)

        If segments Is Nothing OrElse segments.Count = 0 Then Return New List(Of GeoLine)()

        ' Tolerances in scaled units (mm * 100000)
        Dim tolScaled As Double = Math.Max(0.001, cfg.Tolerance) * FillProcessor.DefaultScalingFactor


        Dim spacingMm As Double = Math.Max(0.0001, cfg.DrawingConfig.MaxStrokeWidth)
        Dim snapTol2 As Double = Math.Max(0.01, cfg.Tolerance * 2.0) * FillProcessor.DefaultScalingFactor
        snapTol2 *= snapTol2

        Dim outlineTol As Double = Math.Max(0.05, cfg.Tolerance * 2.0) * FillProcessor.DefaultScalingFactor
        Dim maxWalkDist As Double = Math.Max(spacingMm * 235.0, 1.0) * FillProcessor.DefaultScalingFactor
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
                        cost += (1.0 - (dot * dot) / (prevDirSq * candDirSq)) * FillProcessor.DirectionPreferenceWeight
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



End Class
