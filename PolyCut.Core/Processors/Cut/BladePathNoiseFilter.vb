Option Strict On

Imports System.Numerics

'https://stackoverflow.com/questions/35290973/line-simplification-algorithm-visvalingam-vs-douglas-peucker 
'TODO try Clipper2's simplify feature. ??Offset by 0 to get the simplified region. 
Friend Class BladePathNoiseFilter
    Public Shared Function Filter(source As List(Of GeoLine), tolerance As Double) As List(Of GeoLine)
        If Not Double.IsFinite(tolerance) OrElse tolerance <= 0 OrElse source.Count < 3 Then Return source
        Dim closed = source.Last().IsContinuousWith(source(0))
        Dim points = source.Select(Function(line) line.StartPoint).ToList()
        If Not closed Then points.Add(source.Last().EndPoint)
        Dim kept = Enumerable.Range(0, points.Count).ToList()
        Const cornerAngle As Double = Math.PI / 4
        Dim corners As New HashSet(Of Integer)(kept.Where(Function(i) (closed OrElse (i > 0 AndAlso i < points.Count - 1)) AndAlso
                                                                      Math.Abs(TurnAt(points, kept, i)) >= cornerAngle))
        Dim minimumCount = If(closed, 3, 2)
        Dim firstCandidate = If(closed, 0, 1)

        While kept.Count > minimumCount
            Dim bestIndex = -1
            Dim bestError = Double.PositiveInfinity
            For i = firstCandidate To kept.Count - If(closed, 1, 2)
                If corners.Contains(kept(i)) Then Continue For
                Dim previous = (i - 1 + kept.Count) Mod kept.Count
                Dim following = (i + 1) Mod kept.Count
                Dim turn = TurnAt(points, kept, i)
                ' Keep distinct corners. Only remove a turn that opposes its neighbours.
                If Math.Abs(turn) >= cornerAngle OrElse turn = 0 Then Continue For
                Dim reversesPrevious = (closed OrElse previous > 0) AndAlso turn * TurnAt(points, kept, previous) < 0
                Dim reversesNext = (closed OrElse following < kept.Count - 1) AndAlso turn * TurnAt(points, kept, following) < 0
                If Not reversesPrevious AndAlso Not reversesNext Then Continue For
                Dim deviation = ReplacementError(points, kept(previous), kept(following))
                If deviation <= tolerance AndAlso deviation < bestError Then
                    bestError = deviation
                    bestIndex = i
                End If
            Next
            If bestIndex < 0 Then Exit While
            kept.RemoveAt(bestIndex)
        End While

        Dim result As New List(Of GeoLine)

        For i = 0 To kept.Count - If(closed, 1, 2)
            result.Add(New GeoLine(points(kept(i)), points(kept((i + 1) Mod kept.Count))))
        Next

        Return result
    End Function

    Private Shared Function TurnAt(points As List(Of Vector2), kept As List(Of Integer), index As Integer) As Double

        Dim a = points(kept((index - 1 + kept.Count) Mod kept.Count))
        Dim b = points(kept(index))
        Dim c = points(kept((index + 1) Mod kept.Count))
        Dim ax = CDbl(b.X) - a.X, ay = CDbl(b.Y) - a.Y
        Dim bx = CDbl(c.X) - b.X, by = CDbl(c.Y) - b.Y
        Return Math.Atan2(ax * by - ay * bx, ax * bx + ay * by)

    End Function

    Private Shared Function ReplacementError(points As List(Of Vector2), first As Integer, last As Integer) As Double

        Dim a = points(first), b = points(last)
        Dim dx = CDbl(b.X) - a.X, dy = CDbl(b.Y) - a.Y
        Dim length2 = dx * dx + dy * dy
        If length2 = 0 Then Return Double.PositiveInfinity
        Dim deviation = 0.0, previousProjection = 0.0
        Dim i = (first + 1) Mod points.Count

        While i <> last
            Dim px = CDbl(points(i).X) - a.X, py = CDbl(points(i).Y) - a.Y
            Dim projection = (px * dx + py * dy) / length2
            If projection < previousProjection OrElse projection > 1 Then Return Double.PositiveInfinity
            previousProjection = projection
            deviation = Math.Max(deviation, Math.Abs(dx * py - dy * px) / Math.Sqrt(length2))
            i = (i + 1) Mod points.Count
        End While

        Return deviation
    End Function
End Class
