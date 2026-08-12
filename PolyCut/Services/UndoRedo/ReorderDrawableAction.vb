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