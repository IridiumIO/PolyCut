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

        Dim intersectionPoints As New List(Of Vector2)

        For Each line In shapeBoundaries
            Dim intersection = ray.GetIntersectionPointWith(line, True)
            If intersection IsNot Nothing Then
                intersectionPoints.Add(intersection)
            End If
        Next

        Dim segments As New List(Of GeoLine)

        intersectionPoints = intersectionPoints.OrderBy(Function(p) p.X).ThenBy(Function(p) p.Y).ToList()

        ' Iterate through pairs of intersection points to create segments
        For i As Integer = 0 To intersectionPoints.Count - 2 Step 2
            segments.Add(intersectionPoints(i).LineTo(intersectionPoints(i + 1)))
        Next

        Return segments

    End Function


    Public Shared Function OptimiseFills(lines As List(Of GeoLine), geometrybounds As List(Of GeoLine), allowTravelInOutlines As Boolean) As List(Of GeoLine)

        Dim workingLines As New List(Of GeoLine)(lines)
        Dim currentPoint As Vector2 = New Vector2(0, 0)
        Dim optimisedLines As New List(Of GeoLine)

        While workingLines.Count > 0
            Dim nearestLine As GeoLine = Nothing
            Dim nearestDistance As Double = Double.MaxValue

            ' Find the nearest line
            For Each line In workingLines

                Dim startDistance As Single = Vector2.DistanceSquared(currentPoint, line.StartPoint)
                Dim endDistance As Single = Vector2.DistanceSquared(currentPoint, line.EndPoint)

                If startDistance < nearestDistance Or endDistance < nearestDistance Then
                    nearestLine = line
                    nearestDistance = Math.Min(startDistance, endDistance)
                End If

            Next

            Dim isReversed As Boolean = Vector2.DistanceSquared(currentPoint, nearestLine.EndPoint) < Vector2.DistanceSquared(currentPoint, nearestLine.StartPoint)

            'TODO: Only generate fractional lines if the user specifies (lines that travel along the boundaries of the shape)
            Dim fractionalLine As New GeoLine(currentPoint, If(isReversed, nearestLine.EndPoint, nearestLine.StartPoint))

            If allowTravelInOutlines AndAlso fractionalLine.IsLineOnAnyLine(geometrybounds, 100) Then 'Since we're multiplying all values by 100,000 - a tolerance of 100 is 0.001mm
                optimisedLines.Add(fractionalLine)
            End If

            ' Add the nearest line
            If isReversed Then
                optimisedLines.Add(nearestLine.Reverse())
                currentPoint = nearestLine.StartPoint
            Else
                optimisedLines.Add(nearestLine)
                currentPoint = nearestLine.EndPoint
            End If

            workingLines.Remove(nearestLine)

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

        If Not IsShapeClosed(lines) OrElse cfg.DrawingConfig.FillType = FillType.None Then
            Return lines
        End If

        'TODO ( in another processor, not here: Especially for text, process each of the shape outlines to see which one is nearest afterwards, and move to that shape next.
        'Then consider aligning the end vector of the first shape's last line with the start vector of the next shape's first line, particularly useful to negate the effect of the swivelling toolhead when cutting

        Dim optimisedLines As New List(Of GeoLine)
        For Each ln In lines
            optimisedLines.Add(New GeoLine(ln.X1 * scalingFactor, ln.Y1 * scalingFactor, ln.X2 * scalingFactor, ln.Y2 * scalingFactor))
        Next

        Dim processedLines As New List(Of GeoLine)
        processedLines.AddRange(FillLines(optimisedLines, cfg.DrawingConfig.MinStrokeWidth * scalingFactor, cfg.DrawingConfig.ShadingAngle))

        If cfg.DrawingConfig.FillType = FillType.CrossHatch Then
            processedLines.AddRange(FillLines(optimisedLines, cfg.DrawingConfig.MinStrokeWidth * scalingFactor, cfg.DrawingConfig.ShadingAngle + 90))
        End If

        'TODO: Choose whether to skip optimisation step
        If cfg.OptimisedToolPath Then processedLines = OptimiseFills(processedLines, optimisedLines, cfg.DrawingConfig.AllowDrawingOverOutlines)


        If cfg.DrawingConfig.KeepOutlines Then
            processedLines.InsertRange(0, optimisedLines)
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
End Class
