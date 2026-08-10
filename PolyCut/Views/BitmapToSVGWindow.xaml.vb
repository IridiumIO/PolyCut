Public Class BitmapToSVGWindow

    Private _flattenedShapes As List(Of (Geometry As Geometry, Source As GeometryDrawing))
    Private _drawingBounds As Rect

    Private _colorGroups As Dictionary(Of Color, List(Of Integer))

    Private _excludedIndices As New HashSet(Of Integer)
    Private _occlusionCache As New Dictionary(Of (Integer, String), Drawing)

    ' Hit test cache
    Private _lastHitIndex As Integer = -1
    Private _lastHitBounds As Rect = Rect.Empty

    Private _vm As BitmapToSVGWindowViewModel

    Public Sub New(vm As BitmapToSVGWindowViewModel)
        InitializeComponent()
        _vm = vm
        DataContext = vm
        AddHandler vm.RequestClose, AddressOf OnRequestClose
    End Sub

    Private Sub BitmapToSVGWindow_Loaded(sender As Object, e As RoutedEventArgs)
        AddHandler DirectCast(DataContext, ComponentModel.INotifyPropertyChanged).PropertyChanged, AddressOf ViewModel_PropertyChanged
    End Sub

    Private Sub ViewModel_PropertyChanged(sender As Object, e As ComponentModel.PropertyChangedEventArgs)
        If e.PropertyName = NameOf(BitmapToSVGWindowViewModel.PreviewDrawing) Then
            UpdateHitTestGeometry()
        End If
    End Sub



    '####################
    ' VISIBLE GEOMETRY MANAGEMENT
    '####################

    Private Function BuildOcclusionDrawing(aboveIndex As Integer, skipIndices As ICollection(Of Integer), Optional isExclusion As Boolean = False) As Drawing
        Dim cacheKey = (aboveIndex, String.Join("|"c, skipIndices.OrderBy(Function(i) i)))

        Dim cached As Drawing = Nothing
        If Not isExclusion AndAlso _occlusionCache.TryGetValue(cacheKey, cached) Then Return cached

        Dim group As New DrawingGroup()
        For i = aboveIndex + 1 To _flattenedShapes.Count - 1
            If Not skipIndices.Contains(i) Then
                Dim shape = _flattenedShapes(i)
                Dim br As Brush = shape.Source.Brush.Clone()
                If Not isExclusion Then br.Opacity = 0.5

                group.Children.Add(New GeometryDrawing(br, shape.Source.Pen, shape.Geometry))
            End If
        Next

        Dim result As Drawing = If(group.Children.Count > 0, group, Nothing)
        If Not isExclusion Then _occlusionCache(cacheKey) = result
        Return result
    End Function



    '####################
    ' COLOUR MATCHING
    '####################

    Private Shared Function GetColorKey(gd As GeometryDrawing) As Color?
        Dim scb = TryCast(gd.Brush, SolidColorBrush)
        If scb Is Nothing Then Return Nothing
        Return scb.Color
    End Function

    Private Function GetIndicesMatchingColor(referenceIndex As Integer) As IEnumerable(Of Integer)
        Dim refColor = GetColorKey(_flattenedShapes(referenceIndex).Source)
        If Not refColor.HasValue Then Return {referenceIndex}

        Dim group As List(Of Integer) = Nothing
        If _colorGroups.TryGetValue(refColor.Value, group) Then Return group

        Return {referenceIndex}
    End Function



    '####################
    ' HIT TESTING
    '####################

    Private Sub UpdateHitTestGeometry()
        Dim drawing = _vm?.PreviewDrawing
        If drawing Is Nothing Then Return

        _flattenedShapes = SvgHitTestHelper.FlattenDrawing(drawing).ToList()
        _excludedIndices.Clear()
        _occlusionCache.Clear()
        _lastHitIndex = -1
        _lastHitBounds = Rect.Empty

        _colorGroups = New Dictionary(Of Color, List(Of Integer))
        For i = 0 To _flattenedShapes.Count - 1
            Dim color = GetColorKey(_flattenedShapes(i).Source)
            If color.HasValue Then
                Dim group As List(Of Integer) = Nothing
                If Not _colorGroups.TryGetValue(color.Value, group) Then
                    group = New List(Of Integer)()
                    _colorGroups(color.Value) = group
                End If
                group.Add(i)
            End If
        Next

        Dim canvasSize = _vm.PreviewCanvasSize
        HitTestCanvas.Width = canvasSize.Width
        HitTestCanvas.Height = canvasSize.Height

        HighlightPath.Data = Nothing
        HighlightPath.Visibility = Visibility.Collapsed
        ExclusionPath.Data = Nothing
        OcclusionDrawingHost.Drawing = Nothing

        _vm.ExcludedRegionIndices = _excludedIndices
    End Sub




    Private Function HitTestIndex(point As Point) As Integer
        If _flattenedShapes Is Nothing OrElse _flattenedShapes.Count = 0 Then Return -1

        ' Fast path: check if still within the previously hit shape, but only if we are not in Stacked mode
        If _lastHitIndex >= 0 AndAlso _vm.VTracerOptions.Hierarchical = HeiarchicalMethod.Cutout Then
            If _lastHitBounds.Contains(point) Then
                If _flattenedShapes(_lastHitIndex).Geometry.FillContains(point, 0.001, ToleranceType.Absolute) Then
                    Return _lastHitIndex    ' Mouse hasn't left the shape — skip full scan
                End If
            End If
        End If

        ' Full scan (top-to-bottom z-order)
        For i = _flattenedShapes.Count - 1 To 0 Step -1
            If _flattenedShapes(i).Geometry.FillContains(point, 0.001, ToleranceType.Absolute) Then
                _lastHitIndex = i
                _lastHitBounds = _flattenedShapes(i).Geometry.Bounds
                Return i
            End If
        Next

        ' Mouse is over empty space — clear cache
        _lastHitIndex = -1
        _lastHitBounds = Rect.Empty
        Return -1
    End Function

    Private Sub UpdateHighlightPath(point As Point)
        Dim idx = HitTestIndex(point)
        If idx < 0 Then
            HighlightPath.Data = Nothing
            OcclusionDrawingHost.Drawing = Nothing
            Return
        End If

        If Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift) Then
            ' Ctrl+Shift: preview all shapes of the same colour
            Dim matchedSet = New HashSet(Of Integer)(GetIndicesMatchingColor(idx))
            Dim minIdx = matchedSet.Min()

            Dim geomGroup As New GeometryGroup()
            geomGroup.FillRule = FillRule.Nonzero
            For Each i In matchedSet
                geomGroup.Children.Add(_flattenedShapes(i).Geometry)
            Next
            HighlightPath.Data = geomGroup
            OcclusionDrawingHost.Drawing = BuildOcclusionDrawing(matchedSet.Min(), matchedSet)
        Else
            ' Ctrl only: single shape 
            HighlightPath.Data = _flattenedShapes(idx).Geometry
            OcclusionDrawingHost.Drawing = BuildOcclusionDrawing(idx, {idx})
        End If
    End Sub

    Private Sub RefreshExclusionOverlay()
        If _excludedIndices.Count = 0 Then
            ExclusionPath.Data = Nothing
            ExclusionOcclusionDrawingHost.Drawing = Nothing
            Return
        End If

        ' 1. Build ExclusionPath from all excluded raw geometries (full, unclipped)
        Dim geomGroup As New GeometryGroup()
        geomGroup.FillRule = FillRule.Nonzero
        For Each idx In _excludedIndices
            geomGroup.Children.Add(_flattenedShapes(idx).Geometry)
        Next
        ExclusionPath.Data = geomGroup

        ExclusionOcclusionDrawingHost.Drawing = BuildOcclusionDrawing(_excludedIndices.Min(), _excludedIndices, True)
    End Sub


    '####################
    ' MOUSE EVENTS
    '####################

    Private Sub HitTestCanvas_MouseMove(sender As Object, e As MouseEventArgs)
        If Not Keyboard.IsKeyDown(Key.LeftCtrl) OrElse _flattenedShapes Is Nothing OrElse _flattenedShapes.Count = 0 Then Return
        Dim point = e.GetPosition(HitTestCanvas)
        UpdateHighlightPath(point)
    End Sub

    Private Sub HitTestCanvas_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        If Not Keyboard.IsKeyDown(Key.LeftCtrl) Then Return
        If _flattenedShapes Is Nothing OrElse _flattenedShapes.Count = 0 Then Return

        Dim point = e.GetPosition(HitTestCanvas)
        Dim idx = HitTestIndex(point)
        If idx < 0 Then Return

        Dim shiftHeld = Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift)

        If shiftHeld Then
            ' Ctrl+Shift+Click: toggle ALL shapes sharing the same fill colour
            Dim matchingIndices = GetIndicesMatchingColor(idx).ToList()
            Dim allAlreadyExcluded = matchingIndices.All(Function(i) _excludedIndices.Contains(i))
            If allAlreadyExcluded Then
                For Each i In matchingIndices
                    _excludedIndices.Remove(i)
                Next
            Else
                For Each i In matchingIndices
                    _excludedIndices.Add(i)
                Next
            End If
        Else
            ' Ctrl+Click: toggle just the hovered shape
            If _excludedIndices.Contains(idx) Then
                _excludedIndices.Remove(idx)
            Else
                _excludedIndices.Add(idx)
            End If
        End If

        Dim vm = TryCast(DataContext, BitmapToSVGWindowViewModel)
        If vm IsNot Nothing Then vm.ExcludedRegionIndices = New HashSet(Of Integer)(_excludedIndices)

        RefreshExclusionOverlay()
        UpdateHighlightPath(point)
        e.Handled = True
    End Sub

    Private Sub HitTestCanvas_MouseLeave(sender As Object, e As MouseEventArgs)
        HighlightPath.Data = Nothing
        OcclusionDrawingHost.Drawing = Nothing
    End Sub


    '####################
    ' KEYBOARD EVENTS
    '####################

    Private Sub FluentWindow_PreviewKeyDown(sender As Object, e As KeyEventArgs)
        If e.SystemKey = Key.LeftAlt OrElse e.SystemKey = Key.RightAlt Then
            OriginalImageOverlay.Visibility = Visibility.Visible
            PreviewDrawingHost.Visibility = Visibility.Collapsed
        End If

        If e.Key = Key.LeftCtrl Then
            HighlightPath.Visibility = Visibility.Visible
            UpdateHighlightPath(Mouse.GetPosition(HitTestCanvas))
        End If

        ' Shift pressed while Ctrl already held — refresh
        If (e.Key = Key.LeftShift OrElse e.Key = Key.RightShift) AndAlso Keyboard.IsKeyDown(Key.LeftCtrl) Then
            UpdateHighlightPath(Mouse.GetPosition(HitTestCanvas))
        End If

    End Sub

    Private Sub FluentWindow_PreviewKeyUp(sender As Object, e As KeyEventArgs)
        If e.SystemKey = Key.LeftAlt OrElse e.SystemKey = Key.RightAlt Then
            OriginalImageOverlay.Visibility = Visibility.Collapsed
            PreviewDrawingHost.Visibility = Visibility.Visible
        End If

        If e.Key = Key.LeftCtrl Then
            HighlightPath.Visibility = Visibility.Collapsed
            OcclusionDrawingHost.Drawing = Nothing
        End If

        ' Shift released while Ctrl still held — refresh
        If (e.Key = Key.LeftShift OrElse e.Key = Key.RightShift) AndAlso Keyboard.IsKeyDown(Key.LeftCtrl) Then
            UpdateHighlightPath(Mouse.GetPosition(HitTestCanvas))
        End If

    End Sub

    Private Sub OnRequestClose(result As Boolean)
        DialogResult = result
        Close()
    End Sub

End Class


Public Class DrawingHost
    Inherits FrameworkElement

    Public Shared ReadOnly DrawingProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(Drawing), GetType(Drawing), GetType(DrawingHost),
            New FrameworkPropertyMetadata(Nothing, FrameworkPropertyMetadataOptions.AffectsRender))

    Public Property Drawing As Drawing
        Get
            Return CType(GetValue(DrawingProperty), Drawing)
        End Get
        Set(value As Drawing)
            SetValue(DrawingProperty, value)
        End Set
    End Property

    Protected Overrides Sub OnRender(drawingContext As DrawingContext)
        MyBase.OnRender(drawingContext)
        If Drawing IsNot Nothing Then
            drawingContext.DrawDrawing(Drawing)
        End If
    End Sub

End Class