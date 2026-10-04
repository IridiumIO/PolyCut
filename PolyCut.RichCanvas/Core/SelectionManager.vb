Imports System.Collections.ObjectModel
Imports System.Collections.Specialized

Imports PolyCut.Shared

''' <summary>
''' Manages selection state for drawable items.
''' Decouples selection logic from UI rendering.
''' </summary>
Public Class SelectionManager

    Private _selectedItems As New ObservableCollection(Of IDrawable)
    Private ReadOnly _selectedSet As New HashSet(Of IDrawable)
    Private _notificationBatchDepth As Integer
    Private _hasDeferredNotification As Boolean
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

    Public ReadOnly Property IsBatchingNotifications As Boolean
        Get
            Return _notificationBatchDepth > 0
        End Get
    End Property

    'Batches selection notifications: every mutation inside the block raises CollectionChanged, but SelectionChanged
    'is raised once on the outermost EndNotificationBatch. Without this each item costs a transform-overlay update plus a
    'selection-bounds recomputation, which is quadratic across a bulk select.
    Private Sub BeginNotificationBatch()
        _notificationBatchDepth += 1
    End Sub

    Private Sub EndNotificationBatch()
        If _notificationBatchDepth = 0 Then Return
        _notificationBatchDepth -= 1
        If _notificationBatchDepth = 0 AndAlso _hasDeferredNotification Then
            _hasDeferredNotification = False
            RaiseEvent SelectionChanged(Me, EventArgs.Empty)
        End If
    End Sub


    'The single place selection is mutated. Runs inside one notification batch, keeps _selectedItems and _selectedSet in
    'step, sets the model flag itself, and is idempotent: items already in the requested state raise nothing.
    Public Sub ApplySelection(itemsToSelect As IEnumerable(Of IDrawable), Optional itemsToDeselect As IEnumerable(Of IDrawable) = Nothing, Optional clearExisting As Boolean = False)
        BeginNotificationBatch()
        Try
            If clearExisting Then ClearSelection()

            If itemsToDeselect IsNot Nothing Then
                For Each item In itemsToDeselect
                    If item Is Nothing Then Continue For
                    _selectedItems.Remove(item)
                    _selectedSet.Remove(item)
                    'Clear the flag even when the item was not tracked: a direct IsSelected write can leave the model
                    'flag set while the manager knows nothing about the item.
                    If item.IsSelected Then item.IsSelected = False
                Next
            End If

            If itemsToSelect IsNot Nothing Then
                For Each item In itemsToSelect
                    If item IsNot Nothing AndAlso Not _selectedSet.Contains(item) Then
                        _selectedItems.Add(item)
                        _selectedSet.Add(item)
                        item.IsSelected = True
                    End If
                Next
            End If
        Finally
            EndNotificationBatch()
        End Try
        InvalidateBounds()
    End Sub

    Public Sub SelectItem(item As IDrawable, multiSelect As Boolean)
        If item Is Nothing Then Return
        ApplySelection({item}, Nothing, Not multiSelect)
    End Sub


    Public Sub DeselectItem(item As IDrawable)
        If item Is Nothing Then Return
        ApplySelection(Nothing, {item})
    End Sub


    Public Sub ToggleItem(item As IDrawable)
        If item Is Nothing Then Return

        If _selectedSet.Contains(item) Then
            ApplySelection(Nothing, {item})
        Else
            ApplySelection({item})
        End If
    End Sub




    Public Sub ClearSelection()
        BeginNotificationBatch()
        Try
            For Each item In _selectedItems.ToList()
                item.IsSelected = False
            Next
            _selectedItems.Clear()
            _selectedSet.Clear()
        Finally
            EndNotificationBatch()
        End Try
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
                    Dim size = TransformMath.GetWrapperSize(wrapper)
                    Return New Rect(left, top, size.Width, size.Height)
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
                    Dim itemBounds = TransformMath.GetWorldBounds(wrapper)

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


    Private Sub InvalidateBounds()
        _boundsInvalid = True
    End Sub

    Public Sub InvalidateBoundsCache()
        InvalidateBounds()
    End Sub

    Private Sub OnSelectionCollectionChanged(sender As Object, e As NotifyCollectionChangedEventArgs)
        InvalidateBounds()
        If _notificationBatchDepth > 0 Then
            _hasDeferredNotification = True
            Return
        End If
        RaiseEvent SelectionChanged(Me, EventArgs.Empty)
    End Sub

    Public Function GetFirstSelectedItem() As IDrawable
        Return _selectedItems.FirstOrDefault()
    End Function

    Public Event SelectionChanged As EventHandler

End Class
