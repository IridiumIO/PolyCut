Imports System.Windows
Imports System.Windows.Shapes

Public Class Fill

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

        Dim boundingBox As New Rect(New Point(minX, minY), New Point(maxX, maxY))
        Dim centerP As Point = New Point(boundingBox.X + boundingBox.Width / 2, boundingBox.Y + boundingBox.Height / 2)

        Dim safeLength As Double = Math.Sqrt((maxX - minX) ^ 2 + (maxY - minY) ^ 2) / 2

        Dim traverseangle = Math.PI * fillangle / 180 + (Math.PI / 2)
        fillangle = Math.PI * fillangle / 180


        While boundingBox.Contains(centerP)
            Dim endpoint As Point = New Point(centerP.X + safeLength * Math.Cos(fillangle), centerP.Y + safeLength * Math.Sin(fillangle))
            Dim startpoint As Point = New Point(centerP.X - safeLength * Math.Cos(fillangle), centerP.Y - safeLength * Math.Sin(fillangle))

            Dim fillLine As New Line With {
                    .X1 = startpoint.X,
                    .Y1 = startpoint.Y,
                    .X2 = endpoint.X,
                    .Y2 = endpoint.Y
                }
            fills.Add(fillLine)

            centerP.X += (density * Math.Cos(traverseangle))
            centerP.Y += (density * Math.Sin(traverseangle))


        End While
        centerP = New Point(boundingBox.X + boundingBox.Width / 2, boundingBox.Y + boundingBox.Height / 2)
        While boundingBox.Contains(centerP)
            Dim endpoint As Point = New Point(centerP.X + safeLength * Math.Cos(fillangle), centerP.Y + safeLength * Math.Sin(fillangle))

            Dim startpoint As Point = New Point(centerP.X - safeLength * Math.Cos(fillangle), centerP.Y - safeLength * Math.Sin(fillangle))

            Dim fillLine As New Line With {
                    .X1 = startpoint.X,
                    .Y1 = startpoint.Y,
                    .X2 = endpoint.X,
                    .Y2 = endpoint.Y
                }
            fills.Add(fillLine)

            centerP.X -= (density * Math.Cos(traverseangle))
            centerP.Y -= (density * Math.Sin(traverseangle))


        End While


        Dim clippedLines As New List(Of Line)

        For Each l In fills
            Dim segmentedL = GetLinesWithinShape(lines, l)
            clippedLines.AddRange(segmentedL)
        Next

        Return clippedLines
    End Function
    Public Shared Function GetLinesWithinShape(shapeBoundaries As List(Of Line), ray As Line) As List(Of Line)

        Dim intersectionPoints As New List(Of Point)

        For Each line In shapeBoundaries

            Dim intersection = GetLineIntersection(ray, line)
            If intersection IsNot Nothing Then
                intersectionPoints.Add(intersection)
            End If
        Next

        Dim segments As New List(Of Line)

        intersectionPoints = intersectionPoints.OrderBy(Function(p) p.X).ThenBy(Function(p) p.Y).ToList()

        For i As Integer = 0 To intersectionPoints.Count - 2
            Dim segment As New Line With {
                .X1 = intersectionPoints(i).X,
                .Y1 = intersectionPoints(i).Y,
                .X2 = intersectionPoints(i + 1).X,
                .Y2 = intersectionPoints(i + 1).Y
            }

            'Naive filtering method; in most cases, a line that passes through a shape must intersect the shape an even number of times
            'but this does not account for lines that pass through a shape's corners, which will intersect the shape an odd number of times
            If i Mod 2 = 0 Then
                segments.Add(segment)

            End If

        Next

        Return segments

    End Function

    Private Shared Function GetLineIntersection(line1 As Line, line2 As Line) As Nullable(Of Point)
        Dim Ax As Double = line1.X1
        Dim Ay As Double = line1.Y1
        Dim Bx As Double = line1.X2
        Dim By As Double = line1.Y2

        Dim Cx As Double = line2.X1
        Dim Cy As Double = line2.Y1
        Dim Dx As Double = line2.X2
        Dim Dy As Double = line2.Y2

        Dim denominator As Double = (Dy - Cy) * (Bx - Ax) - (Dx - Cx) * (By - Ay)
        Dim numerator1 As Double = (Dx - Cx) * (Ay - Cy) - (Dy - Cy) * (Ax - Cx)
        Dim numerator2 As Double = (Cy - Ay) * (Ax - Bx) - (Cx - Ax) * (Ay - By)

        If denominator = 0 Then Return Nothing

        Dim t = numerator1 / denominator
        Dim u = numerator2 / denominator

        If t < 0 OrElse t > 1 OrElse u < 0 OrElse u > 1 Then Return Nothing

        Return Lerp(New Point(Ax, Ay), New Point(Bx, By), t)

    End Function

    Private Shared Function Lerp(A As Point, B As Point, t As Double)

        Return New Point(A.X + (B.X - A.X) * t, A.Y + (B.Y - A.Y) * t)

    End Function



End Class
