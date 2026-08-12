Imports System.Windows.Media.Animation

Public Class DrawableRegistrationMark : Inherits DrawableRectangle

    'Animated because why not?

    Public Sub New(element As Rectangle)
        MyBase.New(element)
        Name = "Registration Mark"
        StartRainbowAnimation()
    End Sub

    Private ReadOnly _rainbowBrush As New DrawingBrush()
    Private _animationStarted As Boolean

    Private Sub StartRainbowAnimation()
        If _animationStarted Then Return
        _animationStarted = True

        Dim rect = TryCast(DrawableElement, Rectangle)
        If rect Is Nothing Then Return

        Dim group As New DrawingGroup()

        group.Children.Add(New GeometryDrawing(
            Brushes.DeepSkyBlue,
            Nothing,
            New RectangleGeometry(New Rect(0, 0, 1, 1))
        ))

        AddBlob(group, Color.FromArgb(&HFF, &H27, &H2B, &HFB), New Point(0.2, 0.3), New Point(0.8, 0.7), 5) '272bfb
        AddBlob(group, Color.FromArgb(&HFF, &H21, &H72, &HC1), New Point(0.8, 0.2), New Point(0.25, 0.8), 7) '2172c1
        AddBlob(group, Color.FromArgb(&HFF, &H1C, &HB8, &H86), New Point(0.5, 0.8), New Point(0.7, 0.15), 9) '1CB886
        AddBlob(group, Color.FromArgb(&HFF, &H16, &HFF, &H4C), New Point(0.1, 0.7), New Point(0.9, 0.45), 11) '16FF4C

        _rainbowBrush.Drawing = group
        _rainbowBrush.Stretch = Stretch.Fill

        rect.Fill = _rainbowBrush
    End Sub

    Private Shared Sub AddBlob(group As DrawingGroup, color As Color, fromPoint As Point, toPoint As Point, seconds As Double)
        Dim brush As New RadialGradientBrush With {
            .Center = New Point(0.5, 0.5),
            .GradientOrigin = New Point(0.5, 0.5),
            .RadiusX = 0.7,
            .RadiusY = 0.7
        }

        brush.GradientStops.Add(New GradientStop(color, 0))
        brush.GradientStops.Add(New GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1))

        Dim transform As New TranslateTransform(fromPoint.X - 0.5, fromPoint.Y - 0.5)
        brush.RelativeTransform = transform

        group.Children.Add(New GeometryDrawing(brush, Nothing, New RectangleGeometry(New Rect(0, 0, 1, 1))))

        Dim xAnimation As New DoubleAnimation(fromPoint.X - 0.5, toPoint.X - 0.5, TimeSpan.FromSeconds(seconds)) With {
            .AutoReverse = True,
            .RepeatBehavior = RepeatBehavior.Forever
        }

        Timeline.SetDesiredFrameRate(xAnimation, 15)
        transform.BeginAnimation(TranslateTransform.XProperty, xAnimation)

        Dim yAnimation As New DoubleAnimation(fromPoint.Y - 0.5, toPoint.Y - 0.5, TimeSpan.FromSeconds(seconds * 1.07)) With {
            .AutoReverse = True,
            .RepeatBehavior = RepeatBehavior.Forever
        }

        Timeline.SetDesiredFrameRate(yAnimation, 15)
        transform.BeginAnimation(TranslateTransform.YProperty, yAnimation)
    End Sub

    Public Overrides Sub ApplyVisualStyle()
        MyBase.ApplyVisualStyle()

        If _animationStarted Then
            Dim rect = TryCast(DrawableElement, Rectangle)
            If rect IsNot Nothing Then rect.Fill = _rainbowBrush
        End If
    End Sub

End Class