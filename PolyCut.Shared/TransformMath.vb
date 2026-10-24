Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media


Public Module TransformMath

    ' SVG geometry -> geometric wrapper. Fit axes independently; a zero extent
    ' stays a line and never needs artificial sizing segments or pen padding.
    Public Function GeometryBoundsTransform(bounds As Rect, size As Size) As Matrix
        If bounds.IsEmpty Then Return Matrix.Identity
        Dim fit = Matrix.Identity
        fit.Translate(-bounds.X, -bounds.Y)
        fit.Scale(If(bounds.Width > 0, size.Width / bounds.Width, 1),
                     If(bounds.Height > 0, size.Height / bounds.Height, 1))
        Return fit
    End Function

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
                                           anchor As Point, Optional affine As Matrix? = Nothing) As (Left As Double, Top As Double)

        Dim m = If(affine, Matrix.Identity)
        m.Rotate(angleDeg)

        Dim fxOld = anchor.X * w
        Dim fyOld = anchor.Y * h
        Dim fxNew = anchor.X * newW
        Dim fyNew = anchor.Y * newH

        Dim oldPivotX = left + origin.X * w
        Dim oldPivotY = top + origin.Y * h

        Dim oldOffset = m.Transform(New Vector(fxOld - origin.X * w, fyOld - origin.Y * h))
        Dim fixedWorldX = oldPivotX + oldOffset.X
        Dim fixedWorldY = oldPivotY + oldOffset.Y

        Dim newPivotX = origin.X * newW
        Dim newPivotY = origin.Y * newH

        Dim newOffset = m.Transform(New Vector(fxNew - newPivotX, fyNew - newPivotY))

        Return (fixedWorldX - newPivotX - newOffset.X, fixedWorldY - newPivotY - newOffset.Y)
    End Function


    Public Function GetWrapperSize(wrapper As FrameworkElement) As (Width As Double, Height As Double)
        If wrapper Is Nothing Then Return (0.0, 0.0)

        Dim w As Double = If(Double.IsNaN(wrapper.Width), wrapper.ActualWidth, wrapper.Width)
        Dim h As Double = If(Double.IsNaN(wrapper.Height), wrapper.ActualHeight, wrapper.Height)
        Return (w, h)
    End Function


    ' Axis-aligned bounds of a wrapper in its parent (canvas) coordinate space.
    Public Function GetWorldBounds(wrapper As ContentControl) As Rect
        If wrapper Is Nothing Then Return Rect.Empty

        Dim left = Canvas.GetLeft(wrapper)
        Dim top = Canvas.GetTop(wrapper)
        If Double.IsNaN(left) Then left = 0
        If Double.IsNaN(top) Then top = 0

        Dim size = GetWrapperSize(wrapper)
        Dim width = size.Width
        Dim height = size.Height
        Dim parentCanvas = TryCast(wrapper.Parent, UIElement)
        Dim m = GetAccumulatedMatrix(wrapper, parentCanvas)

        If m.IsIdentity Then Return New Rect(left, top, width, height)

        Return New MatrixTransform(m).TransformBounds(New Rect(0, 0, width, height))
    End Function



    'vvvv   duplidcated logic until I can decide where to unify this stuff

    Public Function ComputeSelectionFrame(items As IEnumerable(Of IDrawable), frameAngle As Double) As (Origin As Point, Frame As Rect)
        Dim corners As New List(Of Point)
        If items IsNot Nothing Then
            For Each item In items
                If item?.DrawableElement Is Nothing Then Continue For

                Dim wrapper = TryCast(item.DrawableElement.Parent, ContentControl)
                If wrapper Is Nothing Then Continue For

                corners.AddRange(FootprintCorners(wrapper))
            Next
        End If

        If corners.Count = 0 Then Return (New Point(0, 0), Rect.Empty)

        Dim world = BoundsOf(corners)
        Dim origin As New Point(world.Left + world.Width / 2, world.Top + world.Height / 2)

        Dim local As New List(Of Point)
        For Each corner In corners
            local.Add(RotateAbout(corner, origin, -frameAngle))
        Next

        Return (origin, BoundsOf(local))
    End Function

    Private Function FootprintCorners(wrapper As ContentControl) As List(Of Point)
        Dim result As New List(Of Point)
        If wrapper Is Nothing Then Return result

        Dim left = Canvas.GetLeft(wrapper)
        Dim top = Canvas.GetTop(wrapper)
        If Double.IsNaN(left) Then left = 0
        If Double.IsNaN(top) Then top = 0

        Dim size = GetWrapperSize(wrapper)
        Dim origin = wrapper.RenderTransformOrigin
        Dim m = Matrix.Identity
        m.Translate(-origin.X * size.Width, -origin.Y * size.Height)
        m.Append(If(wrapper.RenderTransform?.Value, Matrix.Identity))
        m.Translate(left + origin.X * size.Width, top + origin.Y * size.Height)

        result.Add(m.Transform(New Point(0, 0)))
        result.Add(m.Transform(New Point(size.Width, 0)))
        result.Add(m.Transform(New Point(size.Width, size.Height)))
        result.Add(m.Transform(New Point(0, size.Height)))

        Return result
    End Function


    Public Function RotateAbout(p As Point, center As Point, angleDegrees As Double) As Point
        If Math.Abs(angleDegrees) < 0.0001 Then Return p

        Dim rad = angleDegrees * Math.PI / 180.0
        Dim cosA = Math.Cos(rad)
        Dim sinA = Math.Sin(rad)
        Dim dx = p.X - center.X
        Dim dy = p.Y - center.Y

        'This fucker 
        Return New Point(center.X + dx * cosA - dy * sinA, center.Y + dx * sinA + dy * cosA)
    End Function

    Private Function BoundsOf(points As IEnumerable(Of Point)) As Rect
        Dim minX = Double.MaxValue
        Dim minY = Double.MaxValue
        Dim maxX = Double.MinValue
        Dim maxY = Double.MinValue

        For Each p In points
            minX = Math.Min(minX, p.X)
            minY = Math.Min(minY, p.Y)
            maxX = Math.Max(maxX, p.X)
            maxY = Math.Max(maxY, p.Y)
        Next

        If minX = Double.MaxValue Then Return Rect.Empty
        Return New Rect(minX, minY, maxX - minX, maxY - minY)
    End Function

End Module
