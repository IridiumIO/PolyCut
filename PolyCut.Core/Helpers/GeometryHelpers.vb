
Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes
Imports Svg
Imports Svg.Transforms





Public Module GeometryHelpers


    Public Function BuildLinesFromGeometry(geo As PathGeometry, tolerance As Double) As List(Of List(Of Line))

        Dim segs As New List(Of List(Of Line))

        For Each figure In geo.Figures

            Dim figLines As New List(Of Line)
            Dim startPoint = figure.StartPoint
            For Each segment In figure.Segments
                Dim lineSegment = TryCast(segment, LineSegment)
                If lineSegment IsNot Nothing Then

                    figLines.Add(startPoint.LineTo(lineSegment.Point))
                    startPoint = lineSegment.Point
                Else
                    Dim polyLineSegment = TryCast(segment, PolyLineSegment)
                    If polyLineSegment IsNot Nothing Then
                        For Each point In polyLineSegment.Points
                            figLines.Add(startPoint.LineTo(point))
                            startPoint = point
                        Next
                    End If
                End If
            Next

            Dim tolerancedLines As New List(Of Line) From {figLines(0)}

            Dim wasLastLineShortened As Boolean = False
            For i As Integer = 1 To figLines.Count - 1

                If figLines(i).Length < tolerance And Not wasLastLineShortened Then
                    tolerancedLines.Last.X2 = figLines(i).X2
                    tolerancedLines.Last.Y2 = figLines(i).Y2
                    wasLastLineShortened = True
                Else
                    tolerancedLines.Add(figLines(i))
                    wasLastLineShortened = False
                End If
            Next

            segs.Add(tolerancedLines)

        Next

        Return segs

    End Function


End Module
