Imports PolyCut.Shared

Public Class TransformOverlay
    Inherits Grid

    Private _gizmo As TransformGizmo
    Private _selectionManager As SelectionManager
    Private _contentCanvas As Canvas

    Public Sub New()
        Me.IsHitTestVisible = True
        Me.Background = Nothing
        Me.ClipToBounds = False
    End Sub

    Public Sub Initialize(selectionManager As SelectionManager, contentCanvas As Canvas)
        _selectionManager = selectionManager
        _contentCanvas = contentCanvas

        _gizmo = New TransformGizmo(selectionManager, contentCanvas)
        _gizmo.IsHitTestVisible = True

        Me.Children.Add(_gizmo)

        AddHandler _selectionManager.SelectionChanged, AddressOf OnSelectionChanged
        AddHandler Me.SizeChanged, AddressOf OnSizeChanged

        UpdateGizmo()
    End Sub

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
                If item?.DrawableElement IsNot Nothing Then
                    Dim wrapper = TryCast(item.DrawableElement.Parent, ContentControl)
                    If wrapper IsNot Nothing AndAlso TypeOf wrapper.Content Is TextBox Then
                        Dim textBox = CType(wrapper.Content, TextBox)
                        If textBox.IsFocused OrElse textBox.IsKeyboardFocusWithin Then
                            shouldBeVisible = False
                            isTextBoxFocused = True
                            Exit For
                        End If
                    End If
                End If
            Next
        End If

        _gizmo.Visibility = If(shouldBeVisible, Visibility.Visible, Visibility.Collapsed)


        _gizmo.IsHitTestVisible = Not isTextBoxFocused

        _gizmo.InvalidateVisual()
    End Sub

    Public Sub SetScale(scale As Double)
        If _gizmo IsNot Nothing Then
            _gizmo.Scale = scale
        End If
    End Sub

    Public Sub UpdateGizmoImmediate()
        If _selectionManager IsNot Nothing Then
            _selectionManager.InvalidateBoundsCache()
        End If
        UpdateGizmo()
    End Sub

End Class
