Option Strict On
Imports PolyCut.Shared

Public Class AddWeedingBoxesAction
    Implements IUndoableAction

    Private ReadOnly _manager As IDrawableManager
    Private ReadOnly _shapes As List(Of Shape)
    Private ReadOnly _actions As New List(Of AddDrawableAction)
    Private _root As DrawableGroup
    Private _createdRoot As Boolean
    Private _rootIndex As Integer

    Public Sub New(manager As IDrawableManager, shapes As IEnumerable(Of Shape))
        _manager = manager
        _shapes = shapes.ToList()
    End Sub

    Public ReadOnly Property Description As String Implements IUndoableAction.Description
        Get
            Return "Add Weeding Boxes"
        End Get
    End Property

    Public Function Execute() As Boolean Implements IUndoableAction.Execute
        If _shapes.Count = 0 Then Return False
        _root = _manager.ImportRoots.OfType(Of DrawableGroup)().FirstOrDefault(Function(group) group.Name = WeedingBoxGenerator.ROOT_NAME)
        _createdRoot = _root Is Nothing
        If _createdRoot Then _root = New DrawableGroup(WeedingBoxGenerator.ROOT_NAME)
        Using ModelBatch.Begin()
            For Each shape In _shapes
                Dim action As New AddDrawableAction(_manager, shape, _root)
                If action.Execute() Then _actions.Add(action)
            Next
            _rootIndex = _manager.IndexOfImportRoot(_root)
            _manager.NotifyChanged()
        End Using
        Return _actions.Count > 0
    End Function

    Public Sub Undo() Implements IUndoableAction.Undo
        Using ModelBatch.Begin()
            For i = _actions.Count - 1 To 0 Step -1
                _actions(i).Undo()
            Next
            If _createdRoot AndAlso _root.GroupChildren.Count = 0 Then _manager.RemoveImportRoot(_root)
            _manager.NotifyChanged()
        End Using
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        Using ModelBatch.Begin()
            _manager.AddImportRoot(_root, _rootIndex)
            For Each action In _actions
                action.Redo()
            Next
            _manager.NotifyChanged()
        End Using
    End Sub
End Class
