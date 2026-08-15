Imports System.Globalization
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media
Imports System.Windows.Shapes

Imports PolyCut.Shared


Public Class GeometryHitTestHelper


    Public Shared Function GetTransformedGeometry(drawable As IDrawable) As Geometry
        If drawable?.DrawableElement Is Nothing Then Return Nothing

        Dim element = drawable.DrawableElement
        Dim wrapper = TryCast(element.Parent, ContentControl)
        If wrapper Is Nothing Then Return Nothing

        Dim geometry As Geometry = Nothing

        If TypeOf element Is Rectangle Then
            Dim rect = CType(element, Rectangle)
            geometry = New RectangleGeometry(New Rect(0, 0, rect.ActualWidth, rect.ActualHeight))

        ElseIf TypeOf element Is Ellipse Then
            Dim ellipse = CType(element, Ellipse)
            Dim radiusX = ellipse.ActualWidth / 2
            Dim radiusY = ellipse.ActualHeight / 2
            geometry = New EllipseGeometry(New Point(radiusX, radiusY), radiusX, radiusY)

        ElseIf TypeOf element Is Line Then
            Dim line = CType(element, Line)
            Dim lineGeometry As New LineGeometry(New Point(line.X1, line.Y1), New Point(line.X2, line.Y2))
            Dim thickness = If(line.StrokeThickness > 0, line.StrokeThickness, 1.0)
            geometry = lineGeometry.GetWidenedPathGeometry(New Pen(Brushes.Black, thickness))

        ElseIf TypeOf element Is Path Then
            Dim path = CType(element, Path)
            If path.Data IsNot Nothing Then
                geometry = path.Data.Clone()
            End If

        ElseIf TypeOf element Is TextBox Then
            Dim textBox = CType(element, TextBox)
            If Not String.IsNullOrEmpty(textBox.Text) Then
                Dim formattedText As New FormattedText(
                    textBox.Text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    New Typeface(textBox.FontFamily, textBox.FontStyle, textBox.FontWeight, textBox.FontStretch),
                    textBox.FontSize,
                    Brushes.Black,
                    1.0)

                Dim contentOrigin As Point = New Point(3, 1)
                Dim firstCharRect = textBox.GetRectFromCharacterIndex(0, False)
                If Not firstCharRect.IsEmpty AndAlso Not Double.IsNaN(firstCharRect.X) AndAlso Not Double.IsNaN(firstCharRect.Y) Then
                    contentOrigin = New Point(firstCharRect.X, firstCharRect.Y)
                End If
                geometry = formattedText.BuildGeometry(contentOrigin)
            End If
        End If

        If geometry Is Nothing Then Return Nothing

        Dim elementTransformGroup = TryCast(element.RenderTransform, TransformGroup)
        If elementTransformGroup IsNot Nothing Then
            Dim elementScale = elementTransformGroup.Children.OfType(Of ScaleTransform)().FirstOrDefault()
            If elementScale IsNot Nothing Then
                Dim scaleTransform = New ScaleTransform(elementScale.ScaleX, elementScale.ScaleY,
                    geometry.Bounds.Width / 2, geometry.Bounds.Height / 2)
                geometry = Geometry.Combine(geometry, geometry, GeometryCombineMode.Union, scaleTransform)
            End If
        End If

        Dim transformGroup As New TransformGroup()

        If Not TypeOf element Is TextBox Then
            If geometry.Bounds.Width > 0 AndAlso geometry.Bounds.Height > 0 Then
                Dim scaleX = wrapper.ActualWidth / geometry.Bounds.Width
                Dim scaleY = wrapper.ActualHeight / geometry.Bounds.Height
                transformGroup.Children.Add(New ScaleTransform(scaleX, scaleY))
            End If
        End If

        Dim rotateTransform = TryCast(wrapper.RenderTransform, RotateTransform)
        If rotateTransform IsNot Nothing Then
            transformGroup.Children.Add(New RotateTransform(rotateTransform.Angle,
                wrapper.ActualWidth / 2, wrapper.ActualHeight / 2))
        End If

        Dim left = Canvas.GetLeft(wrapper)
        Dim top = Canvas.GetTop(wrapper)
        If Not Double.IsNaN(left) AndAlso Not Double.IsNaN(top) Then
            transformGroup.Children.Add(New TranslateTransform(left, top))
        End If

        Return Geometry.Combine(geometry, geometry, GeometryCombineMode.Union, transformGroup)
    End Function

    Private Const STROKE_HIT_PAD As Double = 2.0

    Public Shared Function ContainsPoint(drawable As IDrawable, point As Point) As Boolean
        If drawable Is Nothing OrElse drawable.IsHidden Then Return False

        Dim geometry = GetTransformedGeometry(drawable)
        If geometry Is Nothing Then Return False

        ' Lines are widened into a filled body, so their "fill" is the stroke region.
        If TypeOf drawable.DrawableElement Is Line Then
            Dim padded = geometry.GetWidenedPathGeometry(New Pen(Brushes.Black, STROKE_HIT_PAD))
            Return padded.FillContains(point, ToleranceType.Absolute, 0.5)
        ElseIf drawable.Fill IsNot Nothing Then
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

            ' Quick reject by wrapper bounds (skip for rotated wrappers, whosegeometry can extend outside the AABB).
            Dim rotate = TryCast(wrapper.RenderTransform, RotateTransform)
            If rotate Is Nothing OrElse rotate.Angle = 0 Then
                Dim left = Canvas.GetLeft(wrapper)
                Dim top = Canvas.GetTop(wrapper)
                If Double.IsNaN(left) Then left = 0
                If Double.IsNaN(top) Then top = 0
                If Not New Rect(left, top, wrapper.ActualWidth, wrapper.ActualHeight).Contains(point) Then Continue For
            End If

            If ContainsPoint(d, point) Then Return d
        Next

        Return Nothing
    End Function

End Class