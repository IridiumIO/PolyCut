Option Strict On
Imports PolyCut.RichCanvas
Imports PolyCut.Shared

Public Class GroupAction : Implements IUndoableAction
    Private ReadOnly _manager As IDrawableManager
    Private ReadOnly _selectionManager As SelectionManager
    Private ReadOnly _selectedItems As List(Of IDrawable)
    Private _participants As List(Of IDrawable)
    Private _before As GroupingState
    Private _after As GroupingState

    Public Sub New(manager As IDrawableManager, selectedItems As IEnumerable(Of IDrawable), selectionManager As SelectionManager)
        _manager = manager
        _selectionManager = selectionManager
        _selectedItems = selectedItems.Where(Function(d) d IsNot Nothing).Distinct().ToList()
    End Sub

    Public ReadOnly Property Description As String = "Group" Implements IUndoableAction.Description

    Public Function Execute() As Boolean Implements IUndoableAction.Execute
        If _after IsNot Nothing Then
            Redo()
            Return True
        End If
        If _selectedItems.Count < 2 OrElse _selectedItems.Any(Function(d) TypeOf d Is DrawableGroup OrElse
            Not _manager.DrawableCollection.Contains(d) OrElse d.DrawableElement Is Nothing) Then Return False

        Dim parent = If(_manager.ParentGroupOf(_selectedItems(0)), _manager.DrawingGroup)
        If parent Is Nothing Then Return False
        Dim items = _selectedItems.OrderBy(Function(d) _manager.DrawableCollection.IndexOf(d)).ToList()
        _before = New GroupingState(_manager, items, _selectionManager)

        Using ModelBatch.Begin()
            For Each item In items
                _manager.RemoveDrawableFromCollection(item)
                If item.ParentGroup IsNot Nothing Then _manager.RemoveChildFromGroup(item.ParentGroup, item)
            Next

            Dim group = NestedDrawableGroup.CreateNestedGroup(items, "NewGroup")
            _manager.AddChildToGroup(parent, group)
            _manager.AddDrawableToCollection(group, -1)
            _manager.NotifyChanged()
            _selectionManager.ApplySelection({group}, clearExisting:=True)

            _participants = items.Concat({DirectCast(group, IDrawable)}).ToList()
            _after = New GroupingState(_manager, _participants, _selectionManager)
        End Using
        Return True
    End Function

    Public Sub Undo() Implements IUndoableAction.Undo
        If _after IsNot Nothing Then _before.Restore(_manager, _participants)
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        _after?.Restore(_manager, _participants)
    End Sub
End Class
