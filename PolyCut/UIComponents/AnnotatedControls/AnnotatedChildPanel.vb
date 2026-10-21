Public Class AnnotatedChildPanel
    Inherits StackPanel

    Protected Overrides Function ArrangeOverride(arrangeSize As Size) As Size
        Dim size = MyBase.ArrangeOverride(arrangeSize)
        InvalidateVisual()
        Return size
    End Function

    Protected Overrides Sub OnRender(dc As DrawingContext)
        MyBase.OnRender(dc)
        Dim pen As New Pen(DirectCast(GetValue(TextElement.ForegroundProperty), Brush), 1)
        Dim lastY As Double = 0
        dc.PushOpacity(0.4)
        For Each child As UIElement In Children
            If child.Visibility = Visibility.Collapsed Then Continue For
            Dim control = TryCast(child, AnnotatedControl)
            Dim anchor As UIElement = If(control Is Nothing, child, control.LabelBlock)
            Dim y = anchor.TranslatePoint(New Point(0, anchor.RenderSize.Height / 2 + 2), Me).Y
            dc.DrawLine(pen, New Point(-12.5, y), New Point(-3, y))
            lastY = y
        Next
        dc.DrawLine(pen, New Point(-12.5, 0), New Point(-12.5, lastY))
        dc.Pop()
    End Sub
End Class
