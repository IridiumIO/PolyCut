Imports System.Windows.Media

Public NotInheritable Class PerformantRasteriser

    Private Const ViewportPadFraction As Double = 0.25
    Private Const CacheSuperSample As Double = 2.0

    'Stupid ceiling for sanity fallback to vector rendering for pixel peepers
    Private Const MaxCacheZoom As Double = 84.0

    'Caching is only worth its bitmap cost once the geometry actually on screen is expensive to re-process. Check SEGMENTS not total shapes
    Private Const MinCacheWeight As Integer = 25000
    Private Const CacheWeightHysteresis As Double = 0.7
    Private Const TotalCacheBudgetPixels As Double = 512.0 * 1024.0 * 1024.0
    Private Const CacheRescaleBatchSize As Integer = 16
    Private Const RescaleWeightPerTick As Integer = 1000

    Private Const SettleMilliseconds As Double = 120.0
    Private Const FallbackExtent As Double = 1000000000.0

    Public Shared ReadOnly CacheEligibleProperty As DependencyProperty = DependencyProperty.RegisterAttached("CacheEligible", GetType(Boolean), GetType(PerformantRasteriser), New PropertyMetadata(False))

    Public Shared Sub SetCacheEligible(element As DependencyObject, value As Boolean)
        element.SetValue(CacheEligibleProperty, value)
    End Sub

    Public Shared Function GetCacheEligible(element As DependencyObject) As Boolean
        Return CBool(element.GetValue(CacheEligibleProperty))
    End Function

    Private Shared _enabled As Boolean = True
    Private Shared ReadOnly _live As New List(Of WeakReference)

    Public Shared Property Enabled As Boolean
        Get
            Return _enabled
        End Get
        Set(value As Boolean)
            If _enabled = value Then Return
            _enabled = value
            For i As Integer = _live.Count - 1 To 0 Step -1
                Dim target = TryCast(_live(i).Target, PerformantRasteriser)
                If target Is Nothing Then
                    _live.RemoveAt(i)
                Else
                    target.Invalidate()
                End If
            Next
        End Set
    End Property

    Private Structure CacheCandidate
        Public Element As FrameworkElement
        Public Weight As Integer
        Public Slot As Rect
        Public HasSlot As Boolean
    End Structure

    Private NotInheritable Class CullContainer
        Public ReadOnly Container As Canvas
        Public ReadOnly Children As List(Of CacheCandidate)

        Public Sub New(container As Canvas, children As List(Of CacheCandidate))
            Me.Container = container
            Me.Children = children
        End Sub
    End Class

    Private Structure VisibleEntry
        Public Element As FrameworkElement
        Public UnitScale As Double
        Public Width As Double
        Public Height As Double
        Public Weight As Integer
    End Structure

    Private Structure RescaleEntry
        Public Element As FrameworkElement
        Public Scale As Double
        Public Weight As Integer
    End Structure

    Private ReadOnly _canvas As PolyCanvas
    Private _viewport As FrameworkElement

    Private _containers As List(Of CullContainer)
    Private _containersDirty As Boolean = True
    Private _structureChanged As Boolean = True
    Private ReadOnly _weightMemo As New Dictionary(Of Geometry, Integer)
    Private ReadOnly _visibleSet As New HashSet(Of FrameworkElement)
    Private ReadOnly _visible As New List(Of VisibleEntry)

    Private ReadOnly _rescaleQueue As New List(Of RescaleEntry)
    Private _rescaleIndex As Integer
    Private _rescaleTimer As System.Windows.Threading.DispatcherTimer

    Private _settleTimer As System.Windows.Threading.DispatcherTimer
    Private _pendingZoom As Double = 1.0
    Private _settledZoom As Double = 1.0
    Private _viewportRect As Rect = Rect.Empty
    Private _hasViewport As Boolean
    Private _cachesActive As Boolean


    Public Sub New(canvas As PolyCanvas)
        _canvas = canvas
        _live.Add(New WeakReference(Me))
        AddHandler canvas.LayoutUpdated, AddressOf OnCanvasLayoutUpdated
    End Sub

    Public Sub AttachViewport(viewport As FrameworkElement)
        If _viewport Is viewport Then Return
        If _viewport IsNot Nothing Then RemoveHandler _viewport.SizeChanged, AddressOf OnViewportSizeChanged
        _viewport = viewport
        If viewport IsNot Nothing Then AddHandler viewport.SizeChanged, AddressOf OnViewportSizeChanged
    End Sub

    Public Sub Invalidate()
        'The rebuild is deferred to the next layout pass rather than run here: after an import the wrappers
        'have no applied template yet, so walking the tree now would catalogue only the top-level wrappers
        'and miss every leaf inside a group.
        _weightMemo.Clear()
        _structureChanged = True
        _containers = Nothing
        _containersDirty = True
    End Sub

    Public Sub CancelRescale()
        _rescaleQueue.Clear()
        _rescaleIndex = 0
        If _rescaleTimer IsNot Nothing Then _rescaleTimer.Stop()
    End Sub

    Private Shared Function CachingApplies(zoom As Double) As Boolean
        Return _enabled AndAlso zoom <= MaxCacheZoom
    End Function

    Public Sub ViewportChanged(zoom As Double, zoomChanged As Boolean)
        _pendingZoom = zoom
        If _viewport Is Nothing Then Return

        CancelRescale()

        If Not CachingApplies(zoom) Then
            StartSettleTimer()
            Return
        End If

        If zoomChanged AndAlso zoom >= _settledZoom Then
            StartSettleTimer()
            Return
        End If

        Dim bounds = VisibleCanvasRect()
        If bounds.IsEmpty Then
            StartSettleTimer()
            Return
        End If
        _viewportRect = bounds
        _hasViewport = True


        Apply(zoom, bounds, False)
        StartSettleTimer()
    End Sub

    Private Sub StartSettleTimer()
        If _settleTimer Is Nothing Then
            _settleTimer = New Threading.DispatcherTimer(Threading.DispatcherPriority.Background) With {
                .Interval = TimeSpan.FromMilliseconds(SettleMilliseconds)
            }
            AddHandler _settleTimer.Tick, AddressOf OnSettleTick
        End If
        _settleTimer.Stop()
        _settleTimer.Start()
    End Sub

    Private Sub OnSettleTick(sender As Object, e As EventArgs)
        _settleTimer.Stop()
        _settledZoom = _pendingZoom
        If _viewport Is Nothing Then Return
        Dim bounds = VisibleCanvasRect()
        If bounds.IsEmpty Then Return
        _viewportRect = bounds
        _hasViewport = True
        Apply(_settledZoom, bounds, True)
    End Sub

    Private Sub OnViewportSizeChanged(sender As Object, e As SizeChangedEventArgs)
        If _viewport Is Nothing Then Return
        Dim bounds = VisibleCanvasRect()
        If bounds.IsEmpty Then Return
        _viewportRect = bounds
        _hasViewport = True
        Apply(_settledZoom, bounds, True)
    End Sub

    Private Sub OnCanvasLayoutUpdated(sender As Object, e As EventArgs)

        If Not _structureChanged Then Return
        _structureChanged = False
        _containers = Nothing
        _containersDirty = True
        If _hasViewport Then
            Apply(_settledZoom, _viewportRect, True)
        Else
            Apply(1.0, New Rect(-FallbackExtent, -FallbackExtent, FallbackExtent * 2, FallbackExtent * 2), True)
        End If
    End Sub

    Private Function VisibleCanvasRect() As Rect
        Try
            Dim toCanvas = _viewport.TransformToDescendant(_canvas)
            If toCanvas IsNot Nothing Then
                Dim bounds = toCanvas.TransformBounds(New Rect(0, 0, Math.Max(1.0, _viewport.ActualWidth), Math.Max(1.0, _viewport.ActualHeight)))
                If Not bounds.IsEmpty AndAlso bounds.Width > 0 AndAlso bounds.Height > 0 Then Return bounds
            End If
        Catch
        End Try
        Return Rect.Empty
    End Function

    Private ReadOnly Property DeviceScale As Double
        Get
            Try
                Return VisualTreeHelper.GetDpi(_canvas).DpiScaleX
            Catch
                Return 1.0
            End Try
        End Get
    End Property

    Private Sub Apply(zoom As Double, visibleRect As Rect, updateExistingScale As Boolean)
        If Not CachingApplies(zoom) Then
            CancelRescale()
            If _cachesActive Then ClearCaches()
            Return
        End If
        If visibleRect.IsEmpty OrElse visibleRect.Width <= 0 OrElse visibleRect.Height <= 0 Then Return
        EnsureContainers()
        If _containers Is Nothing Then Return

        Dim dpi As Double = DeviceScale
        Dim dpiArea As Double = dpi * dpi

        Dim padX As Double = visibleRect.Width * ViewportPadFraction
        Dim padY As Double = visibleRect.Height * ViewportPadFraction
        Dim padded As New Rect(visibleRect.X - padX, visibleRect.Y - padY, visibleRect.Width + padX * 2, visibleRect.Height + padY * 2)

        _visibleSet.Clear()
        _visible.Clear()

        Dim areaSum As Double = 0
        Dim weightSum As Integer = 0
        For Each container In _containers
            Dim local As Rect
            Dim unitScale As Double = 1.0
            If Not TryContainerLocalRect(container, padded, local, unitScale) Then Continue For
            Dim unitArea As Double = unitScale * unitScale * dpiArea
            For Each candidate In container.Children
                If candidate.HasSlot AndAlso local.IntersectsWith(candidate.Slot) Then
                    _visibleSet.Add(candidate.Element)
                    _visible.Add(New VisibleEntry With {.Element = candidate.Element, .UnitScale = unitScale, .Width = candidate.Slot.Width, .Height = candidate.Slot.Height, .Weight = candidate.Weight})
                    areaSum += candidate.Slot.Width * candidate.Slot.Height * unitArea
                    weightSum += candidate.Weight
                End If
            Next
        Next

        'Hysteresis: need a higher bar to switch on than to stay on so panning doesn't cook the raster toggle at the edges.
        Dim required As Integer = If(_cachesActive, CInt(MinCacheWeight * CacheWeightHysteresis), MinCacheWeight)
        Dim engage As Boolean = weightSum >= required

        If Not engage Then
            CancelRescale()
            If _cachesActive Then ClearCaches()
            Return
        End If

        Dim scale As Double = zoom * dpi * CacheSuperSample
        If areaSum > 0 Then
            Dim budgetScale As Double = Math.Sqrt(TotalCacheBudgetPixels / areaSum)
            If budgetScale < scale Then scale = budgetScale
        End If
        scale = Math.Max(DrawableWrapperFactory.MinCacheScale, scale)

        'Never re-derive the scale while panning: it changes every frame, so each one would invalidate every raster.
        If updateExistingScale Then CancelRescale()

        For Each entry In _visible
            Dim elementScale As Double = DrawableWrapperFactory.CacheScaleFor(scale * entry.UnitScale, entry.Width, entry.Height)
            If Not updateExistingScale Then
                If TryCast(entry.Element.CacheMode, BitmapCache) Is Nothing Then
                    ApplyCacheState(entry.Element, True, elementScale, False)
                End If
            Else
                Dim cache = TryCast(entry.Element.CacheMode, BitmapCache)
                If cache Is Nothing Then
                    ApplyCacheState(entry.Element, True, elementScale, False)
                ElseIf cache.RenderAtScale <> elementScale Then
                    'Zooming out goes to the FRONT of the queue so oversized bitmaps are released before the drain spend time sharpening everything else. Zooming in waits until its turn
                    If elementScale < cache.RenderAtScale Then
                        _rescaleQueue.Insert(_rescaleIndex, New RescaleEntry With {.Element = entry.Element, .Scale = elementScale, .Weight = entry.Weight})
                    Else
                        _rescaleQueue.Add(New RescaleEntry With {.Element = entry.Element, .Scale = elementScale, .Weight = entry.Weight})
                    End If
                End If
            End If
        Next

        If updateExistingScale AndAlso _rescaleQueue.Count > 0 Then StartRescaleDrain()

        For Each container In _containers
            For Each candidate In container.Children
                Dim element = candidate.Element
                If Not _visibleSet.Contains(element) AndAlso element.CacheMode IsNot Nothing Then
                    ApplyCacheState(element, False, 1.0, False)
                End If
            Next
        Next

        _cachesActive = _visible.Count > 0
    End Sub

    Private Sub StartRescaleDrain()
        If _rescaleTimer Is Nothing Then
            _rescaleTimer = New Threading.DispatcherTimer(Threading.DispatcherPriority.Background) With {
                .Interval = TimeSpan.FromMilliseconds(1)
            }
            AddHandler _rescaleTimer.Tick, AddressOf OnRescaleTick
        End If
        If Not _rescaleTimer.IsEnabled Then _rescaleTimer.Start()
    End Sub

    Private Sub OnRescaleTick(sender As Object, e As EventArgs)

        Dim weightBudget As Integer = RescaleWeightPerTick
        Dim countBudget As Integer = CacheRescaleBatchSize
        While _rescaleIndex < _rescaleQueue.Count AndAlso weightBudget > 0 AndAlso countBudget > 0
            Dim entry = _rescaleQueue(_rescaleIndex)
            _rescaleIndex += 1
            weightBudget -= Math.Max(1, entry.Weight)
            countBudget -= 1
            Dim cache = TryCast(entry.Element.CacheMode, BitmapCache)
            If cache IsNot Nothing AndAlso cache.RenderAtScale <> entry.Scale Then
                cache.RenderAtScale = entry.Scale
            End If
        End While
        If _rescaleIndex >= _rescaleQueue.Count Then CancelRescale()
    End Sub

    Private Function TryContainerLocalRect(container As CullContainer, padded As Rect, ByRef local As Rect, ByRef unitScale As Double) As Boolean
        unitScale = 1.0
        Try
            Dim inverse = container.Container.TransformToAncestor(_canvas).Inverse
            If inverse Is Nothing Then Return False
            local = inverse.TransformBounds(padded)
            If local.Width > 0 AndAlso Not Double.IsNaN(local.Width) Then unitScale = padded.Width / local.Width
            Return True
        Catch
            Return False
        End Try
    End Function

    Private Sub ClearCaches()
        _cachesActive = False
        EnsureContainers()
        If _containers Is Nothing Then Return
        For Each container In _containers
            For Each candidate In container.Children
                If candidate.Element.CacheMode IsNot Nothing Then candidate.Element.CacheMode = Nothing
            Next
        Next
    End Sub

    Private Sub EnsureContainers()
        If _containers IsNot Nothing AndAlso Not _containersDirty Then Return
        Dim list As New List(Of CullContainer)
        CollectContainers(_canvas, list)
        _containers = list
        _containersDirty = False

        Dim total As Integer = 0
        Dim count As Integer = 0
        For Each container In list
            For Each candidate In container.Children
                total += candidate.Weight
                count += 1
            Next
        Next

    End Sub

    Private Function CollectContainers(node As DependencyObject, into As List(Of CullContainer)) As Boolean
        Dim eligible As New List(Of CacheCandidate)
        Dim subtreeCached As Boolean = False
        Dim count As Integer = VisualTreeHelper.GetChildrenCount(node)
        For i As Integer = 0 To count - 1
            Dim child As DependencyObject = VisualTreeHelper.GetChild(node, i)
            Dim element = TryCast(child, FrameworkElement)
            Dim childEligible As Boolean = element IsNot Nothing AndAlso GetCacheEligible(element)
            Dim descendantCached As Boolean = CollectContainers(child, into)
            'Only cache a wrapper with no eligible descendants: caching the container too nests a raster over every leaf.
            If descendantCached Then
                subtreeCached = True
            ElseIf childEligible Then
                Dim slot = ElementSlotRect(element)
                eligible.Add(New CacheCandidate With {.Element = element, .Weight = WeightOf(element), .Slot = If(slot.HasValue, slot.Value, Rect.Empty), .HasSlot = slot.HasValue})
                subtreeCached = True
            End If
        Next
        If eligible.Count > 0 Then
            Dim container = TryCast(node, Canvas)
            If container IsNot Nothing Then into.Add(New CullContainer(container, eligible))
        End If
        Return subtreeCached
    End Function

    Private Function WeightOf(wrapper As FrameworkElement) As Integer
        Dim content = TryCast(TryCast(wrapper, ContentControl)?.Content, Shape)
        If content Is Nothing Then Return 1

        Dim polyline = TryCast(content, Polyline)
        If polyline IsNot Nothing Then Return Math.Max(1, polyline.Points.Count)

        Dim polygon = TryCast(content, Polygon)
        If polygon IsNot Nothing Then Return Math.Max(1, polygon.Points.Count)

        Dim path = TryCast(content, Path)
        If path IsNot Nothing Then
            Dim geometry = path.Data
            If geometry IsNot Nothing Then

                Dim weight As Integer
                If _weightMemo.TryGetValue(geometry, weight) Then Return weight

                Dim pathGeometry = TryCast(geometry, PathGeometry)
                If pathGeometry IsNot Nothing Then
                    weight = GeometryWeight(pathGeometry)
                    _weightMemo(geometry) = weight
                    Return weight
                End If
            End If
        End If

        Return 1
    End Function

    'Counts POINTS, not pathsegment objects. Imported geometry is flattened, so each path is one PathFigure holding a single poyllinesegment carrying every point:
    'counting segments gave every path a weight of 2 no matter how complex it was so we never hit the budget
    Private Shared Function GeometryWeight(geometry As PathGeometry) As Integer
        Dim total As Integer = 0
        For Each figure In geometry.Figures
            total += 1
            For Each segment As PathSegment In figure.Segments
                Dim polyLine = TryCast(segment, PolyLineSegment)
                If polyLine IsNot Nothing Then
                    total += polyLine.Points.Count
                    Continue For
                End If
                Dim polyBezier = TryCast(segment, PolyBezierSegment)
                If polyBezier IsNot Nothing Then
                    total += polyBezier.Points.Count
                    Continue For
                End If
                Dim polyQuadratic = TryCast(segment, PolyQuadraticBezierSegment)
                If polyQuadratic IsNot Nothing Then
                    total += polyQuadratic.Points.Count
                    Continue For
                End If
                total += 1
            Next
        Next
        Return Math.Max(1, total)
    End Function

    Private Shared Function ElementSlotRect(element As FrameworkElement) As Rect?
        Dim w As Double = element.ActualWidth
        Dim h As Double = element.ActualHeight
        If Double.IsNaN(w) OrElse Double.IsNaN(h) OrElse w <= 0 OrElse h <= 0 Then Return Nothing
        Dim left As Double = Canvas.GetLeft(element)
        Dim top As Double = Canvas.GetTop(element)
        If Double.IsNaN(left) Then left = 0
        If Double.IsNaN(top) Then top = 0
        Return New Rect(left, top, w, h)
    End Function

    Private Shared Sub ApplyCacheState(element As FrameworkElement, wantCache As Boolean, elementScale As Double, updateExistingScale As Boolean)
        Dim cache = TryCast(element.CacheMode, BitmapCache)
        If Not wantCache Then
            If cache IsNot Nothing Then element.CacheMode = Nothing
            Return
        End If
        Dim isNew As Boolean = cache Is Nothing
        If isNew Then
            cache = New BitmapCache() With {.EnableClearType = False, .SnapsToDevicePixels = False}
            element.CacheMode = cache
        End If
        If isNew OrElse updateExistingScale Then
            If cache.RenderAtScale <> elementScale Then cache.RenderAtScale = elementScale
        End If
    End Sub
End Class
