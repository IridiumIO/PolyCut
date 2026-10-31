Option Strict On

Imports Clipper2Lib

Public Class CutWidthCompensationProcessor

    Private Const CoordinateScale As Double = 100_000

    Public Shared Function Process(figures As List(Of List(Of GeoLine)), cutWidth As Double) As List(Of List(Of GeoLine))
        If cutWidth = 0 Then Return figures.Select(Function(figure) figure.ToList()).ToList()

        Dim result As New List(Of List(Of GeoLine))
        Dim paths As New Paths64

        For Each figure In figures
            ' Only continuous, closed contours have an unambiguous inside and outside.
            If figure.Count < 3 OrElse Not figure.Last().IsContinuousWith(figure(0)) OrElse
                Enumerable.Range(1, figure.Count - 1).Any(Function(i) Not figure(i - 1).IsContinuousWith(figure(i))) Then
                result.Add(figure.ToList())
                Continue For
            End If

            Dim path As New Path64
            For Each line In figure
                If line.StartPoint <> line.EndPoint Then
                    path.Add(New Point64(CDbl(line.X1) * CoordinateScale, CDbl(line.Y1) * CoordinateScale))
                End If
            Next
            If path.Count >= 3 Then paths.Add(path)
        Next

        ' Resolve holes together, independently of contour winding, as in contour fills.
        Dim region = Clipper.Union(paths, Clipper2Lib.FillRule.EvenOdd)
        Dim distance = cutWidth / 2 * CoordinateScale
        Dim compensated = Clipper.InflatePaths(region, distance, JoinType.Miter, EndType.Polygon)

        For Each path In compensated
            Dim loopLines As New List(Of GeoLine)(path.Count)
            For i = 0 To path.Count - 1
                Dim a = path(i)
                Dim b = path((i + 1) Mod path.Count)
                Dim line As New GeoLine(CSng(a.X / CoordinateScale), CSng(a.Y / CoordinateScale),
                                        CSng(b.X / CoordinateScale), CSng(b.Y / CoordinateScale))
                If line.StartPoint <> line.EndPoint Then loopLines.Add(line)
            Next
            If loopLines.Count >= 3 Then result.Add(loopLines)
        Next

        Return result
    End Function

End Class
