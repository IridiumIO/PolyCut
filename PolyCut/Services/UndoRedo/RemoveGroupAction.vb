Option Strict On
Imports PolyCut.[Shared]
Imports PolyCut.RichCanvas

Public Class RemoveGroupAction : Implements IUndoableAction

    Private ReadOnly _manager As IDrawableManager
    Private ReadOnly _selectionManager As SelectionManager
    Private ReadOnly _group As DrawableGroup
    Private ReadOnly _wasInImportedGroups As Boolean
    Private ReadOnly _indexInImportedGroups As Integer
    Private ReadOnly _parentGroup As DrawableGroup
    Private ReadOnly _collectionMembers As New List(Of (Drawable As IDrawable, Index As Integer, Snapshot As TransformState))()

    Public Sub New(manager As IDrawableManager, group As DrawableGroup, selectionManager As SelectionManager)
        _manager = manager
        _selectionManager = selectionManager
        _group = group
        _parentGroup = TryCast(group.ParentGroup, DrawableGroup)

        _wasInImportedGroups = _manager.ContainsImportRoot(group)
        _indexInImportedGroups = If(_wasInImportedGroups, _manager.IndexOfImportRoot(group), -1)

        CaptureCollectionMembers()
    End Sub

    Private Sub CaptureCollectionMembers()
        If _manager.DrawableCollection Is Nothing Then Return

        Dim tracked As New HashSet(Of IDrawable)()
        For Each d In _manager.DrawableCollection
            tracked.Add(d)
        Next

        AddCollectionMember(_group, tracked)
        CaptureChildren(_group, tracked, New HashSet(Of IDrawable)())
    End Sub

    Private Sub CaptureChildren(drawable As IDrawable, tracked As HashSet(Of IDrawable), seen As HashSet(Of IDrawable))
        For Each child In ProjectGraph.GetGroupChildren(drawable)
            If child Is Nothing Then Continue For
            If _manager.IsDrawingGroup(child) Then
                Continue For
            End If
            If Not seen.Add(child) Then Continue For

            AddCollectionMember(child, tracked)
            CaptureChildren(child, tracked, seen)
        Next
    End Sub

    Private Sub AddCollectionMember(drawable As IDrawable, tracked As HashSet(Of IDrawable))
        If drawable Is Nothing OrElse Not tracked.Contains(drawable) Then Return

        _collectionMembers.Add((drawable, _manager.DrawableCollection.IndexOf(drawable), TransformAction.CaptureState(drawable)))
    End Sub

    Public ReadOnly Property Description As String Implements IUndoableAction.Description
        Get
            Return $"Remove Group: {_group?.Name}"
        End Get
    End Property

    Public Function Execute() As Boolean Implements IUndoableAction.Execute
        If _group Is Nothing Then Return False

        If _parentGroup IsNot Nothing Then
            _parentGroup.RemoveChild(_group)
        ElseIf _manager.ContainsImportRoot(_group) Then
            _manager.RemoveImportRoot(_group)
        End If

        'A removed group must not stay selected: the canvas would keep a gizmo on something the document no longer holds, and the next delete would act on nothing.
        Try
            _selectionManager.DeselectItem(_group)
            For Each m In _collectionMembers
                _selectionManager.DeselectItem(m.Drawable)
            Next
        Catch
        End Try

        If _collectionMembers.Count > 0 Then
            _manager.RemoveDrawablesFromCollection(_collectionMembers.Select(Function(m) m.Drawable))
        End If

        _manager.NotifyChanged()

        Return True
    End Function

    Public Sub Undo() Implements IUndoableAction.Undo
        If _group Is Nothing Then Return

        If _parentGroup IsNot Nothing Then
            If Not _manager.ContainsChild(_parentGroup, _group) Then
                _parentGroup.AddChild(_group)
            End If
        ElseIf _wasInImportedGroups Then
            _manager.AddImportRoot(_group, _indexInImportedGroups)
        End If

        For Each m In _collectionMembers
            QueueMemberRestore(m.Drawable, m.Index, m.Snapshot)
        Next

        Dim topLevel = TryCast(_manager.TopLevelGroupOf(_group), DrawableGroup)
        topLevel?.RebuildDisplayChildren()
        _group.RebuildDisplayChildren()

        _manager.NotifyChanged()
    End Sub

    Private Sub QueueMemberRestore(drawable As IDrawable, index As Integer, snapshot As TransformState)
        ModelBatch.RestoreToCollection(_manager, drawable, index,
            Sub() TransformAction.ApplyState(snapshot, drawable))
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        Execute()
    End Sub

End Class
