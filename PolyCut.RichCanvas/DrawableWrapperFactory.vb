Imports PolyCut.Shared


'TODO: Single source for wrapper creation to avoid split jobs between the canvas project and the main drawables. 
Public Module DrawableWrapperFactory

    Public Const MaxCachePixels As Double = 8192.0 * 8192.0
    Public Const MinCacheScale As Double = 0.25

    Public Function CacheScaleFor(zoom As Double, width As Double, height As Double) As Double
        Dim s As Double = If(Double.IsNaN(zoom) OrElse zoom <= 0, 1.0, zoom)
        If width > 0 AndAlso height > 0 AndAlso Not Double.IsNaN(width) AndAlso Not Double.IsNaN(height) Then
            Dim budgetScale As Double = Math.Sqrt(MaxCachePixels / (width * height))
            If budgetScale < s Then s = budgetScale
        End If
        Return Math.Max(MinCacheScale, s)
    End Function

    Public Function CreateWrapper(child As FrameworkElement, parentIDrawable As IDrawable, designerItemStyle As Style) As ContentControl

        If child Is Nothing Then Return Nothing

        Dim w As Double = If(Not Double.IsNaN(child.Width) AndAlso child.Width > 0, child.Width, child.ActualWidth)
        Dim h As Double = If(Not Double.IsNaN(child.Height) AndAlso child.Height > 0, child.Height, child.ActualHeight)
        Dim geometric = TryCast(child, GeometricShape)
        If geometric IsNot Nothing Then
            w = If(Double.IsNaN(child.Width), geometric.Data.Bounds.Width, child.Width)
            h = If(Double.IsNaN(child.Height), geometric.Data.Bounds.Height, child.Height)
        End If

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

        If TypeOf child Is GeometricShape OrElse TypeOf child Is Viewbox Then
            wrapper.MinWidth = 0
            wrapper.MinHeight = 0
        End If

        MetadataHelper.SetOriginalDimensions(wrapper, (wrapper.Width, wrapper.Height))

        'FUTRE ME STOP THINKING YOU CAN REMOVE THE NEED FOR STRETCH YOU ARE NOT SMART ENOUGH FOR THIS.
        'YOU DON'T KNOW HOW TO RECALCULATE POINT LOCATIONS, MIRRORS, STROKSE AND SHIT SO DON'T WASTE ANOTHER WEEKEND ON THIS
        'EVERY TIME YOU TRY YOU GET STUCK WONDERING WHY THE STROKE ENDS UP DEFORMED WHEN SCALING AND THET RANSFORM GIZMO STOPS BEING ALIGNED PROPERLY
        'Child stretches to the wrapper size (Stretch=Fill layout) so strokes are drawn at constant width
        child.HorizontalAlignment = HorizontalAlignment.Stretch
        ' Geometric shapes start top-aligned for bare canvas placement. Once hosted,
        ' both axes must fill the wrapper after their explicit dimensions are cleared.
        If TypeOf child Is GeometricShape Then child.VerticalAlignment = VerticalAlignment.Stretch
        child.Width = Double.NaN
        child.Height = Double.NaN

        Canvas.SetLeft(wrapper, If(Double.IsNaN(Canvas.GetLeft(child)), 0, Canvas.GetLeft(child)))
        Canvas.SetTop(wrapper, If(Double.IsNaN(Canvas.GetTop(child)), 0, Canvas.GetTop(child)))

        If parentIDrawable IsNot Nothing Then MetadataHelper.SetDrawableReference(wrapper, parentIDrawable)

        If TypeOf child Is GeometricShape OrElse TypeOf child Is Path OrElse
           TypeOf child Is Polyline OrElse
           TypeOf child Is Polygon Then
            PerformantRasteriser.SetCacheEligible(wrapper, True)
        End If

        Return wrapper
    End Function

End Module
