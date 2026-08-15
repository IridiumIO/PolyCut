Imports Clipper2Lib

Public Class ContourFillGenerator


    Private Const EndpointToleranceSquared As Double = 16.0

    Public Shared Function Generate(lines As List(Of GeoLine), spacing As Double) As List(Of List(Of GeoLine))

        Dim result As New List(Of List(Of GeoLine))
        If lines Is Nothing OrElse lines.Count < 3 OrElse spacing <= 0 Then Return result

        ' Convert the incoming GeoLines into closed Clipper polygons.
        Dim sourceRegion As Paths64 = BuildPaths(lines)
        If sourceRegion.Count = 0 Then Return result

        sourceRegion = Clipper.Union(sourceRegion, Clipper2Lib.FillRule.EvenOdd)
        If sourceRegion.Count = 0 Then Return result

        Dim cleanupTolerance As Double = Math.Max(1.0, spacing * 0.01)
        sourceRegion = Clipper.SimplifyPaths(sourceRegion, cleanupTolerance)
        RemoveInvalidPaths(sourceRegion)

        For level As Integer = 1 To 10000
            Dim distance As Double = level * spacing

            Dim contourRegion = Clipper.InflatePaths(sourceRegion, -distance, JoinType.Miter, EndType.Polygon, 2.0, cleanupTolerance)

            If contourRegion.Count = 0 Then Exit For

            contourRegion = Clipper.Union(contourRegion, Clipper2Lib.FillRule.EvenOdd)
            contourRegion = Clipper.SimplifyPaths(contourRegion, cleanupTolerance)
            RemoveInvalidPaths(contourRegion)

            If contourRegion.Count = 0 Then Exit For

            result.AddRange(ToGeoLoops(contourRegion))
        Next

        Return result
    End Function


    ' -------------------------
    ' GeoLine > Clipper polygons
    ' -------------------------

    Private Shared Function BuildPaths(lines As List(Of GeoLine)) As Paths64
        Dim result As New Paths64
        Dim unused As New List(Of GeoLine)(lines)

        While unused.Count > 0
            Dim seed = unused(0)
            unused.RemoveAt(0)

            Dim startX As Double = seed.X1
            Dim startY As Double = seed.Y1
            Dim curX As Double = seed.X2
            Dim curY As Double = seed.Y2

            ' Ignore degenerate segments.

            If Dist2(startX, startY, curX, curY) <= EndpointToleranceSquared Then Continue While

            Dim path As New Path64 From {
                New Point64(startX, startY),
                New Point64(curX, curY)
            }

            Dim closed As Boolean = IsNear(curX, curY, startX, startY)

            While Not closed
                Dim foundIndex As Integer = -1
                Dim nextX As Double = 0
                Dim nextY As Double = 0

                For i As Integer = 0 To unused.Count - 1
                    Dim ln = unused(i)

                    If IsNear(curX, curY, ln.X1, ln.Y1) Then
                        nextX = ln.X2
                        nextY = ln.Y2
                        foundIndex = i
                        Exit For
                    End If

                    If IsNear(curX, curY, ln.X2, ln.Y2) Then
                        nextX = ln.X1
                        nextY = ln.Y1
                        foundIndex = i
                        Exit For
                    End If
                Next

                ' Open chain: don't treat it as a polygon.
                If foundIndex < 0 Then Exit While

                unused.RemoveAt(foundIndex)

                curX = nextX
                curY = nextY

                If IsNear(curX, curY, startX, startY) Then
                    closed = True
                ElseIf Not IsNear(curX, curY, path(path.Count - 1).X, path(path.Count - 1).Y) Then
                    path.Add(New Point64(curX, curY))
                End If
            End While

            If closed AndAlso path.Count >= 3 Then result.Add(path)
        End While

        Return result
    End Function


    ' -------------------------
    ' Clipper polygons > GeoLines
    ' -------------------------

    Private Shared Function ToGeoLoops(paths As Paths64) As List(Of List(Of GeoLine))
        Dim result As New List(Of List(Of GeoLine))(paths.Count)

        For Each path In paths
            If path.Count < 3 Then Continue For

            Dim geo As New List(Of GeoLine)(path.Count)

            For i As Integer = 0 To path.Count - 1
                Dim a = path(i)
                Dim b = path((i + 1) Mod path.Count)

                If a.X = b.X AndAlso a.Y = b.Y Then Continue For

                geo.Add(New GeoLine(
                    CSng(a.X), CSng(a.Y),
                    CSng(b.X), CSng(b.Y)))
            Next

            If geo.Count >= 3 Then result.Add(geo)
        Next

        Return result
    End Function


    Private Shared Sub RemoveInvalidPaths(paths As Paths64)
        paths.RemoveAll(Function(path) path.Count < 3)
    End Sub

    Private Shared Function IsNear(x1 As Double, y1 As Double, x2 As Double, y2 As Double) As Boolean
        Return Dist2(x1, y1, x2, y2) <= EndpointToleranceSquared
    End Function

    Private Shared Function Dist2(x1 As Double, y1 As Double, x2 As Double, y2 As Double) As Double
        Dim dx = x2 - x1
        Dim dy = y2 - y1
        Return dx * dx + dy * dy
    End Function

End Class