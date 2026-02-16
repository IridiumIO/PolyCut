Imports System.Runtime.CompilerServices
Imports System.Runtime.Intrinsics.Arm
Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes



' Line and IEnumerable(Of Line) extensions
Partial Public Module Extensions


    <Extension>
    Public Function Direction(line As GeoLine) As Vector
        Dim v As New Vector(line.X2 - line.X1, line.Y2 - line.Y1)
        If v.Length > 0 Then v.Normalize()
        Return v
    End Function


    <Extension>
    Public Function TransformLines(lines As IEnumerable(Of GeoLine), transforms As System.Drawing.Drawing2D.Matrix) As IEnumerable(Of GeoLine)
        Return lines.Select(Function(line) line.TransformLine(transforms))
    End Function




    <Extension>
    Public Function RotateStartAt(lines As IEnumerable(Of GeoLine), startIndex As Integer) As List(Of GeoLine)
        Dim list = lines.ToList()
        If list.Count = 0 OrElse startIndex <= 0 OrElse startIndex >= list.Count Then
            Return New List(Of GeoLine)(list)
        End If

        Dim result As New List(Of GeoLine)(list.Count)
        For i = startIndex To list.Count - 1
            result.Add(list(i))
        Next
        For i = 0 To startIndex - 1
            result.Add(list(i))
        Next
        Return result
    End Function

    <Extension>
    Public Function RepresentativeCenterPoint(figure As IEnumerable(Of GeoLine)) As Point
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
    Public Function LastDirection(lines As IEnumerable(Of GeoLine)) As Vector?
        Dim list = lines.ToList()
        For i = list.Count - 1 To 0 Step -1
            Dim v = list(i).Direction()
            If v.Length > 0 Then Return v
        Next
        Return Nothing
    End Function

    'Reorder figures to minimise travel distance between them. I really need to get away from List(of List(of Line)) at some point...
    <Extension>
    Public Function ReorderFiguresGreedy(figures As IEnumerable(Of List(Of GeoLine))) As List(Of List(Of GeoLine))

        If figures Is Nothing OrElse figures.Count = 0 Then Return New List(Of List(Of GeoLine))()

        Dim remaining As New List(Of List(Of GeoLine))(figures)
        Dim orderedFigures As New List(Of List(Of GeoLine))

        Dim currentPoint As New System.Windows.Point(0, 0)

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

    ' Overload for IPathBasedElement 
    <Extension>
    Public Function ReorderFiguresGreedy(figures As IEnumerable(Of IPathBasedElement)) As List(Of IPathBasedElement)

        If figures Is Nothing OrElse figures.Count = 0 Then Return New List(Of IPathBasedElement)()

        Dim remaining As New List(Of IPathBasedElement)(figures)
        Dim orderedFigures As New List(Of IPathBasedElement)

        Dim currentPoint As New System.Windows.Point(0, 0)

        While remaining.Count > 0
            Dim bestIdx As Integer = -1
            Dim bestDistSq As Double = Double.MaxValue

            For i = 0 To remaining.Count - 1
                ' Use FlattenedLines to calculate representative center point
                Dim rep = remaining(i).FlattenedLines.RepresentativeCenterPoint
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

            ' update currentPoint to the representative of chosen group
            currentPoint = chosen.FlattenedLines.RepresentativeCenterPoint
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
    Public Function LineTo(startPoint As Point, endPoint As Point) As GeoLine
        Return New GeoLine(startPoint.X, startPoint.Y, endPoint.X, endPoint.Y)
    End Function


    <Extension>
    Public Function RaycastIntersectsWithShape(point As Point, shapeBoundaries As List(Of GeoLine), angle As Double, rayLength As Double) As Boolean
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
    Public Function IsPointInsideShape(point As Point, shapeBoundaries As List(Of GeoLine), Optional angle As Double = 0, Optional rayLength As Double = 10000) As Boolean
        ' Define the endpoint of the ray based on the given angle and length
        Dim endpoint As New Point(point.X + rayLength * Math.Cos(angle), point.Y + rayLength * Math.Sin(angle))
        Dim intersectionCount As Integer = 0

        ' Loop through each boundary line of the shape
        For Each line In shapeBoundaries
            Dim intersection = New GeoLine(point.X, point.Y, endpoint.X, endpoint.Y).GetIntersectionPointWith(line)

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