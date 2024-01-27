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
    Public Function GetIntersectionPointWith(line1 As Line, line2 As Line) As Nullable(Of Point)
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

        Return New Point(Ax, Ay).Lerp(New Point(Bx, By), t)

    End Function

    <Extension>
    Public Function IntersectsWithShape(line As Line, shapeBoundaries As List(Of Line)) As Boolean

        Dim intersectionPoints As New List(Of Point)
        For Each segment In shapeBoundaries
            Dim intersection = line.GetIntersectionPointWith(segment)
            If intersection IsNot Nothing Then
                Return True
            End If
        Next

        Return False
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