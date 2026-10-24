Imports System.Globalization
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media
Imports System.Windows.Shapes

Imports PolyCut.Shared


Public Class GeometryHitTestHelper


    Public Shared Function GetRenderedBounds(drawable As IDrawable, relativeTo As UIElement) As Rect
        Dim bounds = Rect.Empty
        If drawable Is Nothing OrElse drawable.IsHidden OrElse drawable.DrawableElement Is Nothing Then Return bounds
        AccumulateRenderedBounds(drawable.DrawableElement, relativeTo, bounds, drawable)
        Return bounds
    End Function

    Private Shared Sub AccumulateRenderedBounds(visual As Visual, relativeTo As UIElement, ByRef bounds As Rect, Optional drawable As IDrawable = Nothing)
        Dim ui = TryCast(visual, UIElement)
        If ui IsNot Nothing AndAlso (Not ui.IsVisible OrElse ui.Opacity <= 0) Then Return

        ' WPF already supplies each shape's stretched geometry and complete stroke pen.
        Dim drawing = If(TypeOf visual Is Shape, VisualTreeHelper.GetDrawing(visual), Nothing)
        Dim textBox = TryCast(visual, TextBox)
        If textBox IsNot Nothing Then
            If drawable Is Nothing Then
                Dim wrapper = TryCast(textBox.Parent, ContentControl)
                If wrapper IsNot Nothing Then drawable = MetadataHelper.GetDrawableReference(wrapper)
            End If
            Dim pen = If(drawable IsNot Nothing AndAlso drawable.StrokeThickness > 0, New Pen(drawable.Stroke, drawable.StrokeThickness), Nothing)
            drawing = New DrawingGroup()
            drawing.Children.Add(New GeometryDrawing(textBox.Foreground, pen, TextGeometryHelper.BuildTextGeometry(textBox)))
        End If

        If drawing IsNot Nothing Then
            Dim painted As New DrawingGroup With {.Transform = New MatrixTransform(TransformMath.GeneralTransformToMatrix(visual.TransformToVisual(relativeTo)))}
            For Each part As GeometryDrawing In drawing.Children.OfType(Of GeometryDrawing)()
                Dim fill = If(HasPaint(part.Brush), part.Brush, Nothing)
                Dim pen = If(HasPaint(part.Pen?.Brush), part.Pen, Nothing)
                If fill IsNot Nothing OrElse pen IsNot Nothing Then painted.Children.Add(New GeometryDrawing(fill, pen, part.Geometry))
            Next
            bounds.Union(painted.Bounds)
            Return
        End If

        For i = 0 To VisualTreeHelper.GetChildrenCount(visual) - 1
            Dim child = TryCast(VisualTreeHelper.GetChild(visual, i), Visual)
            If child IsNot Nothing Then AccumulateRenderedBounds(child, relativeTo, bounds)
        Next
    End Sub

    Private Shared Function HasPaint(brush As Brush) As Boolean
        If brush Is Nothing OrElse brush.Opacity <= 0 Then Return False
        Dim solid = TryCast(brush, SolidColorBrush)
        Return solid Is Nothing OrElse solid.Color.A > 0
    End Function

    Public Shared Function GetTransformedGeometry(drawable As IDrawable) As Geometry
        If drawable?.DrawableElement Is Nothing Then Return Nothing

        Dim element = drawable.DrawableElement
        Dim wrapper = TryCast(element.Parent, ContentControl)
        If wrapper Is Nothing Then Return Nothing

        Dim geometry = BuildLocalGeometry(element)
        If geometry Is Nothing Then Return Nothing

        Dim canvas = TryCast(wrapper.Parent, UIElement)
        If canvas Is Nothing Then Return geometry

        Dim transform = TransformMath.GetAccumulatedMatrix(element, canvas)
        If TypeOf element Is GeometricShape Then
            Dim local = If(geometry.Transform?.Value, Matrix.Identity)
            local.Append(transform)
            transform = local
        End If
        geometry.Transform = New MatrixTransform(transform)
        Return geometry
    End Function

    Private Shared Function BuildLocalGeometry(element As FrameworkElement) As Geometry
        Dim geometry As Geometry = Nothing

        If TypeOf element Is GeometricShape Then
            geometry = DirectCast(element, GeometricShape).LocalGeometry()
        ElseIf TypeOf element Is TextBox Then
            Dim textBox = CType(element, TextBox)
            If Not String.IsNullOrEmpty(textBox.Text) Then
                'TODO: Fix the need for manual 3,1 offsets
                geometry = TextGeometryHelper.BuildTextGeometry(textBox, , TextGeometryHelper.GetContentOrigin(textBox, New Point(3, 1)))
            End If
        End If

        Return geometry
    End Function

    Private Const STROKE_HIT_PAD As Double = 2.0

    Public Shared Function ContainsPoint(drawable As IDrawable, point As Point) As Boolean
        If drawable Is Nothing OrElse drawable.IsHidden Then Return False

        Dim geometry = GetTransformedGeometry(drawable)
        If geometry Is Nothing Then Return False


        If drawable.Fill IsNot Nothing Then
            Dim solid = TryCast(drawable.Fill, SolidColorBrush)
            If solid Is Nothing OrElse solid.Color.A > 0 Then
                If geometry.FillContains(point, ToleranceType.Absolute, 0.5) Then Return True
            End If
        End If

        If drawable.Stroke IsNot Nothing Then
            Dim solidStroke = TryCast(drawable.Stroke, SolidColorBrush)
            If solidStroke Is Nothing OrElse solidStroke.Color.A > 0 Then
                Dim thickness = If(drawable.StrokeThickness > 0, drawable.StrokeThickness, 1.0)
                Dim hitPen As New Pen(drawable.Stroke, thickness + STROKE_HIT_PAD)
                Dim stroked = geometry.GetWidenedPathGeometry(hitPen)
                If stroked.FillContains(point, ToleranceType.Absolute, 0.5) Then Return True
            End If
        End If

        Return False
    End Function


    Public Shared Function HitTestTopmost(drawables As IEnumerable(Of IDrawable), point As Point) As IDrawable
        If drawables Is Nothing Then Return Nothing

        Dim list = drawables.ToList()
        For i = list.Count - 1 To 0 Step -1
            Dim d = list(i)
            If d Is Nothing OrElse d.DrawableElement Is Nothing Then Continue For

            Dim wrapper = TryCast(d.DrawableElement.Parent, ContentControl)
            If wrapper Is Nothing Then Continue For

            ' Only untransformed wrappers fit the simple bounds check; rotation/shear can extend beyond it.
            Dim rotate = TryCast(wrapper.RenderTransform, RotateTransform)
            If Not TypeOf d.DrawableElement Is GeometricShape AndAlso ((rotate IsNot Nothing AndAlso rotate.Angle = 0) OrElse wrapper.RenderTransform Is Nothing) Then
                Dim left = Canvas.GetLeft(wrapper)
                Dim top = Canvas.GetTop(wrapper)
                If Double.IsNaN(left) Then left = 0
                If Double.IsNaN(top) Then top = 0
                Dim size = TransformMath.GetWrapperSize(wrapper)
                If Not New Rect(left, top, size.Width, size.Height).Contains(point) Then Continue For
            End If

            If ContainsPoint(d, point) Then Return d
        Next

        Return Nothing
    End Function

End Class
