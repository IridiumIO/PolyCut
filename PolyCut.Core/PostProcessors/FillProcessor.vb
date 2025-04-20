Imports System.Windows
Imports System.Windows.Shapes

Imports MeasurePerformance.IL.Weaver

Public Class FillProcessor : Implements IProcessor
    <MeasurePerformance>
    Public Shared Function FillLines(lines As List(Of Line), density As Double, fillangle As Double) As List(Of Line)
        Dim fills As New List(Of Line)

        ' Convert angles to radians
        Dim traverseAngleRad = Math.PI * fillangle / 180 + (Math.PI / 2)
        Dim fillAngleRad = Math.PI * fillangle / 180

        ' Calculate the bounding box of the shape
        Dim minX = lines.Min(Function(line) Math.Min(line.X1, line.X2))
        Dim minY = lines.Min(Function(line) Math.Min(line.Y1, line.Y2))
        Dim maxX = lines.Max(Function(line) Math.Max(line.X1, line.X2))
        Dim maxY = lines.Max(Function(line) Math.Max(line.Y1, line.Y2))
        Dim centerX = (minX + maxX) / 2
        Dim centerY = (minY + maxY) / 2

        ' Calculate the maximum traverse extent
        Dim maxExtent = Math.Sqrt((maxX - minX) ^ 2 + (maxY - minY) ^ 2)
        Dim scaleFactor = 10 * Math.Max(maxX - minX, maxY - minY) ' Scale for infinite ray length

        ' Traverse across the shape
        For traversePosition = -maxExtent To maxExtent Step density
            ' Calculate the starting point of the ray
            Dim rayStart = New Point(
            centerX + traversePosition * Math.Cos(traverseAngleRad),
            centerY + traversePosition * Math.Sin(traverseAngleRad)
        )

            ' Create an infinite ray in the fill direction
            Dim ray = New Line With {
            .X1 = rayStart.X - scaleFactor * Math.Cos(fillAngleRad),
            .Y1 = rayStart.Y - scaleFactor * Math.Sin(fillAngleRad),
            .X2 = rayStart.X + scaleFactor * Math.Cos(fillAngleRad),
            .Y2 = rayStart.Y + scaleFactor * Math.Sin(fillAngleRad)
        }

            ' Get the valid segments of the ray inside the shape
            fills.AddRange(GetLinesWithinShape(lines, ray))
        Next

        Return fills
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
    Public Shared Function OptimiseFills(lines As List(Of Line), geometrybounds As List(Of Line))

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

            ' Determine if nearestLine needs to be reversed
            Dim isReversed As Boolean = currentPoint.DistanceTo(nearestLine.EndPoint) < currentPoint.DistanceTo(nearestLine.StartPoint)

            ' Create fractional line if needed
            Dim fractionalLine As New Line With {.X1 = currentPoint.X, .Y1 = currentPoint.Y, .X2 = If(isReversed, nearestLine.X2, nearestLine.X1), .Y2 = If(isReversed, nearestLine.Y2, nearestLine.Y1)}

            If currentPoint.DistanceTo(If(isReversed, nearestLine.EndPoint, nearestLine.StartPoint)) < 1000 AndAlso Not fractionalLine.IntersectsWithShape(geometrybounds) Then
                optimisedLines.Add(fractionalLine)
                fractionalPaths += 1
            End If

            ' Add the nearest line
            If isReversed Then
                optimisedLines.Add(New Line With {.X1 = nearestLine.X2, .Y1 = nearestLine.Y2, .X2 = nearestLine.X1, .Y2 = nearestLine.Y1})
                currentPoint = nearestLine.StartPoint
            Else
                optimisedLines.Add(nearestLine)
                currentPoint = nearestLine.EndPoint
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
        Debug.WriteLine($"{processedLines.Count} fills generated")
        processedLines = OptimiseFills(processedLines, lines)
        Debug.WriteLine($"{processedLines.Count} optimised fills generated")

        If cfg.DrawingConfig.KeepOutlines Then
            processedLines.InsertRange(0, lines)
        End If

        Return processedLines

    End Function
End Class
