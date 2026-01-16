Imports System.Collections.ObjectModel
Imports System.Collections.Specialized

Imports PolyCut.Shared

''' <summary>
''' Manages selection state for drawable items.
''' Decouples selection logic from UI rendering.
''' </summary>
Public Class SelectionManager

    Private _selectedItems As New ObservableCollection(Of IDrawable)
    Private _cachedBounds As Rect?
    Private _boundsInvalid As Boolean = True

    Public Sub New()
        AddHandler _selectedItems.CollectionChanged, AddressOf OnSelectionCollectionChanged
    End Sub

    Public ReadOnly Property SelectedItems As IReadOnlyCollection(Of IDrawable)
        Get
            Return _selectedItems
        End Get
    End Property

    Public ReadOnly Property Count As Integer
        Get
            Return _selectedItems.Count
        End Get
    End Property

    Public ReadOnly Property HasSelection As Boolean
        Get
            Return _selectedItems.Count > 0
        End Get
    End Property

    Public ReadOnly Property HasMultipleSelection As Boolean
        Get
            Return _selectedItems.Count > 1
        End Get
    End Property


    Public Sub SelectItem(item As IDrawable, multiSelect As Boolean)
        If item Is Nothing Then Return

        If Not multiSelect Then
            ClearSelection()
        End If

        If Not _selectedItems.Contains(item) Then
            _selectedItems.Add(item)
            item.IsSelected = True
            InvalidateBounds()
        End If
    End Sub


    Public Sub DeselectItem(item As IDrawable)
        If item Is Nothing Then Return

        If _selectedItems.Contains(item) Then
            _selectedItems.Remove(item)
            item.IsSelected = False
            InvalidateBounds()
        End If
    End Sub


    Public Sub ToggleItem(item As IDrawable)
        If item Is Nothing Then Return

        If _selectedItems.Contains(item) Then
            DeselectItem(item)
        Else
            SelectItem(item, True)
        End If
    End Sub


    Public Sub SelectRange(items As IEnumerable(Of IDrawable), clearExisting As Boolean)
        If items Is Nothing Then Return

        If clearExisting Then
            ClearSelection()
        End If

        For Each item In items
            If item IsNot Nothing AndAlso Not _selectedItems.Contains(item) Then
                _selectedItems.Add(item)
                item.IsSelected = True
            End If
        Next

        InvalidateBounds()
    End Sub


    Public Sub ClearSelection()
        For Each item In _selectedItems.ToList()
            item.IsSelected = False
        Next
        _selectedItems.Clear()
        InvalidateBounds()
    End Sub


    Public Function GetSelectionBounds() As Rect?
        If Not HasSelection Then Return Nothing

        If _boundsInvalid OrElse Not _cachedBounds.HasValue Then
            _cachedBounds = CalculateBounds()
            _boundsInvalid = False
        End If

        Return _cachedBounds
    End Function


    Public Function GetUnrotatedBounds() As Rect?
        If Not HasSelection Then Return Nothing

        If Count = 1 Then
            ' For single selection, return actual object bounds (not AABB)
            Dim item = _selectedItems.FirstOrDefault()
            If item?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(item.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing Then
                    Dim left = Canvas.GetLeft(wrapper)
                    Dim top = Canvas.GetTop(wrapper)
                    Return New Rect(left, top, wrapper.ActualWidth, wrapper.ActualHeight)
                End If
            End If
        End If

        ' For multi-selection, return AABB
        Return GetSelectionBounds()
    End Function

    Private Function CalculateBounds() As Rect?
        If Not HasSelection Then Return Nothing

        Dim minX As Double = Double.MaxValue
        Dim minY As Double = Double.MaxValue
        Dim maxX As Double = Double.MinValue
        Dim maxY As Double = Double.MinValue

        For Each drawable In _selectedItems
            If drawable?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(drawable.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing Then
                    Dim itemBounds = GetTransformedBounds(wrapper)

                    minX = Math.Min(minX, itemBounds.Left)
                    minY = Math.Min(minY, itemBounds.Top)
                    maxX = Math.Max(maxX, itemBounds.Right)
                    maxY = Math.Max(maxY, itemBounds.Bottom)
                End If
            End If
        Next

        If minX = Double.MaxValue Then Return Nothing

        Return New Rect(minX, minY, maxX - minX, maxY - minY)
    End Function

    Private Function GetTransformedBounds(wrapper As ContentControl) As Rect
        Dim left = Canvas.GetLeft(wrapper)
        Dim top = Canvas.GetTop(wrapper)
        Dim width = wrapper.ActualWidth
        Dim height = wrapper.ActualHeight

        ' Handle rotation
        Dim rotateTransform = TryCast(wrapper.RenderTransform, RotateTransform)
        If rotateTransform Is Nothing OrElse Math.Abs(rotateTransform.Angle) < 0.01 Then
            Return New Rect(left, top, width, height)
        End If

        ' Calculate rotated bounds
        Dim angle = rotateTransform.Angle * Math.PI / 180.0
        Dim cosAngle = Math.Cos(angle)
        Dim sinAngle = Math.Sin(angle)

        Dim centerX = left + width * wrapper.RenderTransformOrigin.X
        Dim centerY = top + height * wrapper.RenderTransformOrigin.Y

        Dim corners() As Point = {
            New Point(0, 0),
            New Point(width, 0),
            New Point(0, height),
            New Point(width, height)
        }

        Dim minX = Double.MaxValue
        Dim minY = Double.MaxValue
        Dim maxX = Double.MinValue
        Dim maxY = Double.MinValue

        For Each corner In corners
            Dim cx = left + corner.X
            Dim cy = top + corner.Y

            Dim offsetX = cx - centerX
            Dim offsetY = cy - centerY

            Dim rotatedX = centerX + (offsetX * cosAngle - offsetY * sinAngle)
            Dim rotatedY = centerY + (offsetX * sinAngle + offsetY * cosAngle)

            minX = Math.Min(minX, rotatedX)
            minY = Math.Min(minY, rotatedY)
            maxX = Math.Max(maxX, rotatedX)
            maxY = Math.Max(maxY, rotatedY)
        Next

        Return New Rect(minX, minY, maxX - minX, maxY - minY)
    End Function

    Private Sub InvalidateBounds()
        _boundsInvalid = True
    End Sub

    Public Sub InvalidateBoundsCache()
        InvalidateBounds()
    End Sub

    Private Sub OnSelectionCollectionChanged(sender As Object, e As NotifyCollectionChangedEventArgs)
        InvalidateBounds()
        RaiseEvent SelectionChanged(Me, EventArgs.Empty)
    End Sub

    Public Function GetFirstSelectedItem() As IDrawable
        Return _selectedItems.FirstOrDefault()
    End Function

    Public Event SelectionChanged As EventHandler

End Class
