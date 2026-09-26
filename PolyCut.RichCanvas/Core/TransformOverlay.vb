Imports PolyCut.Shared

Public Class TransformOverlay
    Inherits Grid

    Private _gizmo As TransformGizmo
    Private _selectionManager As SelectionManager
    Private _contentCanvas As Canvas

    Public Sub New()
        Me.IsHitTestVisible = True
        Me.Background = Nothing
        Me.ClipToBounds = True
    End Sub

    Public Sub Initialize(selectionManager As SelectionManager, contentCanvas As Canvas)
        _selectionManager = selectionManager
        _contentCanvas = contentCanvas

        _gizmo = New TransformGizmo(selectionManager, contentCanvas)
        _gizmo.IsHitTestVisible = True

        Me.Children.Add(_gizmo)

        AddHandler _selectionManager.SelectionChanged, AddressOf OnSelectionChanged
        AddHandler Me.SizeChanged, AddressOf OnSizeChanged

        AddHandler Me.PreviewMouseDown, AddressOf OnOverlayPreviewMouseDown
        AddHandler Me.PreviewMouseUp, AddressOf OnOverlayPreviewMouseUp

        UpdateGizmo()
    End Sub

    Private Function GetZoomBorder() As ZoomBorder
        Dim current As DependencyObject = _contentCanvas
        While current IsNot Nothing
            Dim zb = TryCast(current, ZoomBorder)
            If zb IsNot Nothing Then Return zb
            current = VisualTreeHelper.GetParent(current)
        End While
        Return Nothing
    End Function

    Private Sub OnOverlayPreviewMouseDown(sender As Object, e As MouseButtonEventArgs)
        If e.ChangedButton <> MouseButton.Middle Then Return

        Dim zb = GetZoomBorder()
        If zb Is Nothing Then Return
        zb.BeginPan(e)
        _panZoomBorder = zb
        e.Handled = True
    End Sub


    Private Sub OnOverlayPreviewMouseUp(sender As Object, e As MouseButtonEventArgs)
        If _panZoomBorder Is Nothing OrElse e.ChangedButton <> MouseButton.Middle Then Return

        Dim zb = _panZoomBorder
        _panZoomBorder = Nothing
        zb.EndPan(e)
        e.Handled = True
    End Sub

    Private _panZoomBorder As ZoomBorder

    Private Sub OnSelectionChanged(sender As Object, e As EventArgs)
        UpdateGizmo()
    End Sub

    Private Sub OnSizeChanged(sender As Object, e As SizeChangedEventArgs)
        UpdateGizmo()
    End Sub

    Private Sub UpdateGizmo()
        If _gizmo Is Nothing Then Return

        If Me.ActualWidth > 0 AndAlso Me.ActualHeight > 0 Then
            _gizmo.Width = Me.ActualWidth
            _gizmo.Height = Me.ActualHeight
        Else
            _gizmo.Width = Double.NaN
            _gizmo.Height = Double.NaN
            _gizmo.HorizontalAlignment = HorizontalAlignment.Stretch
            _gizmo.VerticalAlignment = VerticalAlignment.Stretch
        End If

        Dim shouldBeVisible = _selectionManager.HasSelection
        Dim isTextBoxFocused = False

        If shouldBeVisible Then
            For Each item In _selectionManager.SelectedItems
                If (item?.DrawableElement) Is Nothing Then Continue For

                Dim wrapper = TryCast(item.DrawableElement.Parent, ContentControl)
                If wrapper Is Nothing OrElse (TypeOf wrapper.Content IsNot TextBox) Then Continue For

                Dim textBox = CType(wrapper.Content, TextBox)
                If Not textBox.IsFocused AndAlso Not textBox.IsKeyboardFocusWithin Then Continue For

                shouldBeVisible = False
                isTextBoxFocused = True
                Exit For
            Next
        End If

        _gizmo.Visibility = If(shouldBeVisible, Visibility.Visible, Visibility.Collapsed)


        _gizmo.IsHitTestVisible = Not isTextBoxFocused

        _gizmo.InvalidateVisual()
    End Sub

    Public Sub UpdateGizmoImmediate()
        If _selectionManager IsNot Nothing Then
            _selectionManager.InvalidateBoundsCache()
        End If
        UpdateGizmo()
    End Sub

End Class
