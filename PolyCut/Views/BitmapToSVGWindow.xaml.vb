Public Class BitmapToSVGWindow

    Private _flattenedShapes As List(Of (Geometry As Geometry, Source As GeometryDrawing))
    Private _drawingBounds As Rect

    Private _colorGroups As Dictionary(Of Color, List(Of Integer))

    Private _excludedIndices As New HashSet(Of Integer)
    Private _occlusionCache As New Dictionary(Of (Integer, String), Drawing)
    Private _fullPreviewDrawing As Drawing

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

        _fullPreviewDrawing = drawing

        _flattenedShapes = SvgHitTestHelper.FlattenDrawing(drawing).ToList()
        _excludedIndices.Clear()
        _occlusionCache.Clear()
        _exclusionOverlayCache.Clear()
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

        PreviewDrawingHost.Drawing = _fullPreviewDrawing
        ExclusionPath.Visibility = Visibility.Collapsed
        ExclusionOcclusionDrawingHost.Drawing = Nothing
        HighlightPath.Data = Nothing
        HighlightPath.Visibility = Visibility.Collapsed
        OcclusionDrawingHost.Drawing = Nothing

        _vm.ExcludedRegionIndices = _excludedIndices
    End Sub


    Private Sub RebuildPreviewDrawing()
        If _fullPreviewDrawing Is Nothing Then Return

        If _excludedIndices.Count = 0 Then
            PreviewDrawingHost.Drawing = _fullPreviewDrawing
            Return
        End If

        Dim group As New DrawingGroup()
        For i = 0 To _flattenedShapes.Count - 1
            If Not _excludedIndices.Contains(i) Then
                Dim shape = _flattenedShapes(i)
                group.Children.Add(New GeometryDrawing(shape.Source.Brush, shape.Source.Pen, shape.Geometry))
            End If
        Next
        PreviewDrawingHost.Drawing = group
    End Sub


    Private Function HitTestIndex(point As Point) As Integer
        If _flattenedShapes Is Nothing OrElse _flattenedShapes.Count = 0 Then Return -1

        ' Fast path: check if still within the previously hit shape, but only if we are not in Stacked mode
        If _lastHitIndex >= 0 AndAlso _vm.VTracerOptions.Hierarchical <> HeiarchicalMethod.Stacked Then
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
        If idx < 0 OrElse _excludedIndices.Contains(idx) Then
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

    Private _exclusionOverlayCache As New Dictionary(Of String, Drawing)   ' add field

    Private Sub RefreshExclusionOverlay()
        If _excludedIndices.Count = 0 Then
            ExclusionPath.Visibility = Visibility.Collapsed
            ExclusionOcclusionDrawingHost.Drawing = Nothing
            Return
        End If

        ' Red is now rendered by the interleaved overlay; the standalone path is unused
        ExclusionPath.Data = Nothing
        ExclusionPath.Visibility = Visibility.Collapsed

        Dim key = String.Join("|"c, _excludedIndices.OrderBy(Function(i) i))
        Dim cached As Drawing = Nothing
        If _exclusionOverlayCache.TryGetValue(key, cached) Then
            ExclusionOcclusionDrawingHost.Drawing = cached
            Return
        End If

        Dim hatch = TryCast(FindResource("ExclusionHatchBrush"), Brush)
        Dim redPen = New Pen(New SolidColorBrush(Color.FromArgb(&HFF, &HFF, &H44, &H44)), 0.4)
        redPen.LineJoin = PenLineJoin.Round

        ' Walk shapes in paint order (bottom -> top) and interleave:
        '   excluded shape  -> emit its red hatch HERE, at its true z-position
        '   non-excluded    -> emit the shape itself, which correctly occludes any red below it
        Dim group As New DrawingGroup()
        For i = 0 To _flattenedShapes.Count - 1
            If _excludedIndices.Contains(i) Then
                group.Children.Add(New GeometryDrawing(hatch, redPen, _flattenedShapes(i).Geometry))
            Else
                Dim shape = _flattenedShapes(i)
                group.Children.Add(New GeometryDrawing(shape.Source.Brush, shape.Source.Pen, shape.Geometry))
            End If
        Next

        _exclusionOverlayCache(key) = group
        ExclusionOcclusionDrawingHost.Drawing = group
    End Sub


    '####################
    ' MOUSE EVENTS
    '####################


    Private Const MouseMoveThrottleInterval As Integer = 100
    Private _lastMouseMove As DateTime
    Private _pendingMouseMove As Boolean

    Private Async Sub HitTestCanvas_MouseMove(sender As Object, e As MouseEventArgs)
        If Not Keyboard.IsKeyDown(Key.LeftCtrl) OrElse _flattenedShapes Is Nothing OrElse _flattenedShapes.Count = 0 Then Return

        Dim point = e.GetPosition(HitTestCanvas)
        Dim elapsed = (DateTime.UtcNow - _lastMouseMove).TotalMilliseconds

        If elapsed >= MouseMoveThrottleInterval Then
            _lastMouseMove = DateTime.UtcNow
            UpdateHighlightPath(point)
        ElseIf Not _pendingMouseMove Then
            _pendingMouseMove = True
            Await Task.Delay(CInt(MouseMoveThrottleInterval - elapsed))
            _pendingMouseMove = False
            _lastMouseMove = DateTime.UtcNow
            UpdateHighlightPath(e.GetPosition(HitTestCanvas))
        End If
    End Sub

    Private Sub HitTestCanvas_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)

        Dim shiftHeld = Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift)
        If Not Keyboard.IsKeyDown(Key.LeftCtrl) Then Return

        If _flattenedShapes Is Nothing OrElse _flattenedShapes.Count = 0 Then Return

        Dim point = e.GetPosition(HitTestCanvas)
        Dim idx = HitTestIndex(point)
        If idx < 0 Then Return


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

        PreviewDrawingHost.Drawing = _fullPreviewDrawing

        If _excludedIndices.Count > 0 Then
            ExclusionPath.Visibility = Visibility.Visible
            RefreshExclusionOverlay()
        Else
            ExclusionPath.Visibility = Visibility.Collapsed
            ExclusionOcclusionDrawingHost.Drawing = Nothing
        End If

        HighlightPath.Visibility = Visibility.Visible
        HighlightPath.Data = Nothing
        OcclusionDrawingHost.Drawing = Nothing

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
        If e.IsRepeat Then Return

        If e.SystemKey = Key.LeftAlt OrElse e.SystemKey = Key.RightAlt Then
            OriginalImageOverlay.Visibility = Visibility.Visible
            PreviewDrawingHost.Visibility = Visibility.Collapsed
        End If

        If e.Key = Key.LeftCtrl Then
            ' While Ctrl is held, bring excluded shapes back and show the red exclusion overlay
            PreviewDrawingHost.Drawing = _fullPreviewDrawing
            If _excludedIndices.Count > 0 Then
                ExclusionPath.Visibility = Visibility.Visible
                RefreshExclusionOverlay()
            End If
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
            ' Excluded shapes vanish from the preview until Ctrl is pressed again
            RebuildPreviewDrawing()
            ExclusionPath.Visibility = Visibility.Collapsed
            ExclusionOcclusionDrawingHost.Drawing = Nothing
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



Public Module SvgHitTestHelper

    Public Function FlattenDrawing(drawing As Drawing) As List(Of (Geometry As Geometry, Source As GeometryDrawing))
        Dim results As New List(Of (Geometry, GeometryDrawing))
        If drawing IsNot Nothing Then Flatten(drawing, Matrix.Identity, results)
        Return results
    End Function

    Private Sub Flatten(drawing As Drawing, transform As Matrix, results As List(Of (Geometry, GeometryDrawing)))
        Select Case True
            Case TypeOf drawing Is DrawingGroup
                Dim group = CType(drawing, DrawingGroup)
                Dim childTransform = transform
                If group.Transform IsNot Nothing Then
                    childTransform = Matrix.Multiply(group.Transform.Value, transform)
                End If
                For Each child In group.Children
                    Flatten(child, childTransform, results)
                Next

            Case TypeOf drawing Is GeometryDrawing
                Dim gd = CType(drawing, GeometryDrawing)
                If gd.Geometry IsNot Nothing Then
                    Dim geom = gd.Geometry.Clone()
                    geom.Transform = New MatrixTransform(transform)
                    results.Add((geom, gd))
                End If
        End Select
    End Sub

End Module