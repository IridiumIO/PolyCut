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
                                           anchor As Point) As (Left As Double, Top As Double)

        Dim angle = angleDeg * Math.PI / 180.0
        Dim cosA = Math.Cos(angle)
        Dim sinA = Math.Sin(angle)

        Dim fxOld = anchor.X * w
        Dim fyOld = anchor.Y * h
        Dim fxNew = anchor.X * newW
        Dim fyNew = anchor.Y * newH

        Dim oldPivotX = left + origin.X * w
        Dim oldPivotY = top + origin.Y * h

        Dim fixedWorldX = oldPivotX + cosA * (fxOld - origin.X * w) - sinA * (fyOld - origin.Y * h)
        Dim fixedWorldY = oldPivotY + sinA * (fxOld - origin.X * w) + cosA * (fyOld - origin.Y * h)

        Dim newPivotX = origin.X * newW
        Dim newPivotY = origin.Y * newH

        Dim rotatedX = cosA * (fxNew - newPivotX) - sinA * (fyNew - newPivotY)
        Dim rotatedY = sinA * (fxNew - newPivotX) + cosA * (fyNew - newPivotY)

        Return (fixedWorldX - newPivotX - rotatedX, fixedWorldY - newPivotY - rotatedY)
    End Function


    ' Axis-aligned bounds of a wrapper in its parent (canvas) coordinate space.
    Public Function GetWorldBounds(wrapper As ContentControl) As Rect
        If wrapper Is Nothing Then Return Rect.Empty

        Dim left = Canvas.GetLeft(wrapper)
        Dim top = Canvas.GetTop(wrapper)
        If Double.IsNaN(left) Then left = 0
        If Double.IsNaN(top) Then top = 0

        Dim width = wrapper.ActualWidth
        Dim height = wrapper.ActualHeight
        If width <= 0 OrElse height <= 0 Then Return New Rect(left, top, width, height)

        Dim parentCanvas = TryCast(wrapper.Parent, UIElement)
        Dim m = GetAccumulatedMatrix(wrapper, parentCanvas)

        If m.IsIdentity Then Return New Rect(left, top, width, height)

        Return New MatrixTransform(m).TransformBounds(New Rect(0, 0, width, height))
    End Function

End Module
