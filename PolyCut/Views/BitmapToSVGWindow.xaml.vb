Public Class BitmapToSVGWindow

    Private _flattenedShapes As List(Of (Geometry As Geometry, Source As GeometryDrawing))


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

        _flattenedShapes = SvgHitTestHelper.FlattenDrawing(drawing).
            Where(Function(s) Not ReferenceEquals(s.Source, vm.BoundsSentinel)).
            ToList()

        Dim bounds = drawing.Bounds
        HitTestCanvas.Width = bounds.Width
        HitTestCanvas.Height = bounds.Height
        HitTestCanvas.RenderTransform = New TranslateTransform(-bounds.X, -bounds.Y)

        PreviewImageOverlay.Width = bounds.Width
        PreviewImageOverlay.Height = bounds.Height

        HighlightPath.Data = Nothing
    End Sub

    Private Sub HitTestCanvas_MouseMove(sender As Object, e As MouseEventArgs)

        If _flattenedShapes Is Nothing OrElse _flattenedShapes.Count = 0 Then Return

        Dim point = e.GetPosition(HitTestCanvas)

        ' Iterate in reverse: later-drawn shapes are visually "on top", so prioritize them.
        For i = _flattenedShapes.Count - 1 To 0 Step -1
            Dim shape = _flattenedShapes(i)
            If shape.Geometry.FillContains(point) Then
                HighlightPath.Data = shape.Geometry
                Return
            End If
        Next

        HighlightPath.Data = Nothing
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
            PreviewImageOverlay.Visibility = Visibility.Collapsed
        End If

        If e.Key = Key.LeftShift Then
            HighlightPath.Visibility = Visibility.Visible
        End If

    End Sub

    Private Sub FluentWindow_PreviewKeyUp(sender As Object, e As KeyEventArgs)
        If e.SystemKey = Key.LeftAlt OrElse e.SystemKey = Key.RightAlt Then
            OriginalImageOverlay.Visibility = Visibility.Collapsed
            PreviewImageOverlay.Visibility = Visibility.Visible
        End If

        If e.Key = Key.LeftShift Then
            HighlightPath.Visibility = Visibility.Collapsed
        End If

    End Sub
End Class
