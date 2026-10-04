Imports PolyCut.[Shared]

Public Class RemoveGroupAction : Implements IUndoableAction

    Private ReadOnly _manager As IDrawableManager
    Private ReadOnly _group As DrawableGroup
    Private ReadOnly _wasInImportedGroups As Boolean
    Private ReadOnly _indexInImportedGroups As Integer
    Private ReadOnly _parentGroup As DrawableGroup
    Private ReadOnly _collectionMembers As New List(Of (Drawable As IDrawable, Index As Integer, Snapshot As TransformAction.Snapshot))()

    Public Sub New(manager As IDrawableManager, group As DrawableGroup)
        _manager = manager
        _group = group
        _parentGroup = TryCast(group.ParentGroup, DrawableGroup)

        Dim mainVM = TryCast(manager, MainViewModel)
        If mainVM IsNot Nothing Then
            _wasInImportedGroups = mainVM.ImportedGroups.Contains(group)
            _indexInImportedGroups = If(_wasInImportedGroups, mainVM.ImportedGroups.IndexOf(group), -1)
        End If

        CaptureCollectionMembers(mainVM)
    End Sub

    Private Sub CaptureCollectionMembers(mainVM As MainViewModel)
        If _manager.DrawableCollection Is Nothing Then Return

        Dim tracked As New HashSet(Of IDrawable)()
        For Each d In _manager.DrawableCollection
            tracked.Add(d)
        Next

        AddCollectionMember(_group, tracked)
        CaptureChildren(_group, tracked, New HashSet(Of IDrawable)(), mainVM)
    End Sub

    Private Sub CaptureChildren(drawable As IDrawable, tracked As HashSet(Of IDrawable), seen As HashSet(Of IDrawable), mainVM As MainViewModel)
        For Each child In ProjectGraph.GetGroupChildren(drawable)
            If child Is Nothing Then Continue For
            If mainVM IsNot Nothing AndAlso (child Is mainVM.DrawingGroup OrElse
               String.Equals(child.Name, "Drawing Group", StringComparison.OrdinalIgnoreCase)) Then
                Continue For
            End If
            If Not seen.Add(child) Then Continue For

            AddCollectionMember(child, tracked)
            CaptureChildren(child, tracked, seen, mainVM)
        Next
    End Sub

    Private Sub AddCollectionMember(drawable As IDrawable, tracked As HashSet(Of IDrawable))
        If drawable Is Nothing OrElse Not tracked.Contains(drawable) Then Return

        _collectionMembers.Add((drawable, _manager.DrawableCollection.IndexOf(drawable), TransformAction.MakeSnapshotFromDrawable(drawable)))
    End Sub

    Public ReadOnly Property Description As String Implements IUndoableAction.Description
        Get
            Return $"Remove Group: {_group?.Name}"
        End Get
    End Property

    Public Function Execute() As Boolean Implements IUndoableAction.Execute
        If _group Is Nothing Then Return False

        Dim mainVM = TryCast(_manager, MainViewModel)
        If _parentGroup IsNot Nothing Then
            _parentGroup.RemoveChild(_group)
        ElseIf mainVM IsNot Nothing AndAlso mainVM.ImportedGroups.Contains(_group) Then
            mainVM.ImportedGroups.Remove(_group)
        End If

        If _collectionMembers.Count > 0 Then
            If mainVM IsNot Nothing Then
                mainVM.RemoveDrawablesFromCollection(_collectionMembers.Select(Function(m) m.Drawable))
            Else
                For Each m In _collectionMembers
                    _manager.RemoveDrawableFromCollection(m.Drawable)
                Next
            End If
        End If

        If mainVM IsNot Nothing Then
            mainVM.NotifyCollectionsChanged()
        End If

        Return True
    End Function

    Public Sub Undo() Implements IUndoableAction.Undo
        If _group Is Nothing Then Return

        Dim mainVM = TryCast(_manager, MainViewModel)

        If _parentGroup IsNot Nothing Then
            If Not _parentGroup.GroupChildren.Contains(_group) Then
                _parentGroup.AddChild(_group)
            End If
        ElseIf mainVM IsNot Nothing AndAlso _wasInImportedGroups Then
            If _indexInImportedGroups >= 0 AndAlso _indexInImportedGroups <= mainVM.ImportedGroups.Count Then
                mainVM.ImportedGroups.Insert(_indexInImportedGroups, _group)
            Else
                mainVM.ImportedGroups.Add(_group)
            End If
        End If

        For Each m In _collectionMembers
            QueueMemberRestore(m.Drawable, m.Index, m.Snapshot)
        Next

        Dim topLevel = If(mainVM IsNot Nothing, mainVM.GetTopLevelGroup(_group), Nothing)
        If topLevel IsNot Nothing Then
            topLevel.RebuildDisplayChildren()
        End If
        _group.RebuildDisplayChildren()

        If mainVM IsNot Nothing Then
            mainVM.NotifyCollectionsChanged()
        End If
    End Sub

    Private Sub QueueMemberRestore(drawable As IDrawable, index As Integer, snapshot As TransformAction.Snapshot)
        ModelBatch.RestoreToCollection(_manager, drawable, index,
            Sub() TransformAction.ApplySnapshot(snapshot, drawable))
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        Execute()
    End Sub

End Class
