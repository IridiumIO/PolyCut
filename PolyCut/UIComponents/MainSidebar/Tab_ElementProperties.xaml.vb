Imports System.ComponentModel

Public Class Tab_ElementProperties

    ' ===== Cached state for undo =====
    Private _thicknessBeforeEdit As Double?
    Private _strokeBeforeEdit As Brush
    Private _fillBeforeEdit As Brush


    Private ReadOnly _thicknessValues As Double() = {0, 0.01, 0.1, 0.2, 0.5, 1, 2, 3, 4, 5, 10}

    ' Guards
    Private _updatingSlider As Boolean
    Private _suppressThicknessHandler As Boolean

    ' Track currently-selected drawable for change notifications
    Private _currentDrawable As BaseDrawable

    Public Sub New()
        InitializeComponent()
        AddHandler DataContextChanged, AddressOf OnDataContextChanged
    End Sub


    Private ReadOnly Property VM As SVGPageViewModel
        Get
            Return CType(DataContext, SVGPageViewModel)
        End Get
    End Property

    Private ReadOnly Property MainVM As MainViewModel
        Get
            Return VM.MainVM
        End Get
    End Property


    ' ===== ViewModel wiring =====

    Private Sub OnDataContextChanged(sender As Object, e As DependencyPropertyChangedEventArgs)
        RemoveHandler MainVM.PropertyChanged, AddressOf OnViewModelPropertyChanged
        AddHandler MainVM.PropertyChanged, AddressOf OnViewModelPropertyChanged
        UpdateSliderFromThickness()
    End Sub

    Private Sub OnViewModelPropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        If e.PropertyName = NameOf(MainVM.SelectedDrawable) Then
            UpdateSliderFromThickness()
        End If
    End Sub


    ' ===== Fill =====

    Private Sub FillColorPicker_PopupOpening(sender As Object, e As EventArgs)
        _fillBeforeEdit = MainVM.SelectedDrawable?.Fill
    End Sub

    Private Sub FillColorPicker_ColorSelected(sender As Object, e As ColorSelectedEventArgs)
        VM.ApplyFill(e.SelectedBrush, _fillBeforeEdit)
        _fillBeforeEdit = Nothing
    End Sub


    ' ===== Stroke =====

    Private Sub StrokeColorPicker_PopupOpening(sender As Object, e As EventArgs)
        _strokeBeforeEdit = MainVM.SelectedDrawable?.Stroke
    End Sub

    Private Sub StrokeColorPicker_ColorSelected(sender As Object, e As ColorSelectedEventArgs)
        VM.ApplyStroke(e.SelectedBrush, _strokeBeforeEdit)
        _strokeBeforeEdit = Nothing
    End Sub


    ' ===== Thickness =====

    Private Sub ThicknessSlider_ValueChanged(sender As Object, e As RoutedPropertyChangedEventArgs(Of Double)
    )
        If _updatingSlider Then Return

        Dim index = CInt(Math.Round(ThicknessSlider.Value))
        If index < 0 OrElse index >= _thicknessValues.Length Then Return

        Dim newThickness = _thicknessValues(index)
        ThicknessSlider.Tag = newThickness.ToString()

        Dim sd = MainVM.SelectedDrawable
        If sd Is Nothing Then Return

        If Not _thicknessBeforeEdit.HasValue Then
            _thicknessBeforeEdit = sd.StrokeThickness
        End If

        If Math.Abs(sd.StrokeThickness - newThickness) > 0.001 Then
            sd.StrokeThickness = newThickness
        End If
    End Sub

    ' ===== Sync slider from selected drawable =====

    Private Sub UpdateSliderFromThickness()

        ' Unhook old drawable
        If _currentDrawable IsNot Nothing Then
            RemoveHandler _currentDrawable.PropertyChanged, AddressOf OnDrawablePropertyChanged
        End If

        _currentDrawable = TryCast(MainVM.SelectedDrawable, BaseDrawable)

        ' Hook new drawbable
        If _currentDrawable IsNot Nothing Then
            AddHandler _currentDrawable.PropertyChanged, AddressOf OnDrawablePropertyChanged
        End If

        If _currentDrawable Is Nothing Then Return

        _updatingSlider = True

        Try
            Dim thickness = _currentDrawable.StrokeThickness
            Dim closestIndex = 0
            Dim minDiff = Double.MaxValue

            For i = 0 To _thicknessValues.Length - 1
                Dim diff = Math.Abs(_thicknessValues(i) - thickness)
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

    Private Sub OnDrawablePropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        If e.PropertyName = NameOf(BaseDrawable.StrokeThickness) Then
            UpdateSliderFromThickness()
        End If
    End Sub

    ' ===== Commit thickness change to undo stack =====

    Private Sub ThicknessSlider_PreviewMouseUp(sender As Object, e As MouseButtonEventArgs) Handles ThicknessSlider.PreviewMouseUp
        CommitThicknessUndo()
    End Sub

    Private Sub ThicknessSlider_PreviewKeyUp(sender As Object, e As KeyEventArgs) Handles ThicknessSlider.PreviewKeyUp
        If e.Key = Key.Left OrElse e.Key = Key.Right OrElse e.Key = Key.Up OrElse e.Key = Key.Down Then
            CommitThicknessUndo()
        End If
    End Sub

    Private Sub CommitThicknessUndo()
        If Not _thicknessBeforeEdit.HasValue Then Return

        Dim index = CInt(Math.Round(ThicknessSlider.Value))
        If index < 0 OrElse index >= _thicknessValues.Length Then Return

        Dim newThickness = _thicknessValues(index)

        If Math.Abs(_thicknessBeforeEdit.Value - newThickness) > 0.001 Then
            VM.ApplyStrokeThickness(newThickness, _thicknessBeforeEdit.Value)
        End If

        _thicknessBeforeEdit = Nothing
    End Sub


    Private Sub NumberBox_LostFocus(sender As Object, e As RoutedEventArgs)
        Dim numberBox = TryCast(sender, WPF.Ui.Controls.NumberBox)
        If numberBox Is Nothing Then Return

        numberBox.RaiseEvent(New KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(numberBox), 0, Key.Enter) With {.RoutedEvent = Keyboard.KeyDownEvent})
        numberBox.GetBindingExpression(WPF.Ui.Controls.NumberBox.ValueProperty)?.UpdateSource()
    End Sub



    Private Sub TextBox_GotKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        Dim tb = TryCast(sender, WPF.Ui.Controls.TextBox)
        If tb Is Nothing Then Return
        tb.SelectAll()
        e.Handled = True
    End Sub

End Class
