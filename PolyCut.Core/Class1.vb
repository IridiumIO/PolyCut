
Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes
Imports Svg
Imports Svg.Transforms





Public Module SharedF

    Public Function ApplyTransforms(ByRef line As Line, transforms As System.Drawing.Drawing2D.Matrix)

        Dim wpfMatrix As Matrix = New Matrix(transforms.Elements(0), transforms.Elements(1), transforms.Elements(2), transforms.Elements(3), transforms.OffsetX, transforms.OffsetY)

        Dim p1 = New Point(line.X1, line.Y1)
        Dim p2 = New Point(line.X2, line.Y2)
        p1 = wpfMatrix.Transform(p1)
        p2 = wpfMatrix.Transform(p2)

        Return New Line With {
            .X1 = p1.X,
            .Y1 = p1.Y,
            .X2 = p2.X,
            .Y2 = p2.Y
        }

    End Function

    Public Function TransformLines(lines As IEnumerable(Of Line), transforms As System.Drawing.Drawing2D.Matrix) As List(Of Line)

        Dim transformedLines As New List(Of Line)

        For Each line In lines
            transformedLines.Add(ApplyTransforms(line, transforms))
        Next

        Return transformedLines

    End Function

    Public Function BuildLinesFromGeometry(geo As PathGeometry, tolerance As Double) As List(Of Line)

        Dim segs As New List(Of Line)

        For Each figure In geo.Figures
            Dim startPoint = figure.StartPoint
            For Each segment In figure.Segments
                Dim lineSegment = TryCast(segment, LineSegment)
                If lineSegment IsNot Nothing Then
                    Dim line = New Line With {
                .X1 = startPoint.X,
                .Y1 = startPoint.Y,
                .X2 = lineSegment.Point.X,
                .Y2 = lineSegment.Point.Y
            }
                    segs.Add(line)
                    startPoint = lineSegment.Point
                Else
                    Dim polyLineSegment = TryCast(segment, PolyLineSegment)
                    If polyLineSegment IsNot Nothing Then
                        For Each point In polyLineSegment.Points
                            Dim line = New Line With {
                        .X1 = startPoint.X,
                        .Y1 = startPoint.Y,
                        .X2 = point.X,
                        .Y2 = point.Y
                    }
                            segs.Add(line)
                            startPoint = point
                        Next
                    End If
                End If
            Next
        Next

        Dim tolerancedLines As New List(Of Line)

        For i As Integer = 0 To segs.Count - 1

            If OffsetProcessor.GetLineLength(segs(i)) < tolerance Then
                Debug.WriteLine("FUCK " & i)
                tolerancedLines(i - 1).X2 = segs(i).X2
                tolerancedLines(i - 1).Y2 = segs(i).Y2
            Else
                tolerancedLines.Add(segs(i))
            End If



        Next

        Return tolerancedLines


    End Function




End Module
