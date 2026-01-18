
Imports PolyCut.Shared


Public Class Tab_ElementProperties
    Private _thicknessBeforeEdit As Double?
    Private _strokeBeforeEdit As Brush
    Private _fillBeforeEdit As Brush

    ' Map slider positions (0-10) to actual thickness values
    Private ReadOnly _thicknessValues As Double() = {0, 0.01, 0.1, 0.2, 0.5, 1, 2, 3, 4, 5, 10}


    Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Wire up property change notifications
        AddHandler Me.DataContextChanged, AddressOf OnDataContextChanged

    End Sub

    Private Sub OnDataContextChanged(sender As Object, e As DependencyPropertyChangedEventArgs)
        Dim vm = TryCast(Me.DataContext, SVGPageViewModel)
        If vm IsNot Nothing AndAlso vm.MainVM IsNot Nothing Then
            AddHandler vm.MainVM.PropertyChanged, AddressOf OnViewModelPropertyChanged
        End If
    End Sub

    Private Sub OnViewModelPropertyChanged(sender As Object, e As ComponentModel.PropertyChangedEventArgs)
        If e.PropertyName = "SelectedDrawable" Then
            UpdateSliderFromThickness()
        End If
    End Sub

    ' Color picker removed (no WinForms dependency)

    Private Sub FillColorPicker_PopupOpening(sender As Object, e As EventArgs)
        Dim vm = TryCast(Me.DataContext, SVGPageViewModel)
        If vm Is Nothing OrElse vm.MainVM Is Nothing Then Return
        Dim sd = vm.MainVM.SelectedDrawable
        If sd IsNot Nothing Then
            _fillBeforeEdit = sd.Fill
        End If
    End Sub

    Private Sub FillColorPicker_ColorSelected(sender As Object, e As ColorSelectedEventArgs)
        Dim vm = TryCast(Me.DataContext, SVGPageViewModel)
        If vm Is Nothing OrElse vm.MainVM Is Nothing Then Return

        If _fillBeforeEdit IsNot Nothing Then
            vm.MainVM.ApplyFill(e.SelectedBrush, _fillBeforeEdit)
            _fillBeforeEdit = Nothing
        ElseIf vm.MainVM.ApplyFillCommand IsNot Nothing AndAlso vm.MainVM.ApplyFillCommand.CanExecute(e.SelectedBrush) Then
            vm.MainVM.ApplyFillCommand.Execute(e.SelectedBrush)
        End If
    End Sub

    Private Sub StrokeColorPicker_PopupOpening(sender As Object, e As EventArgs)
        Dim vm = TryCast(Me.DataContext, SVGPageViewModel)
        If vm Is Nothing OrElse vm.MainVM Is Nothing Then Return
        Dim sd = vm.MainVM.SelectedDrawable
        If sd IsNot Nothing Then
            _strokeBeforeEdit = sd.Stroke
        End If
    End Sub

    Private Sub StrokeColorPicker_ColorSelected(sender As Object, e As ColorSelectedEventArgs)
        Dim vm = TryCast(Me.DataContext, SVGPageViewModel)
        If vm Is Nothing OrElse vm.MainVM Is Nothing Then Return

        If _strokeBeforeEdit IsNot Nothing Then
            vm.MainVM.ApplyStroke(e.SelectedBrush, _strokeBeforeEdit)
            _strokeBeforeEdit = Nothing
        ElseIf vm.MainVM.ApplyStrokeCommand IsNot Nothing AndAlso vm.MainVM.ApplyStrokeCommand.CanExecute(e.SelectedBrush) Then
            vm.MainVM.ApplyStrokeCommand.Execute(e.SelectedBrush)
        End If
    End Sub

    Private _updatingSlider As Boolean = False
    Private _currentDrawable As BaseDrawable = Nothing

    Private Sub ThicknessSlider_ValueChanged(sender As Object, e As RoutedPropertyChangedEventArgs(Of Double))
        If _updatingSlider Then Return

        Dim sliderIndex = CInt(Math.Round(ThicknessSlider.Value))
        If sliderIndex < 0 OrElse sliderIndex >= _thicknessValues.Length Then Return

        Dim actualThickness = _thicknessValues(sliderIndex)
        ThicknessSlider.Tag = actualThickness.ToString()

        Dim vm = TryCast(Me.DataContext, SVGPageViewModel)
        If vm Is Nothing OrElse vm.MainVM Is Nothing Then Return
        Dim sd = vm.MainVM.SelectedDrawable
        If sd Is Nothing Then Return

        ' Capture old value on first change
        If Not _thicknessBeforeEdit.HasValue Then
            _thicknessBeforeEdit = sd.StrokeThickness
        End If

        ' Apply new thickness immediately (live update)
        If Math.Abs(sd.StrokeThickness - actualThickness) > 0.001 Then
            sd.StrokeThickness = actualThickness
        End If
    End Sub

    Private Sub UpdateSliderFromThickness()
        Dim vm = TryCast(Me.DataContext, SVGPageViewModel)
        If vm Is Nothing OrElse vm.MainVM Is Nothing Then Return
        Dim sd = vm.MainVM.SelectedDrawable

        ' Unsubscribe from old drawable
        If _currentDrawable IsNot Nothing Then
            RemoveHandler _currentDrawable.PropertyChanged, AddressOf OnDrawablePropertyChanged
        End If

        _currentDrawable = TryCast(sd, BaseDrawable)

        ' Subscribe to new drawable
        If _currentDrawable IsNot Nothing Then
            AddHandler _currentDrawable.PropertyChanged, AddressOf OnDrawablePropertyChanged
        End If

        If sd Is Nothing Then Return

        _updatingSlider = True
        Try
            Dim currentThickness = sd.StrokeThickness
            Dim closestIndex = 0
            Dim minDiff = Double.MaxValue

            For i = 0 To _thicknessValues.Length - 1
                Dim diff = Math.Abs(_thicknessValues(i) - currentThickness)
                If diff < minDiff Then
                    minDiff = diff
                    closestIndex = i
                End If
            Next

            ThicknessSlider.Value = closestIndex
            ThicknessSlider.Tag = _thicknessValues(closestIndex).ToString()
        Finally
            _updatingSlider = False
        End Try
    End Sub

    Private Sub OnDrawablePropertyChanged(sender As Object, e As ComponentModel.PropertyChangedEventArgs)
        If e.PropertyName = "StrokeThickness" Then
            UpdateSliderFromThickness()
        End If
    End Sub

    ' When user stops interacting with slider, create undo action
    Private Sub ThicknessSlider_PreviewMouseUp(sender As Object, e As MouseButtonEventArgs) Handles ThicknessSlider.PreviewMouseUp
        CreateThicknessUndoAction()
    End Sub

    Private Sub ThicknessSlider_PreviewKeyUp(sender As Object, e As KeyEventArgs) Handles ThicknessSlider.PreviewKeyUp
        If e.Key = Key.Left OrElse e.Key = Key.Right OrElse e.Key = Key.Up OrElse e.Key = Key.Down Then
            CreateThicknessUndoAction()
        End If
    End Sub

    Private Sub CreateThicknessUndoAction()
        If Not _thicknessBeforeEdit.HasValue Then Return

        Dim vm = TryCast(Me.DataContext, SVGPageViewModel)
        If vm Is Nothing OrElse vm.MainVM Is Nothing Then Return
        Dim sd = vm.MainVM.SelectedDrawable
        If sd Is Nothing Then Return

        Dim sliderIndex = CInt(Math.Round(ThicknessSlider.Value))
        If sliderIndex < 0 OrElse sliderIndex >= _thicknessValues.Length Then Return
        Dim newThickness = _thicknessValues(sliderIndex)

        If Math.Abs(_thicknessBeforeEdit.Value - newThickness) > 0.001 Then
            vm.MainVM.ApplyStrokeThickness(newThickness, _thicknessBeforeEdit.Value)
        End If

        _thicknessBeforeEdit = Nothing
    End Sub

    Private Sub NumberBox_LostFocus(sender As Object, e As RoutedEventArgs)
        'Need to explicitly call the `Enter` keypress as pressing `Tab` doesn't commit the new number before switching focus

        Dim numberBox As WPF.Ui.Controls.NumberBox = TryCast(sender, WPF.Ui.Controls.NumberBox)
        If numberBox IsNot Nothing Then

            numberBox.RaiseEvent(New System.Windows.Input.KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(numberBox), 0, System.Windows.Input.Key.Enter) With {
            .RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent
        })


            Dim bindingExpression = numberBox.GetBindingExpression(WPF.Ui.Controls.NumberBox.ValueProperty)
            bindingExpression?.UpdateSource()
            ' Value is committed by binding; ValueChanged handler handles creating the undoable action
        End If
    End Sub

    Private _suppressThicknessHandler As Boolean = False

    Private Sub Thickness_ValueChanged(sender As Object, e As EventArgs)
        If _suppressThicknessHandler Then Return
        Try
            _suppressThicknessHandler = True
            Dim vm = TryCast(Me.DataContext, SVGPageViewModel)
            If vm Is Nothing OrElse vm.MainVM Is Nothing Then Return
            ' No guard here - record thickness changes normally
            Dim sd = vm.MainVM.SelectedDrawable
            If sd Is Nothing Then Return
            Dim numberBox = TryCast(sender, WPF.Ui.Controls.NumberBox)
            If numberBox Is Nothing Then Return
            Dim newThickness = numberBox.Value
            Dim prevMap As New Dictionary(Of IDrawable, Double)()
            Dim items = vm.MainVM.SelectedDrawables.ToList()
            If items.Count = 0 AndAlso vm.MainVM.SelectedDrawable IsNot Nothing Then
                items = New List(Of IDrawable) From {vm.MainVM.SelectedDrawable}
            End If
            For Each d In items
                prevMap(d) = d.StrokeThickness
            Next

            vm.MainVM.ApplyStrokeThickness(newThickness)
        Finally
            _suppressThicknessHandler = False
        End Try
    End Sub


    Private Sub TextBox_GotKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)

        Dim textBox As WPF.Ui.Controls.TextBox = TryCast(sender, WPF.Ui.Controls.TextBox)
        If textBox IsNot Nothing Then
            textBox.SelectAll()
            e.Handled = True
        End If
    End Sub

End Class
