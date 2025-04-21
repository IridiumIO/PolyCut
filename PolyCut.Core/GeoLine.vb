Imports System.Numerics
Imports System.Runtime.CompilerServices
Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes


Public Class GeoLine

    Public Property StartPoint As Vector2
    Public Property EndPoint As Vector2
    Public Property X1 As Single
        Get
            Return StartPoint.X
        End Get
        Set(value As Single)
            StartPoint = New Vector2(value, StartPoint.Y)
        End Set
    End Property
    Public Property Y1 As Single
        Get
            Return StartPoint.Y
        End Get
        Set(value As Single)
            StartPoint = New Vector2(StartPoint.X, value)
        End Set
    End Property
    Public Property X2 As Single
        Get
            Return EndPoint.X
        End Get
        Set(value As Single)
            EndPoint = New Vector2(value, EndPoint.Y)
        End Set
    End Property
    Public Property Y2 As Single
        Get
            Return EndPoint.Y
        End Get
        Set(value As Single)
            EndPoint = New Vector2(EndPoint.X, value)
        End Set
    End Property
    Public ReadOnly Property XLength As Double
        Get
            Return X2 - X1
        End Get
    End Property
    Public ReadOnly Property YLength As Double
        Get
            Return Y2 - Y1
        End Get
    End Property
    Public ReadOnly Property Length As Double
        Get
            Return Vector2.Distance(StartPoint, EndPoint)
        End Get
    End Property
    Public ReadOnly Property Slope As Double
        Get
            Return YLength / XLength
        End Get
    End Property
    Public ReadOnly Property MidPoint As Vector2
        Get
            Return New Vector2((StartPoint.X + EndPoint.X) / 2, (StartPoint.Y + EndPoint.Y) / 2)
        End Get
    End Property
    Public ReadOnly Property AngleR As Double
        Get
            Return Math.Atan2(YLength, XLength)
        End Get
    End Property




    Public Sub New(startP As Vector2, endP As Vector2)
        StartPoint = startP
        EndPoint = endP
    End Sub

    Public Sub New(X1 As Single, Y1 As Single, X2 As Single, Y2 As Single)
        StartPoint = New Vector2(X1, Y1)
        EndPoint = New Vector2(X2, Y2)
    End Sub

    Public Sub New(startP As Vector2, length As Double, angleR As Double)
        StartPoint = startP
        EndPoint = New Vector2(startP.X + length * Math.Cos(angleR), startP.Y + length * Math.Sin(angleR))
    End Sub




    Public Function Reverse() As GeoLine
        Return New GeoLine(EndPoint, StartPoint)
    End Function


    Public Function GetAngleBetween(line2 As GeoLine) As Double

        Dim hypot As GeoLine = StartPoint.LineTo(line2.EndPoint)
        Dim lenA As Double = Me.Length
        Dim lenb As Double = line2.Length
        Dim lenC As Double = hypot.Length
        Return Math.Acos((lenA ^ 2 + lenb ^ 2 - lenC ^ 2) / (2 * lenA * lenb))

    End Function


    Public Function TransformLine(transforms As System.Drawing.Drawing2D.Matrix) As GeoLine
        Dim wpfMatrix As New Matrix(transforms.Elements(0), transforms.Elements(1), transforms.Elements(2), transforms.Elements(3), transforms.OffsetX, transforms.OffsetY)
        Dim p1 = wpfMatrix.Transform(StartPoint.ToPoint)
        Dim p2 = wpfMatrix.Transform(EndPoint.ToPoint)
        Return New GeoLine(p1.X, p1.Y, p2.X, p2.Y)
    End Function


    Public Function IsContinuousWith(otherline As GeoLine, Optional bidirectional As Boolean = False) As Boolean
        If bidirectional Then
            Return (X2 = otherline.X1 AndAlso Y2 = otherline.Y1) OrElse (X1 = otherline.X2 AndAlso Y1 = otherline.Y2)
        Else
            Return X2 = otherline.X1 AndAlso Y2 = otherline.Y1
        End If
    End Function


    Public Function IsCollinearWith(otherline As GeoLine, Optional tolerance As Double = 0) As Boolean
        Dim radians = Math.PI / 180 * tolerance
        Dim TwoAngle = Me.GetAngleBetween(otherline)
        Dim withinTolerance = MathHelpers.Between(TwoAngle, Math.PI - radians, Math.PI + radians)
        Return withinTolerance
    End Function


    Public Function GetIntersectionPointWith(line2 As GeoLine, Optional IncludeCoincidentIntersection As Boolean = False, Optional tolerance As Double = 0.000000001) As Nullable(Of Vector2)

        Dim d1 As Vector2 = EndPoint - StartPoint                   ' Direction vector of the first line
        Dim d2 As Vector2 = line2.EndPoint - line2.StartPoint       ' Direction vector of the second line
        Dim delta As Vector2 = line2.StartPoint - StartPoint        ' Vector between the start points of the two lines


        Dim denominator As Double = d1.X * d2.Y - d1.Y * d2.X       ' Calculate the cross product of the direction vectors
        Dim numerator1 As Double = delta.X * d1.Y - delta.Y * d1.X  ' Calculate the cross product of the delta vector and the direction vector of the first line
        Dim numerator2 As Double = delta.X * d2.Y - delta.Y * d2.X  ' Calculate the cross product of the delta vector and the direction vector of the second line


        If Math.Abs(denominator) < tolerance Then                   ' Lines are parallel or coincident

            Dim isApproxCoincident As Boolean = Math.Abs(numerator1) < tolerance AndAlso Math.Abs(numerator2) < tolerance
            If IncludeCoincidentIntersection AndAlso isApproxCoincident Then Return StartPoint
            Return Nothing

        End If

        Dim t = numerator2 / denominator
        Dim u = numerator1 / denominator

        If t < -tolerance OrElse t > 1 + tolerance OrElse u < -tolerance OrElse u > 1 + tolerance Then Return Nothing


        Return StartPoint + t * d1                                  ' Calculate and return the intersection point

    End Function


    Public Function IsLineOnLine(line2 As GeoLine, tolerance As Double) As Boolean
        Return StartPoint.IsPointOnLineG(line2, tolerance) AndAlso EndPoint.IsPointOnLineG(line2, tolerance)
    End Function


    Public Function IsLineOnAnyLine(lines As List(Of GeoLine), tolerance As Double) As Boolean
        For Each l In lines
            If IsLineOnLine(l, tolerance) Then
                Return True
            End If
        Next
        Return False
    End Function


    Public Function IntersectsWithShape(shapeBoundaries As List(Of GeoLine), Optional includeCoincidentPoints As Boolean = False, Optional tolerance As Double = 0.001) As Boolean
        For Each segment In shapeBoundaries

            Dim intersection = GetIntersectionPointWith(segment, includeCoincidentPoints, tolerance)
            If intersection IsNot Nothing Then Return True

            ' Check if the line starts or ends on the boundary
            If includeCoincidentPoints AndAlso (StartPoint.IsPointOnLineG(segment, tolerance) OrElse EndPoint.IsPointOnLineG(segment, tolerance)) Then
                Return True
            End If
        Next

        Return False
    End Function


End Class


Partial Module GeoLineExtensions

    <Extension>
    Public Function TransformLinesG(lines As IEnumerable(Of GeoLine), transforms As System.Drawing.Drawing2D.Matrix) As IEnumerable(Of GeoLine)
        Return lines.Select(Function(line) line.TransformLine(transforms))
    End Function




    <Extension>
    Public Function IsPointOnLineG(point As Vector2, line As GeoLine, tolerance As Double) As Boolean
        ' Check if the point is on the line within the given tolerance
        Dim crossProduct As Double = (point.Y - line.Y1) * (line.X2 - line.X1) - (point.X - line.X1) * (line.Y2 - line.Y1)
        Dim squaredLength As Double = (line.X2 - line.X1) ^ 2 + (line.Y2 - line.Y1) ^ 2
        Dim squaredTolerance = tolerance ^ 2
        If Math.Abs(crossProduct ^ 2) > (squaredTolerance * squaredLength) Then Return False


        Dim dotProduct As Double = (point.X - line.X1) * (line.X2 - line.X1) + (point.Y - line.Y1) * (line.Y2 - line.Y1)
        If dotProduct + tolerance < 0 Then Return False

        If dotProduct > squaredLength + tolerance Then Return False


        Return True
    End Function



    <Extension>
    Public Function LineTo(startPoint As Vector2, endPoint As Vector2) As GeoLine
        Return New GeoLine(
                X1:=startPoint.X,
                Y1:=startPoint.Y,
                X2:=endPoint.X,
                Y2:=endPoint.Y
            )
    End Function


    <Extension>
    Public Function RaycastIntersectsWithShapeG(point As Vector2, shapeBoundaries As List(Of GeoLine), angle As Double, rayLength As Double) As Boolean
        Dim endpoint As New Vector2(point.X + rayLength * Math.Cos(angle), point.Y + rayLength * Math.Sin(angle))
        Dim startpoint As New Vector2(point.X - rayLength * Math.Cos(angle), point.Y - rayLength * Math.Sin(angle))

        Dim intersectionPoints As New List(Of Vector2)
        For Each line In shapeBoundaries
            Dim intersection = startpoint.LineTo(endpoint).GetIntersectionPointWith(line)
            If intersection IsNot Nothing Then
                Return True
            End If
        Next

        Return False
    End Function

    <Extension>
    Public Function IsPointInsideShapeG(point As Vector2, shapeBoundaries As List(Of GeoLine), Optional angle As Double = 0, Optional rayLength As Double = 10000) As Boolean
        ' Define the endpoint of the ray based on the given angle and length
        Dim endpoint As New Vector2(point.X + rayLength * Math.Cos(angle), point.Y + rayLength * Math.Sin(angle))
        Dim intersectionCount As Integer = 0

        ' Loop through each boundary line of the shape
        For Each line In shapeBoundaries
            Dim intersection = New GeoLine(point, endpoint).GetIntersectionPointWith(line)

            ' If there is an intersection, increment the count
            If intersection IsNot Nothing Then intersectionCount += 1

        Next

        ' Return true if the intersection count is odd (inside), false if even (outside)
        Return intersectionCount Mod 2 = 1
    End Function


    <Extension>
    Public Function DistanceToG(point1 As Vector2, point2 As Vector2) As Double
        Return Math.Sqrt((point1.X - point2.X) ^ 2 + (point1.Y - point2.Y) ^ 2)
    End Function
    <Extension>
    Public Function DistanceToSquaredG(point1 As Vector2, point2 As Vector2) As Double
        Return (point1.X - point2.X) ^ 2 + (point1.Y - point2.Y) ^ 2
    End Function


    <Extension>
    Public Function ToPoint(vec As Vector2) As Point
        Return New Point(vec.X, vec.Y)
    End Function

End Module