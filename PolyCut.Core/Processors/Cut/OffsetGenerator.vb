Option Strict On

Imports System.Windows
Imports System.Windows.Media

Public Class OffsetGenerator

    Public Shared Function CreateOffsetArcs(lines As List(Of GeoLine), toolRadius As Double, Optional noiseTolerance As Double = 0) As List(Of GeoLine)
        If lines Is Nothing Then Return New List(Of GeoLine)
        If toolRadius <= 0 Then Return lines.ToList()

        Dim source = lines.Where(Function(line) line.StartPoint <> line.EndPoint).ToList()

        Dim result As New List(Of GeoLine)
        Dim runStart = 0

        For i = 1 To source.Count
            If i < source.Count AndAlso source(i - 1).IsContinuousWith(source(i)) Then Continue For
            AppendRun(BladePathNoiseFilter.Filter(source.GetRange(runStart, i - runStart), noiseTolerance), toolRadius, result)
            runStart = i
        Next

        Return result
    End Function

    Private Shared Sub AppendRun(source As List(Of GeoLine), radius As Double, result As List(Of GeoLine))
        Dim shifted = source.Select(Function(line) ShiftAlongBlade(line, radius)).ToList()
        Dim closed = source.Last().IsContinuousWith(source(0))

        For i = 0 To source.Count - 1
            If shifted(i).StartPoint <> shifted(i).EndPoint Then result.Add(shifted(i))
            If i = source.Count - 1 AndAlso Not closed Then Exit For
            Dim nextIndex = (i + 1) Mod source.Count
            AppendSwivel(source(i).EndPoint.ToPoint(), shifted(i).EndPoint.ToPoint(), shifted(nextIndex).StartPoint.ToPoint(), source(i).AngleR, source(nextIndex).AngleR, radius, result)
        Next

    End Sub

    Private Shared Function ShiftAlongBlade(line As GeoLine, radius As Double) As GeoLine
        Dim dx = CDbl(line.X2) - line.X1
        Dim dy = CDbl(line.Y2) - line.Y1
        Dim scale = radius / Math.Sqrt(dx * dx + dy * dy)
        Return New GeoLine(CSng(line.X1 + dx * scale), CSng(line.Y1 + dy * scale), CSng(line.X2 + dx * scale), CSng(line.Y2 + dy * scale))
    End Function

    Private Shared Sub AppendSwivel(center As Point, startPoint As Point, endPoint As Point, startAngle As Double, endAngle As Double, radius As Double, result As List(Of GeoLine))
        If startPoint = endPoint Then Return

        Dim angle = Math.IEEERemainder(endAngle - startAngle, 2 * Math.PI)
        Dim parts = Math.Max(1, CInt(Math.Ceiling(Math.Abs(angle) / (Math.PI / 2))))
        Dim figure As New PathFigure With {.StartPoint = startPoint}
        Dim arcStart = startPoint
        ' Split large turns into quarter-circle Beziers, including 180-degree reversals just in case we genuinely want to cut back along the way we came (extreme sharp points).
        For part = 1 To parts
            Dim direction = startAngle + angle * part / parts
            Dim arcEnd = If(part = parts, endPoint,
                New Point(center.X + radius * Math.Cos(direction), center.Y + radius * Math.Sin(direction)))
            figure.Segments.Add(DirectCast(BezierArcApproximation(arcStart, arcEnd, center, radius), BezierSegment))
            arcStart = arcEnd
        Next

        Dim geometry As New PathGeometry()
        geometry.Figures.Add(figure)
        Dim flattened = geometry.GetFlattenedPathGeometry(0.01, ToleranceType.Absolute)

        For Each arc In BuildLinesFromGeometry(flattened, 0).SelectMany(Function(loopLines) loopLines)
            If arc.StartPoint <> arc.EndPoint Then result.Add(arc)
        Next

    End Sub


    ' cos(25 deg)
    Const AlignmentThresholdCos As Double = 0.906

    Public Shared Function ReorderLoopForBladeAlignment(loopLines As List(Of GeoLine), lastBladeDir As Vector?) As List(Of GeoLine)
        If loopLines Is Nothing OrElse loopLines.Count < 2 Then Return loopLines

        Dim n = loopLines.Count

        Dim scored = loopLines.Select(Function(l, idx)
                                          Dim predIdx = (idx - 1 + n) Mod n
                                          Dim predDir = loopLines(predIdx).Direction()
                                          Dim currDir = l.Direction()

                                          ' Strongly prefer entries where the loop predecessor already points the same way.
                                          Dim hasFreeEntry As Boolean = Vector.Multiply(predDir, currDir) >= AlignmentThresholdCos

                                          ' Secondary score: alignment with the last known blade direction.
                                          Dim dirScore As Double = If(lastBladeDir.HasValue, Vector.Multiply(lastBladeDir.Value, currDir), 0.0)

                                          Return New With {idx, Key .score = If(hasFreeEntry, 10.0, 0.0) + dirScore}
                                      End Function).OrderByDescending(Function(x) x.score).ToList()

        Return loopLines.RotateStartAt(scored(0).idx)
    End Function

End Class
