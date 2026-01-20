Imports PolyCut.[Shared]

Public Class RemoveGroupAction : Implements IUndoableAction

    Private ReadOnly _manager As IDrawableManager
    Private ReadOnly _group As DrawableGroup
    Private ReadOnly _allLeafChildren As List(Of IDrawable)
    Private ReadOnly _allNestedGroups As List(Of DrawableGroup)
    Private ReadOnly _wasInImportedGroups As Boolean
    Private ReadOnly _wasInDrawableCollection As Boolean
    Private ReadOnly _indexInImportedGroups As Integer
    Private ReadOnly _indexInDrawableCollection As Integer
    Private ReadOnly _parentGroup As DrawableGroup

    Public Sub New(manager As IDrawableManager, group As DrawableGroup)
        _manager = manager
        _group = group

        _allLeafChildren = group.GetAllLeafChildren().ToList()
        _allNestedGroups = New List(Of DrawableGroup)()

        For Each child In group.GroupChildren
            CollectNestedGroups(child, _allNestedGroups)
        Next

        _parentGroup = TryCast(group.ParentGroup, DrawableGroup)

        Dim mainVM = TryCast(manager, MainViewModel)
        If mainVM IsNot Nothing Then
            _wasInImportedGroups = mainVM.ImportedGroups.Contains(group)
            _indexInImportedGroups = If(_wasInImportedGroups, mainVM.ImportedGroups.IndexOf(group), -1)
        End If

        _wasInDrawableCollection = manager.DrawableCollection.Contains(group)
        _indexInDrawableCollection = If(_wasInDrawableCollection, manager.DrawableCollection.IndexOf(group), -1)
    End Sub

    Private Sub CollectNestedGroups(item As IDrawable, groups As List(Of DrawableGroup))
        Dim grp = TryCast(item, DrawableGroup)
        If grp IsNot Nothing Then
            groups.Add(grp)
            For Each child In grp.GroupChildren
                CollectNestedGroups(child, groups)
            Next
        End If
    End Sub

    Public ReadOnly Property Description As String Implements IUndoableAction.Description
        Get
            Return $"Remove Group: {_group?.Name}"
        End Get
    End Property

    Public Function Execute() As Boolean Implements IUndoableAction.Execute
        If _group Is Nothing Then Return False

        For Each ch In _allLeafChildren
            If _manager.DrawableCollection.Contains(ch) Then
                _manager.RemoveDrawableFromCollection(ch)
            End If
            _manager.ClearDrawableParent(ch)
        Next

        Dim mainVM = TryCast(_manager, MainViewModel)
        For Each grp In _allNestedGroups
            If mainVM IsNot Nothing AndAlso (grp Is mainVM.DrawingGroup OrElse
               String.Equals(grp.Name, "Drawing Group", StringComparison.OrdinalIgnoreCase)) Then
                Continue For
            End If

            If _manager.DrawableCollection.Contains(grp) Then
                _manager.RemoveDrawableFromCollection(grp)
            End If
        Next

        If _parentGroup IsNot Nothing Then
            _parentGroup.RemoveChild(_group)
        Else
            If mainVM IsNot Nothing AndAlso mainVM.ImportedGroups.Contains(_group) Then
                mainVM.ImportedGroups.Remove(_group)
            End If
        End If

        _manager.RemoveDrawableFromCollection(_group)

        If mainVM IsNot Nothing Then
            mainVM.NotifyCollectionsChanged()
        End If

        Return True
    End Function

    Public Sub Undo() Implements IUndoableAction.Undo
        If _group Is Nothing Then Return

        Dim mainVM = TryCast(_manager, MainViewModel)

        For Each ch In _allLeafChildren
            If Not _manager.DrawableCollection.Contains(ch) Then
                _manager.DrawableCollection.Add(ch)
            End If
        Next

        For Each grp In _allNestedGroups
            If Not _manager.DrawableCollection.Contains(grp) Then
                _manager.DrawableCollection.Add(grp)
            End If
        Next

        If _wasInDrawableCollection Then
            _manager.AddDrawableToCollection(_group, _indexInDrawableCollection)
        End If

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

        Dim mainVM2 = TryCast(_manager, MainViewModel)
        If mainVM2 IsNot Nothing Then
            Dim topLevel = mainVM2.GetTopLevelGroup(_group)
            If topLevel IsNot Nothing Then
                topLevel.RebuildDisplayChildren()
            End If
        End If
        _group.RebuildDisplayChildren()

        If mainVM IsNot Nothing Then
            mainVM.NotifyCollectionsChanged()
        End If
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        Execute()
    End Sub

End Class


