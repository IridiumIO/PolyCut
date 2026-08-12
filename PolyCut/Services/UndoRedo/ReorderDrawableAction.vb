Imports System.Collections.ObjectModel

Imports PolyCut.[Shared]

Public Class ReorderDrawableAction : Implements IUndoableAction

    Private ReadOnly _manager As IDrawableManager
    Private ReadOnly _drawable As IDrawable
    Private ReadOnly _parentGroup As DrawableGroup

    Private ReadOnly _sourceGroupIndex As Integer
    Private ReadOnly _targetGroupIndex As Integer
    Private ReadOnly _sourceCollectionIndex As Integer
    Private ReadOnly _targetCollectionIndex As Integer

    Public Sub New(manager As IDrawableManager, drawable As IDrawable, parentGroup As DrawableGroup,
                   sourceGroupIndex As Integer, targetGroupIndex As Integer,
                   sourceCollectionIndex As Integer, targetCollectionIndex As Integer)
        _manager = manager
        _drawable = drawable
        _parentGroup = parentGroup
        _sourceGroupIndex = sourceGroupIndex
        _targetGroupIndex = targetGroupIndex
        _sourceCollectionIndex = sourceCollectionIndex
        _targetCollectionIndex = targetCollectionIndex
    End Sub

    Public ReadOnly Property Description As String Implements IUndoableAction.Description
        Get
            Return $"Reorder: {_drawable?.Name}"
        End Get
    End Property

    Public Function Execute() As Boolean Implements IUndoableAction.Execute
        Return ApplyMove(_sourceGroupIndex, _targetGroupIndex, _sourceCollectionIndex, _targetCollectionIndex)
    End Function

    Public Sub Undo() Implements IUndoableAction.Undo
        ApplyMove(_targetGroupIndex, _sourceGroupIndex, _targetCollectionIndex, _sourceCollectionIndex)
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        ApplyMove(_sourceGroupIndex, _targetGroupIndex, _sourceCollectionIndex, _targetCollectionIndex)
    End Sub

    Private Function ApplyMove(fromGroup As Integer, toGroup As Integer,
                               fromCollection As Integer, toCollection As Integer) As Boolean
        If _drawable Is Nothing Then Return False

        ' Sidebar order lives in the parent group's children
        If _parentGroup IsNot Nothing AndAlso fromGroup >= 0 AndAlso fromGroup < _parentGroup.GroupChildren.Count Then
            Dim safeToGroup As Integer = Math.Min(Math.Max(toGroup, 0), _parentGroup.GroupChildren.Count - 1)
            If fromGroup <> safeToGroup Then
                _parentGroup.GroupChildren.Move(fromGroup, safeToGroup)
            End If
        End If

        ' Canvas Z-order lives in the flat drawable collection
        If _manager.DrawableCollection IsNot Nothing AndAlso fromCollection >= 0 AndAlso fromCollection < _manager.DrawableCollection.Count Then
            Dim safeToCollection As Integer = Math.Min(Math.Max(toCollection, 0), _manager.DrawableCollection.Count - 1)
            If fromCollection <> safeToCollection Then
                _manager.DrawableCollection.Move(fromCollection, safeToCollection)
            End If
        End If

        Dim mainVM = TryCast(_manager, MainViewModel)
        mainVM?.NotifyCollectionsChanged()

        Return True
    End Function

End Class

Public Class ReorderPlanner

    Public Shared Function Move(manager As IDrawableManager, items As IEnumerable(Of IDrawable), direction As Integer, parentGroupResolver As Func(Of IDrawable, DrawableGroup)) As List(Of IUndoableAction)

        Dim actions As New List(Of IUndoableAction)()
        Dim selected = items.Where(Function(x) x IsNot Nothing AndAlso Not TypeOf x Is DrawableGroup).ToList()
        If selected.Count = 0 Then Return actions

        Dim flat = manager.DrawableCollection
        Dim selectedSet As New HashSet(Of IDrawable)(selected)

        ' Absolute moves (very top / bottom): pre-compute each item's rank so the block lands contiguously, in its original relative order.
        Dim ranks As New Dictionary(Of IDrawable, Integer)()
        If direction = Integer.MaxValue OrElse direction = Integer.MinValue Then
            Dim byIndex = selected.OrderBy(Function(x) flat.IndexOf(x)).ToList()
            For i = 0 To byIndex.Count - 1
                ranks(byIndex(i)) = i
            Next
        End If

        ' Process in the direction of travel so earlier moves never collide with later tagrets: descending for higher indices, ascending for lower.
        Dim ordered As List(Of IDrawable)
        If direction > 0 Then
            ordered = selected.OrderByDescending(Function(x) flat.IndexOf(x)).ToList()
        Else
            ordered = selected.OrderBy(Function(x) flat.IndexOf(x)).ToList()
        End If

        For Each item In ordered
            Dim rank As Integer = 0
            ranks.TryGetValue(item, rank)
            Dim action = BuildAction(manager, flat, item, direction, selectedSet, rank, selected.Count, parentGroupResolver)

            If action IsNot Nothing AndAlso action.Execute() Then
                actions.Add(action)
            End If
        Next

        Return actions
    End Function

    Private Shared Function BuildAction(manager As IDrawableManager,
                                        flat As ObservableCollection(Of IDrawable),
                                        item As IDrawable,
                                        direction As Integer,
                                        selected As HashSet(Of IDrawable),
                                        rank As Integer,
                                        selectedCount As Integer,
                                        parentGroupResolver As Func(Of IDrawable, DrawableGroup)) As ReorderDrawableAction

        Dim parentGroup = parentGroupResolver(item)
        If parentGroup Is Nothing Then Return Nothing

        Dim srcGroupIndex = parentGroup.GroupChildren.IndexOf(item)
        If srcGroupIndex < 0 Then Return Nothing

        Dim dstGroupIndex = ComputeTarget(item, parentGroup.GroupChildren, direction, selected, rank, selectedCount)
        If dstGroupIndex = srcGroupIndex Then Return Nothing

        Dim srcCollIndex = flat.IndexOf(item)
        If srcCollIndex < 0 Then Return Nothing

        Dim dstCollIndex = ComputeTarget(item, flat, direction, selected, rank, selectedCount)
        If dstCollIndex = srcCollIndex Then Return Nothing

        Return New ReorderDrawableAction(manager, item, parentGroup, srcGroupIndex, dstGroupIndex, srcCollIndex, dstCollIndex)
    End Function

    Private Shared Function ComputeTarget(item As IDrawable,
                                          collection As ObservableCollection(Of IDrawable),
                                          direction As Integer,
                                          selected As HashSet(Of IDrawable),
                                          rank As Integer,
                                          selectedCount As Integer) As Integer
        Dim src = collection.IndexOf(item)
        If src < 0 Then Return src

        Select Case direction
            Case Integer.MinValue
                ' Very bottom
                Return rank
            Case Integer.MaxValue
                ' Very top
                Return Math.Max(0, collection.Count - selectedCount) + rank
            Case 1
                ' Move up one unless at the end or another selected item is above it
                If src >= collection.Count - 1 Then Return src
                If selected.Contains(collection(src + 1)) Then Return src
                Return src + 1
            Case -1
                ' mirror of above
                If src <= 0 Then Return src
                If selected.Contains(collection(src - 1)) Then Return src
                Return src - 1
            Case Else
                Return src
        End Select
    End Function

End Class