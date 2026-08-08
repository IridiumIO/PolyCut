Public Class BitmapToSVGWindow

    Private _flattenedShapes As List(Of (Geometry As Geometry, Source As GeometryDrawing))
    Private _drawingBounds As Rect

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

    Private Sub UpdateHitTestGeometry()
        Dim vm = TryCast(DataContext, BitmapToSVGWindowViewModel)
        Dim drawing = vm?.PreviewDrawing
        If drawing Is Nothing Then Return

        _flattenedShapes = SvgHitTestHelper.FlattenDrawing(drawing).ToList()

        Dim canvasSize = vm.PreviewCanvasSize
        HitTestCanvas.Width = canvasSize.Width
        HitTestCanvas.Height = canvasSize.Height

        HighlightPath.Data = Nothing
        HighlightPath.Visibility = Visibility.Collapsed
    End Sub


    Private Sub UpdateHighlightPath(point As Point)
        If _flattenedShapes Is Nothing OrElse _flattenedShapes.Count = 0 Then Return
        For i = _flattenedShapes.Count - 1 To 0 Step -1
            Dim shape = _flattenedShapes(i)
            If shape.Geometry.FillContains(point) Then
                HighlightPath.Data = shape.Geometry
                Return
            End If
        Next
        HighlightPath.Data = Nothing
    End Sub

    Private Sub HitTestCanvas_MouseMove(sender As Object, e As MouseEventArgs)
        If Not Keyboard.IsKeyDown(Key.LeftCtrl) OrElse _flattenedShapes Is Nothing OrElse _flattenedShapes.Count = 0 Then Return
        Dim point = e.GetPosition(HitTestCanvas)
        UpdateHighlightPath(point)
    End Sub

    Private Sub HitTestCanvas_MouseLeave(sender As Object, e As MouseEventArgs)
        HighlightPath.Data = Nothing
    End Sub










    Private Sub OnRequestClose(result As Boolean)

        DialogResult = result
        Close()

    End Sub

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