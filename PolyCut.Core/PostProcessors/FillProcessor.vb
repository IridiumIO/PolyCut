Imports System.Numerics
Imports System.Windows.Shapes

Public Class FillProcessor : Implements IProcessor

    Public Shared Function FillLines(lines As List(Of GeoLine), density As Double, fillangle As Double) As List(Of GeoLine)

        Dim fills As New List(Of GeoLine) ' Thread-safe collection for storing results

        Dim traverseAngleRad = Math.PI * fillangle / 180 + (Math.PI / 2)
        Dim fillAngleRad = Math.PI * fillangle / 180

        ' Step 1: Calculate the bounding box of the shape
        Dim minX = lines.Min(Function(line) Math.Min(line.X1, line.X2))
        Dim minY = lines.Min(Function(line) Math.Min(line.Y1, line.Y2))
        Dim maxX = lines.Max(Function(line) Math.Max(line.X1, line.X2))
        Dim maxY = lines.Max(Function(line) Math.Max(line.Y1, line.Y2))
        Dim centerX = (minX + maxX) / 2
        Dim centerY = (minY + maxY) / 2

        ' Step 2: Calculate the maximum traverse extent and scale factor to create rays long enough to cover the shape
        Dim maxExtent = Math.Sqrt((maxX - minX) ^ 2 + (maxY - minY) ^ 2)
        Dim scaleFactor = 10 * Math.Max(maxX - minX, maxY - minY)

        ' Step 3: Traverse across the shape
        For traversePosition = -maxExtent To maxExtent Step density

            Dim rayStart = New Vector2(
                centerX + traversePosition * Math.Cos(traverseAngleRad),
                centerY + traversePosition * Math.Sin(traverseAngleRad)
            )


            Dim ray As New GeoLine(
                X1:=rayStart.X - scaleFactor * Math.Cos(fillAngleRad),
                Y1:=rayStart.Y - scaleFactor * Math.Sin(fillAngleRad),
                X2:=rayStart.X + scaleFactor * Math.Cos(fillAngleRad),
                Y2:=rayStart.Y + scaleFactor * Math.Sin(fillAngleRad)
            )

            ' Step 4: Get the valid segments of the ray inside the shape
            fills.AddRange(GetLinesWithinShape(lines, ray))
        Next


        Return fills
    End Function
    Public Shared Function GetLinesWithinShape(shapeBoundaries As List(Of GeoLine), ray As GeoLine) As List(Of GeoLine)

        Const interTol As Double = 0.000000001   ' intersection math tolerance intentioanlly small
        Const mergeTol As Double = 1.0           ' in already scaled units (0.01mm at 100,000x scaling)
        Dim mergeTol2 As Double = mergeTol * mergeTol

        Dim dir As Vector2 = ray.EndPoint - ray.StartPoint
        Dim dirLen2 As Double = dir.LengthSquared()
        If dirLen2 <= 0 OrElse shapeBoundaries Is Nothing OrElse shapeBoundaries.Count = 0 Then Return New List(Of GeoLine)


        ' Collect intersections with parameter t along the ray.
        Dim hits As New List(Of (t As Double, p As Vector2))(shapeBoundaries.Count \ 2)
        Dim rayStart As Vector2 = ray.StartPoint

        For Each edge In shapeBoundaries
            Dim hit = ray.GetIntersectionPointWith(edge, IncludeCoincidentIntersection:=False, tolerance:=interTol)
            If hit.HasValue Then
                Dim p As Vector2 = hit.Value
                Dim t As Double = Vector2.Dot(p - rayStart, dir) / dirLen2
                hits.Add((t, p))
            End If
        Next

        If hits.Count < 2 Then Return New List(Of GeoLine)
        hits.Sort(Function(a, b) a.t.CompareTo(b.t)) ' Sort along the ray.

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

        ' Tunable weight that converts angular difference into a distance-like penalty (units: squared distance).
        ' Increase to prefer directional continuation more strongly.
        Dim directionPreferenceWeight As Double = 2000.0

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
                    Dim dot As Double = Math.Max(-1.0, Math.Min(1.0, Vector2.Dot(prevNorm, candNorm)))
                    Dim alignment As Double = Math.Abs(dot) ' 1.0 means perfect alignment, 0 means perpendicular
                    ' penalty decreases with better alignment
                    directionPenalty = (1.0 - alignment) * directionPreferenceWeight
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

    Public Function IsShapeClosed(lines As List(Of Line)) As Boolean

        For i = 0 To lines.Count - 1
            For j = i To lines.Count - 1
                If i = j Then Continue For
                If lines(i).StartPoint = lines(j).EndPoint Then Return True
            Next
        Next
        Return False
    End Function


    Public Function Process(lines As List(Of Line), cfg As ProcessorConfiguration) As List(Of Line) Implements IProcessor.Process

        Dim scalingFactor = 100_000 'Define a scaling factor to offset the floating-point precision of working in millimetres. 1mm > 100m

        ' Respect per-element SVG fill presence when deciding to generate fills.
        Dim fillTag As Object = Nothing
        If lines IsNot Nothing AndAlso lines.Count > 0 Then
            fillTag = lines(0).Tag
        End If

        ' If explicit no-fill -> return outlines only
        If Not ShouldGenerateFill(fillTag) Then
            Return lines
        End If

        If Not IsShapeClosed(lines) OrElse cfg.DrawingConfig.FillType = FillType.None Then
            Return lines
        End If

        Dim spacingNullable As Double? = ComputeSpacingFromTag(fillTag, cfg)
        If Not spacingNullable.HasValue Then Return lines
        Dim spacing = spacingNullable.Value

        ' Continue with original fill generation using computed spacing
        Dim optimisedLines As New List(Of GeoLine)
        For Each ln In lines
            optimisedLines.Add(New GeoLine(ln.X1 * scalingFactor, ln.Y1 * scalingFactor, ln.X2 * scalingFactor, ln.Y2 * scalingFactor))
        Next

        Dim processedLines As New List(Of GeoLine)
        processedLines.AddRange(FillLines(optimisedLines, spacing * scalingFactor, cfg.DrawingConfig.ShadingAngle))

        If cfg.DrawingConfig.FillType = FillType.CrossHatch Then
            processedLines.AddRange(FillLines(optimisedLines, spacing * scalingFactor, cfg.DrawingConfig.ShadingAngle + 90))
        End If

        If cfg.OptimisedToolPath Then processedLines = OptimiseFills(processedLines, optimisedLines, cfg.DrawingConfig.AllowDrawingOverOutlines)

        If cfg.DrawingConfig.KeepOutlines Then
            If cfg.DrawingConfig.OutlinesBeforeFill Then
                processedLines.InsertRange(0, optimisedLines)
            Else
                processedLines.AddRange(optimisedLines)
            End If
        End If

        Dim finalLines As New List(Of Line)
        For Each ln In processedLines
            finalLines.Add(New Line With {
            .X1 = ln.X1 / scalingFactor,
            .Y1 = ln.Y1 / scalingFactor,
            .X2 = ln.X2 / scalingFactor,
            .Y2 = ln.Y2 / scalingFactor
        })
        Next

        Return finalLines

    End Function

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
End Class
