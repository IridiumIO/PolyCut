Public Class BitmapToSVGWindow

    Private _flattenedShapes As List(Of (Geometry As Geometry, Source As GeometryDrawing))
    Private _drawingBounds As Rect

    Private _excludedIndices As New HashSet(Of Integer)
    Private _visibleGeometryCache As New Dictionary(Of Integer, Geometry)

    Public Sub New(vm As BitmapToSVGWindowViewModel)
        InitializeComponent()
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

    Private Function GetVisibleGeometry(index As Integer) As Geometry
        Dim cached As Geometry = Nothing
        If _visibleGeometryCache.TryGetValue(index, cached) Then Return cached

        Dim result As Geometry = _flattenedShapes(index).Geometry

        ' Subtract every shape that sits above this one in paint order
        For i = index + 1 To _flattenedShapes.Count - 1
            Dim above = _flattenedShapes(i).Geometry
            ' Skip empty geometry to avoid destroying performanc
            If above.Bounds.IsEmpty Then Continue For
            result = Geometry.Combine(result, above, GeometryCombineMode.Exclude, Nothing)
            If result.IsEmpty() Then Exit For
        Next

        _visibleGeometryCache(index) = result
        Return result
    End Function
    Private Shared Function GetColorKey(gd As GeometryDrawing) As Color?
        Dim scb = TryCast(gd.Brush, SolidColorBrush)
        If scb Is Nothing Then Return Nothing
        Return scb.Color
    End Function

    Private Function GetIndicesMatchingColor(referenceIndex As Integer) As IEnumerable(Of Integer)
        Dim refColor = GetColorKey(_flattenedShapes(referenceIndex).Source)
        If refColor Is Nothing Then Return {referenceIndex} ' can't match by colour; fall back to single
        Return Enumerable.Range(0, _flattenedShapes.Count) _
                         .Where(Function(i) GetColorKey(_flattenedShapes(i).Source)?.Equals(refColor.Value))
    End Function


    '####################
    ' HIT TESTING
    '####################

    Private Sub UpdateHitTestGeometry()
        Dim vm = TryCast(DataContext, BitmapToSVGWindowViewModel)
        Dim drawing = vm?.PreviewDrawing
        If drawing Is Nothing Then Return

        _flattenedShapes = SvgHitTestHelper.FlattenDrawing(drawing).ToList()
        _excludedIndices.Clear()
        _visibleGeometryCache.Clear()

        Dim canvasSize = vm.PreviewCanvasSize
        HitTestCanvas.Width = canvasSize.Width
        HitTestCanvas.Height = canvasSize.Height

        HighlightPath.Data = Nothing
        HighlightPath.Visibility = Visibility.Collapsed
        ExclusionPath.Data = Nothing

        ' Sync cleared exclusions back to VM
        vm.ExcludedRegionIndices = _excludedIndices
    End Sub




    Private Function HitTestIndex(point As Point) As Integer
        If _flattenedShapes Is Nothing OrElse _flattenedShapes.Count = 0 Then Return -1
        For i = _flattenedShapes.Count - 1 To 0 Step -1
            If _flattenedShapes(i).Geometry.FillContains(point) Then Return i
        Next
        Return -1
    End Function

    Private Sub UpdateHighlightPath(point As Point)
        Dim idx = HitTestIndex(point)
        If idx < 0 Then
            HighlightPath.Data = Nothing
            Return
        End If

        ' Ctrl+Shift: preview ALL shapes of the same colour
        If Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift) Then
            Dim group As New GeometryGroup()
            group.FillRule = FillRule.Nonzero
            For Each i In GetIndicesMatchingColor(idx)
                group.Children.Add(GetVisibleGeometry(i))
            Next
            HighlightPath.Data = group
        Else
            HighlightPath.Data = GetVisibleGeometry(idx)
        End If
    End Sub

    Private Sub RefreshExclusionOverlay()
        If _excludedIndices.Count = 0 Then
            ExclusionPath.Data = Nothing
            Return
        End If

        Dim group As New GeometryGroup()
        group.FillRule = FillRule.Nonzero
        For Each idx In _excludedIndices
            Dim vis = GetVisibleGeometry(idx)
            If Not vis.IsEmpty() Then group.Children.Add(vis)
        Next
        ExclusionPath.Data = group
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

            ' If every matching shape is already excluded -> un-exclude all; otherwise exclude all
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
            Dim point = Mouse.GetPosition(HitTestCanvas)
            UpdateHighlightPath(point)
        End If
    End Sub

    Private Sub FluentWindow_PreviewKeyUp(sender As Object, e As KeyEventArgs)
        If e.SystemKey = Key.LeftAlt OrElse e.SystemKey = Key.RightAlt Then
            OriginalImageOverlay.Visibility = Visibility.Collapsed
            PreviewDrawingHost.Visibility = Visibility.Visible
        End If

        If e.Key = Key.LeftCtrl Then
            HighlightPath.Visibility = Visibility.Collapsed
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