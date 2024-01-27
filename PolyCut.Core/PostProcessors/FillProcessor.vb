Imports System.Windows
Imports System.Windows.Shapes

Imports MeasurePerformance.IL.Weaver

Public Class FillProcessor : Implements IProcessor
    <MeasurePerformance>
    Public Shared Function FillLines(lines As List(Of Line), density As Double, fillangle As Double) As List(Of Line)


        Dim fills As New List(Of Line)

        Dim minX As Double = Double.PositiveInfinity
        Dim minY As Double = Double.PositiveInfinity
        Dim maxX As Double = Double.NegativeInfinity
        Dim maxY As Double = Double.NegativeInfinity

        For Each line In lines
            minX = Math.Min(minX, Math.Min(line.X1, line.X2))
            minY = Math.Min(minY, Math.Min(line.Y1, line.Y2))
            maxX = Math.Max(maxX, Math.Max(line.X1, line.X2))
            maxY = Math.Max(maxY, Math.Max(line.Y1, line.Y2))
        Next

        'We fudge the bounding box by a fraction of a pixel to ensure the centerpoint doesn't intersect the exact corner of squares. 
        Dim boundingBox As New Rect(New Point(minX, minY), New Point(maxX, maxY))
        Dim centerP As New Point(boundingBox.X + boundingBox.Width / 2 + 0.001, boundingBox.Y + boundingBox.Height / 2 + 0.001)

        Dim safeLength As Double = Math.Sqrt((maxX - minX) ^ 2 + (maxY - minY) ^ 2)

        Dim traverseangle = Math.PI * fillangle / 180 + (Math.PI / 2)
        fillangle = Math.PI * fillangle / 180


        'Fill the first half of the shape, starting from the center and moving in the positive direction
        While boundingBox.Contains(centerP) OrElse centerP.RaycastIntersectsWithShape(lines, fillangle, safeLength)
            Dim endpoint As New Point(centerP.X + safeLength * Math.Cos(fillangle), centerP.Y + safeLength * Math.Sin(fillangle))
            Dim startpoint As New Point(centerP.X - safeLength * Math.Cos(fillangle), centerP.Y - safeLength * Math.Sin(fillangle))

            fills.Add(startpoint.LineTo(endpoint))

            centerP.X += (density * Math.Cos(traverseangle))
            centerP.Y += (density * Math.Sin(traverseangle))

        End While


        'Resent the centerpoint, and offset it by the density in the opposite direction
        centerP = New Point(boundingBox.X + boundingBox.Width / 2, boundingBox.Y + boundingBox.Height / 2)
        centerP.X -= (density * Math.Cos(traverseangle))
        centerP.Y -= (density * Math.Sin(traverseangle))


        'Fill the second half of the shape, starting from the center and moving in the negative direction
        While boundingBox.Contains(centerP) OrElse centerP.RaycastIntersectsWithShape(lines, fillangle, safeLength)
            Dim endpoint As New Point(centerP.X + safeLength * Math.Cos(fillangle), centerP.Y + safeLength * Math.Sin(fillangle))
            Dim startpoint As New Point(centerP.X - safeLength * Math.Cos(fillangle), centerP.Y - safeLength * Math.Sin(fillangle))

            fills.Add(startpoint.LineTo(endpoint))

            centerP.X -= (density * Math.Cos(traverseangle))
            centerP.Y -= (density * Math.Sin(traverseangle))

        End While


        Dim clippedLines As New List(Of Line)

        For Each l In fills
            clippedLines.AddRange(GetLinesWithinShape(lines, l))
        Next

        Return clippedLines.ToList
    End Function
    Public Shared Function GetLinesWithinShape(shapeBoundaries As List(Of Line), ray As Line) As List(Of Line)

        Dim intersectionPoints As New List(Of Point)

        For Each line In shapeBoundaries
            Dim intersection = ray.GetIntersectionPointWith(line)
            If intersection IsNot Nothing Then
                intersectionPoints.Add(intersection)

            End If
        Next

        Dim segments As New List(Of Line)

        intersectionPoints = intersectionPoints.OrderBy(Function(p) p.X).ThenBy(Function(p) p.Y).ToList()

        For i As Integer = 0 To intersectionPoints.Count - 2
            Dim segment As Line = intersectionPoints(i).LineTo(intersectionPoints(i + 1))

            'Naive filtering method; in most cases, a line that passes through a shape must intersect the shape an even number of times
            'but this does not account for lines that pass through a shape's corners, which will intersect the shape an odd number of times
            'Probably won't be an issue since we're using Doubles.
            If i Mod 2 = 0 Then
                segments.Add(segment)
            End If
        Next
        Return segments

    End Function


    <MeasurePerformance>
    Public Shared Function OptimiseFills(lines As List(Of Line))

        Dim fractionalPaths As Integer = 0

        Dim workingLines As New List(Of Line)(lines)

        Dim currentPoint As New Point(0, 0)

        Dim optimisedLines As New List(Of Line)


        While workingLines.Count > 0

            Dim nearestLine As Line = Nothing
            Dim nearestDistance As Double = Double.MaxValue

            For Each line In workingLines

                Dim startDistance As Double = currentPoint.DistanceTo(line.StartPoint)
                Dim endDistance As Double = currentPoint.DistanceTo(line.EndPoint)

                If startDistance < nearestDistance Or endDistance < nearestDistance Then
                    nearestLine = line
                    nearestDistance = Math.Min(startDistance, endDistance)
                End If
            Next

            If currentPoint.DistanceTo(nearestLine.StartPoint) < currentPoint.DistanceTo(nearestLine.EndPoint) Then

                If currentPoint.DistanceTo(nearestLine.StartPoint) < 1 Then
                    Dim nl As New Line With {.X1 = currentPoint.X, .Y1 = currentPoint.Y, .X2 = nearestLine.X1, .Y2 = nearestLine.Y1}
                    optimisedLines.Add(nl)
                    fractionalPaths += 1
                End If

                optimisedLines.Add(nearestLine)
                currentPoint = nearestLine.EndPoint
            Else

                If currentPoint.DistanceTo(nearestLine.EndPoint) < 1 Then
                    Dim nl As New Line With {.X1 = currentPoint.X, .Y1 = currentPoint.Y, .X2 = nearestLine.X2, .Y2 = nearestLine.Y2}
                    optimisedLines.Add(nl)
                    fractionalPaths += 1
                End If
                optimisedLines.Add(New Line() With {.X1 = nearestLine.X2, .Y1 = nearestLine.Y2, .X2 = nearestLine.X1, .Y2 = nearestLine.Y1})
                currentPoint = nearestLine.StartPoint
            End If

            workingLines.Remove(nearestLine)

        End While

        Debug.WriteLine(fractionalPaths)

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

        If Not IsShapeClosed(lines) Then
            Return lines
        End If

        Dim processedLines As New List(Of Line)
        processedLines.AddRange(FillLines(lines, cfg.DrawingConfig.MinStrokeWidth, cfg.DrawingConfig.ShadingAngle))

        If cfg.DrawingConfig.CrossHatch Then
            processedLines.AddRange(FillLines(lines, cfg.DrawingConfig.MinStrokeWidth, cfg.DrawingConfig.ShadingAngle + 90))
        End If

        processedLines = OptimiseFills(processedLines)

        If cfg.DrawingConfig.KeepOutlines Then
            processedLines.InsertRange(0, lines)
        End If

        Return processedLines

    End Function
End Class
