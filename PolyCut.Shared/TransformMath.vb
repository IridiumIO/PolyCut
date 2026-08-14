Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media


Public Module TransformMath

    'This is how GeometryExtractor flattens TransformToVisual results
    Public Function GeneralTransformToMatrix(gt As GeneralTransform) As Matrix
        If gt Is Nothing Then Return Matrix.Identity

        Dim p0 = gt.Transform(New Point(0, 0))
        Dim p1 = gt.Transform(New Point(1, 0))
        Dim p2 = gt.Transform(New Point(0, 1))

        Return New Matrix(p1.X - p0.X, p1.Y - p0.Y, p2.X - p0.X, p2.Y - p0.Y, p0.X, p0.Y)
    End Function

    'Get AABB of rect after matrix transform
    Public Function TransformBounds(m As Matrix, r As Rect) As Rect
        If r.IsEmpty Then Return Rect.Empty

        Dim topLeft = m.Transform(New Point(r.Left, r.Top))
        Dim topRight = m.Transform(New Point(r.Right, r.Top))
        Dim bottomLeft = m.Transform(New Point(r.Left, r.Bottom))
        Dim bottomRight = m.Transform(New Point(r.Right, r.Bottom))

        Dim minX = Math.Min(topLeft.X, Math.Min(topRight.X, Math.Min(bottomLeft.X, bottomRight.X)))
        Dim minY = Math.Min(topLeft.Y, Math.Min(topRight.Y, Math.Min(bottomLeft.Y, bottomRight.Y)))
        Dim maxX = Math.Max(topLeft.X, Math.Max(topRight.X, Math.Max(bottomLeft.X, bottomRight.X)))
        Dim maxY = Math.Max(topLeft.Y, Math.Max(topRight.Y, Math.Max(bottomLeft.Y, bottomRight.Y)))

        Return New Rect(minX, minY, maxX - minX, maxY - minY)
    End Function

    'Accumulated matrix from an element to a visual ancestor. TODO handle the guard (no idea why it was throwing, can't see vsual issues)
    Public Function GetAccumulatedMatrix(element As FrameworkElement, relativeTo As UIElement) As Matrix
        If element Is Nothing OrElse relativeTo Is Nothing Then Return Matrix.Identity

        Try
            Dim gt = element.TransformToVisual(relativeTo)
            If gt Is Nothing Then Return Matrix.Identity
            Return GeneralTransformToMatrix(gt)
        Catch
            Return Matrix.Identity
        End Try
    End Function


    Public Function ComputeResizePlacement(left As Double, top As Double,
                                           w As Double, h As Double,
                                           angleDeg As Double,
                                           origin As Point,
                                           newW As Double, newH As Double,
                                           moveTop As Boolean, moveLeft As Boolean) As (Left As Double, Top As Double)

        Dim angle = angleDeg * Math.PI / 180.0
        Dim cosA = Math.Cos(angle)
        Dim sinA = Math.Sin(angle)

        ' Fixed corner's local coordinates in the old and new wrapper frames.
        ' (Moving left edge anchors the right edge; moving top anchors the bottom.)
        Dim fxOld = If(moveLeft, w, 0.0)
        Dim fyOld = If(moveTop, h, 0.0)
        Dim fxNew = If(moveLeft, newW, 0.0)
        Dim fyNew = If(moveTop, newH, 0.0)

        ' World position of the fixed corner under the OLD transform
        Dim oldPivotX = left + origin.X * w
        Dim oldPivotY = top + origin.Y * h
        Dim fixedWorldX = oldPivotX + cosA * (fxOld - origin.X * w) - sinA * (fyOld - origin.Y * h)
        Dim fixedWorldY = oldPivotY + sinA * (fxOld - origin.X * w) + cosA * (fyOld - origin.Y * h)

        ' New rotation pivot (origin is relative to the NEW size)
        Dim newPivotX = origin.X * newW
        Dim newPivotY = origin.Y * newH

        ' Solve: T(newLeft,newTop) · RotAt(θ, newPivot) maps (fxNew,fyNew) → fixedWorld
        Dim rotatedX = cosA * (fxNew - newPivotX) - sinA * (fyNew - newPivotY)
        Dim rotatedY = sinA * (fxNew - newPivotX) + cosA * (fyNew - newPivotY)

        Return (fixedWorldX - newPivotX - rotatedX, fixedWorldY - newPivotY - rotatedY)
    End Function


    Public Function RotatedCornersOf(wrapper As ContentControl) As List(Of Point)
        Dim result As New List(Of Point)
        If wrapper Is Nothing Then Return result

        Dim left = Canvas.GetLeft(wrapper)
        Dim top = Canvas.GetTop(wrapper)
        Dim width = wrapper.ActualWidth
        Dim height = wrapper.ActualHeight

        Dim rotationAngle As Double = 0
        Dim rotateTransform = TryCast(wrapper.RenderTransform, RotateTransform)
        If rotateTransform IsNot Nothing Then
            rotationAngle = rotateTransform.Angle * Math.PI / 180.0
        End If

        Dim transformOrigin = wrapper.RenderTransformOrigin
        Dim pivotX = left + width * transformOrigin.X
        Dim pivotY = top + height * transformOrigin.Y

        Dim corners() As Point = {
            New Point(left, top),
            New Point(left + width, top),
            New Point(left + width, top + height),
            New Point(left, top + height)
        }

        For Each corner In corners
            Dim dx = corner.X - pivotX
            Dim dy = corner.Y - pivotY
            Dim rotatedX = pivotX + (dx * Math.Cos(rotationAngle) - dy * Math.Sin(rotationAngle))
            Dim rotatedY = pivotY + (dx * Math.Sin(rotationAngle) + dy * Math.Cos(rotationAngle))
            result.Add(New Point(rotatedX, rotatedY))
        Next

        Return result
    End Function

End Module
