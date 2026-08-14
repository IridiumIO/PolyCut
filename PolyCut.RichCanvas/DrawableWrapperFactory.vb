Imports PolyCut.Shared


'TODO: Single source for wrapper creation to avoid split jobs between the canvas project and the main drawables. 
Public Module DrawableWrapperFactory

    Public Function CreateWrapper(child As FrameworkElement, parentIDrawable As IDrawable, designerItemStyle As Style) As ContentControl

        If child Is Nothing Then Return Nothing

        Dim w As Double = If(Not Double.IsNaN(child.Width) AndAlso child.Width > 0, child.Width, child.ActualWidth)
        Dim h As Double = If(Not Double.IsNaN(child.Height) AndAlso child.Height > 0, child.Height, child.ActualHeight)

        Dim wrapper As New ContentControl With {
            .Content = child,
            .Width = w,
            .Height = h,
            .RenderTransform = New RotateTransform(0),
            .Background = Brushes.Transparent,
            .IsHitTestVisible = True,
            .ClipToBounds = False,
            .Style = designerItemStyle
        }

        If TypeOf child Is Canvas Then DirectCast(child, Canvas).ClipToBounds = True

        If TypeOf child Is Line Then

            Dim line = DirectCast(child, Line)
            wrapper.Width = Math.Abs(line.X2 - line.X1) + line.StrokeThickness
            wrapper.Height = Math.Abs(line.Y2 - line.Y1) + line.StrokeThickness
            FitLineToWrapper(line, wrapper, line.StrokeThickness)
            MetadataHelper.SetOriginalEndPoint(wrapper, New Point(line.X2, line.Y2))

        ElseIf TypeOf child Is Path Then
            DirectCast(child, Path).Stretch = Stretch.Fill
        End If

        MetadataHelper.SetOriginalDimensions(wrapper, (wrapper.Width, wrapper.Height))

        'FUTRE ME STOP THINKING YOU CAN REMOVE THE NEED FOR STRETCH YOU ARE NOT SMART ENOUGH FOR THIS.
        'YOU DON'T KNOW HOW TO RECALCULATE POINT LOCATIONS, MIRRORS, STROKSE AND SHIT SO DON'T WASTE ANOTHER WEEKEND ON THIS
        'EVERY TIME YOU TRY YOU GET STUCK WONDERING WHY THE STROKE ENDS UP DEFORMED WHEN SCALING AND THET RANSFORM GIZMO STOPS BEING ALIGNED PROPERLY
        'Child stretches to the wrapper size (Stretch=Fill layout) so strokes are drawn at constant width
        child.HorizontalAlignment = HorizontalAlignment.Stretch
        child.Width = Double.NaN
        child.Height = Double.NaN

        Canvas.SetLeft(wrapper, If(Double.IsNaN(Canvas.GetLeft(child)), 0, Canvas.GetLeft(child)))
        Canvas.SetTop(wrapper, If(Double.IsNaN(Canvas.GetTop(child)), 0, Canvas.GetTop(child)))

        If parentIDrawable IsNot Nothing Then MetadataHelper.SetDrawableReference(wrapper, parentIDrawable)

        Return wrapper
    End Function


    Public Sub FitLineToWrapper(line As Line, wrapper As ContentControl, strokeThickness As Double)
        If line Is Nothing OrElse wrapper Is Nothing Then Return

        Dim w As Double = If(Double.IsNaN(wrapper.Width), wrapper.ActualWidth, wrapper.Width)
        Dim h As Double = If(Double.IsNaN(wrapper.Height), wrapper.ActualHeight, wrapper.Height)
        If w <= 0 OrElse h <= 0 Then Return

        Dim half As Double = Math.Max(0.0, strokeThickness) * 0.5

        ' Clamp so we don't go negative when wrapper is tiny
        Dim xMin As Double = Math.Min(w * 0.5, half)
        Dim yMin As Double = Math.Min(h * 0.5, half)
        Dim xMax As Double = Math.Max(xMin, w - half)
        Dim yMax As Double = Math.Max(yMin, h - half)

        ' Preserve direction (so reverse lines don't flip)
        Dim leftToRight As Boolean = (line.X2 >= line.X1)
        Dim topToBottom As Boolean = (line.Y2 >= line.Y1)

        line.X1 = If(leftToRight, xMin, xMax)
        line.Y1 = If(topToBottom, yMin, yMax)
        line.X2 = If(leftToRight, xMax, xMin)
        line.Y2 = If(topToBottom, yMax, yMin)
    End Sub

End Module
