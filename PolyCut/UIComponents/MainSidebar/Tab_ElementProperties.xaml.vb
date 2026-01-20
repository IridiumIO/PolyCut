Imports System.ComponentModel

Imports PolyCut.RichCanvas

Imports PolyCut.Shared

Public Class Tab_ElementProperties

    ' ===== Cached state for undo =====
    Private _thicknessBeforeEditMap As IDictionary(Of IDrawable, Double)
    Private _strokeBeforeEdit As IDictionary(Of IDrawable, Brush)
    Private _fillBeforeEdit As IDictionary(Of IDrawable, Brush)


    Private ReadOnly _thicknessValues As Double() = {0, 0.01, 0.1, 0.2, 0.5, 1, 2, 3, 4, 5, 10}

    ' Guards
    Private _updatingSlider As Boolean
    Private _suppressThicknessHandler As Boolean

    ' Track currently-selected drawable for change notifications
    Private _currentDrawable As BaseDrawable
    Private _subscribedDrawables As New List(Of BaseDrawable)()



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
        UpdateSelectedDrawablesSubscriptions()
        UpdateSliderFromThickness()
    End Sub

    Private Sub OnViewModelPropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        If e.PropertyName = NameOf(MainVM.SelectedDrawable) OrElse e.PropertyName = NameOf(MainVM.SelectedDrawables) OrElse e.PropertyName = NameOf(MainVM.HasMultipleSelected) Then
            UpdateSelectedDrawablesSubscriptions()
            UpdateSliderFromThickness()
        End If
    End Sub

    Private Sub UpdateSelectedDrawablesSubscriptions()

        For Each bd In _subscribedDrawables
            Try
                RemoveHandler bd.PropertyChanged, AddressOf OnDrawablePropertyChanged
            Catch
            End Try
        Next
        _subscribedDrawables.Clear()

        If MainVM Is Nothing Then Return

        For Each d In MainVM.SelectedDrawables.OfType(Of BaseDrawable)()
            AddHandler d.PropertyChanged, AddressOf OnDrawablePropertyChanged
            _subscribedDrawables.Add(d)
        Next
    End Sub


    ' ===== Fill =====

    Private Sub FillColorPicker_PopupOpening(sender As Object, e As EventArgs)
        Dim items = MainVM.SelectedDrawables.ToList()
        If items.Count = 0 Then
            _fillBeforeEdit = Nothing
            Return
        End If

        Dim map As New Dictionary(Of IDrawable, Brush)()
        For Each d In items
            map(d) = d.Fill
        Next
        _fillBeforeEdit = map
    End Sub

    Private Sub FillColorPicker_ColorSelected(sender As Object, e As ColorSelectedEventArgs)
        VM.ApplyFill(e.SelectedBrush, _fillBeforeEdit)
        _fillBeforeEdit = Nothing
    End Sub


    ' ===== Stroke =====

    Private Sub StrokeColorPicker_PopupOpening(sender As Object, e As EventArgs)
        Dim items = MainVM.SelectedDrawables.ToList()
        If items.Count = 0 Then
            _strokeBeforeEdit = Nothing
            Return
        End If

        Dim map As New Dictionary(Of IDrawable, Brush)()
        For Each d In items
            map(d) = d.Stroke
        Next
        _strokeBeforeEdit = map
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

        If _thicknessBeforeEditMap Is Nothing Then
            Dim items = MainVM.SelectedDrawables.ToList()
            Dim map As New Dictionary(Of IDrawable, Double)()
            For Each d In items
                map(d) = d.StrokeThickness
            Next
            _thicknessBeforeEditMap = map
        End If


        For Each d In MainVM.SelectedDrawables.ToList()
            If Math.Abs(d.StrokeThickness - newThickness) > 0.001 Then
                d.StrokeThickness = newThickness
            End If
        Next
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
        If _thicknessBeforeEditMap Is Nothing Then Return

        Dim index = CInt(Math.Round(ThicknessSlider.Value))
        If index < 0 OrElse index >= _thicknessValues.Length Then Return

        Dim newThickness = _thicknessValues(index)

        Dim changed As Boolean = False
        For Each kv In _thicknessBeforeEditMap
            If Math.Abs(kv.Value - newThickness) > 0.001 Then
                changed = True
                Exit For
            End If
        Next

        If changed Then
            VM.ApplyStrokeThickness(newThickness, _thicknessBeforeEditMap)
        End If

        _thicknessBeforeEditMap = Nothing
    End Sub


    Private Sub NumberBox_LostFocus(sender As Object, e As WPF.Ui.Controls.NumberBoxValueChangedEventArgs)
        Dim numberBox = TryCast(sender, WPF.Ui.Controls.NumberBox)
        If numberBox Is Nothing OrElse _before Is Nothing Then Return

        Dim center = New Point(_before.Left + _before.Width / 2, _before.Top + _before.Height / 2)
        Dim startRotation As Double = CType(_before.RenderTransform, RotateTransform)?.Angle Or 0

        Dim newRotation As Double = (numberBox.Value - startRotation) Mod 360

        If Math.Abs(newRotation - startRotation) < 0.01 Then
            _before = Nothing
            Return
        End If

        TransformAction.ApplyRotation(MainVM.SelectedWrapper, center, startRotation, newRotation, New Point(_before.Left, _before.Top))
        MainVM.SelectedWrapper.UpdateLayout()

        _after = TransformAction.MakeSnapshotFromWrapper(MainVM.SelectedWrapper)

        Dim items As New List(Of (IDrawable, Object, Object)) From {
            (MainVM.SelectedDrawable, _before, _after)
        }
        EventAggregator.Publish(Of TransformCompletedMessage)(
                New TransformCompletedMessage With {.Items = items}
            )

        _before = Nothing
        _after = Nothing
    End Sub



    Private _before As TransformAction.Snapshot
    Private _after As TransformAction.Snapshot


    Private Sub TextBox_GotKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)

        _before = TransformAction.MakeSnapshotFromWrapper(MainVM.SelectedWrapper)


        Dim tb = TryCast(sender, WPF.Ui.Controls.TextBox)
        If tb Is Nothing Then Return
        tb.SelectAll()
        e.Handled = True
    End Sub



    Private ReadOnly _mmConv As New InputToMillimetresConverter()

    Private Sub TextBox_LostFocus(sender As Object, e As RoutedEventArgs)

        If MainVM.SelectedWrapper Is Nothing OrElse MainVM.SelectedDrawable Is Nothing Then Return
        If _before Is Nothing Then Return

        CommitTextBox(CType(sender, WPF.Ui.Controls.TextBox))



        TransformAction.SetSizeAndPosition(MainVM.SelectedWrapper, MainVM.SelectedWrapper.Width,
        MainVM.SelectedWrapper.Height,
        Canvas.GetLeft(MainVM.SelectedWrapper),
        Canvas.GetTop(MainVM.SelectedWrapper))

        MainVM.SelectedWrapper.UpdateLayout()
        _after = TransformAction.MakeSnapshotFromWrapper(MainVM.SelectedWrapper)

        Dim items As New List(Of (IDrawable, Object, Object)) From {
        (MainVM.SelectedDrawable, _before, _after)
    }

        EventAggregator.Publish(Of TransformCompletedMessage)(
                New TransformCompletedMessage With {.Items = items}
            )

        _before = Nothing
        _after = Nothing
    End Sub


    Private Function CommitTextBox(tb As WPF.Ui.Controls.TextBox) As Boolean
        Dim be = tb.GetBindingExpression(WPF.Ui.Controls.TextBox.TextProperty)
        If be Is Nothing Then Return True
        be.UpdateSource()
        Return Not Validation.GetHasError(tb)
    End Function
End Class
