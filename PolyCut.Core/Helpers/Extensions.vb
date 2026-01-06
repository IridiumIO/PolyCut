Imports System.Runtime.CompilerServices
Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes



' Line and IEnumerable(Of Line) extensions
Partial Public Module Extensions

    <Extension>
    Public Function XLength(line As Line) As Double
        Return line.X2 - line.X1
    End Function


    <Extension>
    Public Function YLength(line As Line) As Double
        Return line.Y2 - line.Y1

    End Function


    <Extension>
    Public Function Length(line As Line) As Double
        Return Math.Sqrt(line.XLength ^ 2 + line.YLength ^ 2)
    End Function

    <Extension>
    Public Function Slope(line As Line) As Double
        Return line.YLength / line.XLength
    End Function


    <Extension>
    Public Function StartPoint(line As Line) As Point
        Return New Point(line.X1, line.Y1)
    End Function


    <Extension>
    Public Function EndPoint(line As Line) As Point
        Return New Point(line.X2, line.Y2)
    End Function

    <Extension>
    Public Function MidPoint(line As Line) As Point
        Return New Point((line.X1 + line.X2) / 2, (line.Y1 + line.Y2) / 2)
    End Function

    <Extension>
    Public Function Direction(line As Line) As Vector
        Dim v As New Vector(line.X2 - line.X1, line.Y2 - line.Y1)
        If v.Length > 0 Then v.Normalize()
        Return v
    End Function

    <Extension>
    Public Function TransformLine(line As Line, transforms As System.Drawing.Drawing2D.Matrix) As Line

        Dim wpfMatrix As New Matrix(transforms.Elements(0), transforms.Elements(1), transforms.Elements(2), transforms.Elements(3), transforms.OffsetX, transforms.OffsetY)

        Dim p1 = wpfMatrix.Transform(line.StartPoint)
        Dim p2 = wpfMatrix.Transform(line.EndPoint)

        Return p1.LineTo(p2)

    End Function

    <Extension>
    Public Function TransformLines(lines As IEnumerable(Of Line), transforms As System.Drawing.Drawing2D.Matrix) As IEnumerable(Of Line)
        Return lines.Select(Function(line) line.TransformLine(transforms))
    End Function


    <Extension>
    Public Function IsContinuousWith(line As Line, otherline As Line) As Boolean
        Return line.X2 = otherline.X1 AndAlso line.Y2 = otherline.Y1
    End Function


    <Extension>
    Public Function IsCollinearWith(line1 As Line, line2 As Line, Optional tolerance As Double = 0) As Boolean

        Dim radians = Math.PI / 180 * tolerance
        Dim TwoAngle = line1.GetAngleBetween(line2)
        Dim withinTolerance = MathHelpers.Between(TwoAngle, Math.PI - radians, Math.PI + radians)
        Return withinTolerance

    End Function

    <Extension>
    Public Function AngleR(line As Line) As Double
        Return Math.Atan2(line.YLength, line.XLength)
    End Function

    <Extension>
    Public Function GetAngleBetween(line1 As Line, line2 As Line) As Double

        Dim hypot As Line = line1.StartPoint.LineTo(line2.EndPoint)

        Dim lenA As Double = line1.Length
        Dim lenb As Double = line2.Length
        Dim lenC As Double = hypot.Length

        Dim Angle = Math.Acos((lenA ^ 2 + lenb ^ 2 - lenC ^ 2) / (2 * lenA * lenb))

        Return Angle


    End Function

    <Extension>
    Public Function GetIntersectionPointWith(line1 As Line, line2 As Line, Optional IncludeCoincidentIntersection As Boolean = False) As Nullable(Of Point)
        Dim Ax As Double = line1.X1
        Dim Ay As Double = line1.Y1
        Dim Bx As Double = line1.X2
        Dim By As Double = line1.Y2

        Dim Cx As Double = line2.X1
        Dim Cy As Double = line2.Y1
        Dim Dx As Double = line2.X2
        Dim Dy As Double = line2.Y2

        Const Epsilon As Double = 0.000000001
        Dim denominator As Double = (Dy - Cy) * (Bx - Ax) - (Dx - Cx) * (By - Ay)
        Dim numerator1 As Double = (Dx - Cx) * (Ay - Cy) - (Dy - Cy) * (Ax - Cx)
        Dim numerator2 As Double = (Cy - Ay) * (Ax - Bx) - (Cx - Ax) * (Ay - By)

        If Math.Abs(denominator) < Epsilon Then
            ' Lines are parallel or coincident
            If IncludeCoincidentIntersection AndAlso Math.Abs(numerator1) < Epsilon AndAlso Math.Abs(numerator2) < Epsilon Then
                ' Return any point on the coincident lines (e.g., the start point of the first line)
                Return New Point(Ax, Ay)
            End If
            Return Nothing
        End If

        Dim t = numerator1 / denominator
        Dim u = numerator2 / denominator

        If t < -Epsilon OrElse t > 1 + Epsilon OrElse u < -Epsilon OrElse u > 1 + Epsilon Then
            Return Nothing
        End If

        ' Calculate the intersection point
        Return New Point(Ax + t * (Bx - Ax), Ay + t * (By - Ay))
    End Function

    <Extension>
    Public Function IsPointOnLine(point As Point, line As Line) As Boolean
        Const Epsilon As Double = 0.000000001
        Dim crossProduct As Double = (point.Y - line.Y1) * (line.X2 - line.X1) - (point.X - line.X1) * (line.Y2 - line.Y1)
        If Math.Abs(crossProduct) > Epsilon Then Return False

        Dim dotProduct As Double = (point.X - line.X1) * (line.X2 - line.X1) + (point.Y - line.Y1) * (line.Y2 - line.Y1)
        If dotProduct < 0 Then Return False

        Dim squaredLength As Double = (line.X2 - line.X1) ^ 2 + (line.Y2 - line.Y1) ^ 2
        If dotProduct > squaredLength Then Return False

        Return True
    End Function

    <Extension>
    Public Function IsLineOnLine(line1 As Line, line2 As Line) As Boolean
        Return line1.StartPoint.IsPointOnLine(line2) AndAlso line1.EndPoint.IsPointOnLine(line2)
    End Function

    <Extension>
    Public Function IsLineOnAnyLine(line As Line, lines As List(Of Line)) As Boolean
        For Each l In lines
            If line.IsLineOnLine(l) Then
                Return True
            End If
        Next
        Return False
    End Function

    <Extension>
    Public Function IntersectsWithShape(line As Line, shapeBoundaries As List(Of Line), Optional includeCoincidentPoints As Boolean = False) As Boolean
        For Each segment In shapeBoundaries
            ' Check for intersection
            Dim intersection = line.GetIntersectionPointWith(segment, includeCoincidentPoints)
            If intersection IsNot Nothing Then
                Return True
            End If

            ' Check if the line starts or ends on the boundary
            If includeCoincidentPoints AndAlso (line.StartPoint.IsPointOnLine(segment) OrElse line.EndPoint.IsPointOnLine(segment)) Then
                Return True
            End If
        Next

        Return False
    End Function

    <Extension>
    Public Function RotateStartAt(lines As IEnumerable(Of Line), startIndex As Integer) As List(Of Line)
        Dim list = lines.ToList()
        If list.Count = 0 OrElse startIndex <= 0 OrElse startIndex >= list.Count Then
            Return New List(Of Line)(list)
        End If

        Dim result As New List(Of Line)(list.Count)
        For i = startIndex To list.Count - 1
            result.Add(list(i))
        Next
        For i = 0 To startIndex - 1
            result.Add(list(i))
        Next
        Return result
    End Function

    <Extension>
    Public Function RepresentativeCenterPoint(figure As IEnumerable(Of Line)) As Point
        If figure Is Nothing Then Return New Point(0, 0)
        Dim list = figure.ToList()
        If list.Count = 0 Then Return New Point(0, 0)
        Dim sumX As Double = 0
        Dim sumY As Double = 0
        For Each ln In list
            Dim mp = ln.MidPoint()
            sumX += mp.X
            sumY += mp.Y
        Next
        Return New Point(sumX / list.Count, sumY / list.Count)
    End Function

    <Extension>
    Public Function LastDirection(lines As IEnumerable(Of Line)) As Vector?
        Dim list = lines.ToList()
        For i = list.Count - 1 To 0 Step -1
            Dim v = list(i).Direction()
            If v.Length > 0 Then Return v
        Next
        Return Nothing
    End Function

    'Reorder figures to minimise travel distance between them. I really need to get away from List(of List(of Line)) at some point...
    <Extension>
    Public Function ReorderFiguresGreedy(figures As IEnumerable(Of List(Of Line))) As List(Of List(Of Line))

        If figures Is Nothing OrElse figures.Count = 0 Then Return New List(Of List(Of Line))()

        Dim remaining As New List(Of List(Of Line))(figures)
        Dim orderedFigures As New List(Of List(Of Line))

        Dim currentPoint As New Windows.Point(0, 0)

        While remaining.Count > 0
            Dim bestIdx As Integer = -1
            Dim bestDistSq As Double = Double.MaxValue

            For i = 0 To remaining.Count - 1
                Dim rep = remaining(i).RepresentativeCenterPoint
                Dim dx = rep.X - currentPoint.X
                Dim dy = rep.Y - currentPoint.Y
                Dim distSq = dx * dx + dy * dy
                If distSq < bestDistSq Then
                    bestDistSq = distSq
                    bestIdx = i
                End If
            Next

            If bestIdx = -1 Then
                orderedFigures.AddRange(remaining)
                Exit While
            End If

            Dim chosen = remaining(bestIdx)
            orderedFigures.Add(chosen)
            remaining.RemoveAt(bestIdx)

            ' update currentPoint to the representative of chosen group (keeps continuity for greedy selection)
            currentPoint = chosen.RepresentativeCenterPoint
        End While

        Return orderedFigures
    End Function





End Module











'Extension Methods for Points
Partial Public Module Extensions

    <Extension>
    Public Function Lerp(A As Point, B As Point, t As Double) As Point
        Return New Point(A.X + (B.X - A.X) * t, A.Y + (B.Y - A.Y) * t)
    End Function



    <Extension>
    Public Function LineTo(startPoint As Point, endPoint As Point) As Line
        Return New Line With {
            .X1 = startPoint.X,
            .Y1 = startPoint.Y,
            .X2 = endPoint.X,
            .Y2 = endPoint.Y
        }
    End Function


    <Extension>
    Public Function RaycastIntersectsWithShape(point As Point, shapeBoundaries As List(Of Line), angle As Double, rayLength As Double) As Boolean
        Dim endpoint As New Point(point.X + rayLength * Math.Cos(angle), point.Y + rayLength * Math.Sin(angle))
        Dim startpoint As New Point(point.X - rayLength * Math.Cos(angle), point.Y - rayLength * Math.Sin(angle))

        Dim intersectionPoints As New List(Of Point)
        For Each line In shapeBoundaries
            Dim intersection = startpoint.LineTo(endpoint).GetIntersectionPointWith(line)
            If intersection IsNot Nothing Then
                Return True
            End If
        Next

        Return False
    End Function

    <Extension>
    Public Function IsPointInsideShape(point As Point, shapeBoundaries As List(Of Line), Optional angle As Double = 0, Optional rayLength As Double = 10000) As Boolean
        ' Define the endpoint of the ray based on the given angle and length
        Dim endpoint As New Point(point.X + rayLength * Math.Cos(angle), point.Y + rayLength * Math.Sin(angle))
        Dim intersectionCount As Integer = 0

        ' Loop through each boundary line of the shape
        For Each line In shapeBoundaries
            Dim intersection = New Line With {
            .X1 = point.X,
            .Y1 = point.Y,
            .X2 = endpoint.X,
            .Y2 = endpoint.Y
        }.GetIntersectionPointWith(line)

            ' If there is an intersection, increment the count
            If intersection IsNot Nothing Then
                intersectionCount += 1
            End If
        Next

        ' Return true if the intersection count is odd (inside), false if even (outside)
        Return intersectionCount Mod 2 = 1
    End Function


    <Extension>
    Public Function DistanceTo(point1 As Point, point2 As Point) As Double
        Return Math.Sqrt((point1.X - point2.X) ^ 2 + (point1.Y - point2.Y) ^ 2)
    End Function

End Module


'General Helpers?
Partial Public Module Extensions

    <Extension>
    Public Sub AddIfNotNull(Of T)(ByRef list As List(Of T), item As T)
        If item IsNot Nothing Then
            list.Add(item)
        End If
    End Sub


    <Extension>
    Public Function Between(number As Double, bound1 As Double, bound2 As Double) As Boolean
        Return number >= Math.Min(bound1, bound2) AndAlso number <= Math.Max(bound1, bound2)
    End Function



End Module