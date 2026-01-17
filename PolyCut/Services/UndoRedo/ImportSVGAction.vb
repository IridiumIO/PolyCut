Imports PolyCut.[Shared]

Public Class ImportSVGAction : Implements IUndoableAction

    Private ReadOnly _manager As IDrawableManager
    Private ReadOnly _svgService As ISvgImportService
    Private ReadOnly _filePath As String
    Private _importedGroup As DrawableGroup
    Private _allImportedDrawables As List(Of IDrawable)
    Private _indexInImportedGroups As Integer

    Public Sub New(manager As IDrawableManager, svgService As ISvgImportService, filePath As String)
        _manager = manager
        _svgService = svgService
        _filePath = filePath
        _allImportedDrawables = New List(Of IDrawable)()
    End Sub

    Public ReadOnly Property Description As String Implements IUndoableAction.Description
        Get
            Return $"Import SVG: {System.IO.Path.GetFileName(_filePath)}"
        End Get
    End Property

    Public Function Execute() As Boolean Implements IUndoableAction.Execute
        If String.IsNullOrEmpty(_filePath) Then Return False
        If Not System.IO.File.Exists(_filePath) Then Return False

        Dim imported = _svgService.ParseFromFile(_filePath)
        If imported Is Nothing OrElse Not imported.Any() Then Return False

        _importedGroup = New DrawableGroup(System.IO.Path.GetFileName(_filePath))

        For Each d As IDrawable In imported
            _importedGroup.AddChild(d)

            If TypeOf d Is DrawableGroup Then
                Dim grp = CType(d, DrawableGroup)
                For Each child In grp.GroupChildren.ToList()
                    If Not _manager.DrawableCollection.Contains(child) Then
                        _manager.DrawableCollection.Add(child)
                        _allImportedDrawables.Add(child)
                    End If
                Next
            Else
                If Not _manager.DrawableCollection.Contains(d) Then
                    _manager.DrawableCollection.Add(d)
                    _allImportedDrawables.Add(d)
                End If
            End If
        Next

        Dim mainVM = TryCast(_manager, MainViewModel)
        If mainVM IsNot Nothing Then
            _indexInImportedGroups = mainVM.ImportedGroups.Count
            mainVM.ImportedGroups.Add(_importedGroup)
        End If

        Return True
    End Function

    Public Sub Undo() Implements IUndoableAction.Undo
        If _importedGroup Is Nothing Then Return

        For Each drawable In _allImportedDrawables
            _manager.RemoveDrawableFromCollection(drawable)
            _manager.ClearDrawableParent(drawable)
        Next

        Dim mainVM = TryCast(_manager, MainViewModel)
        If mainVM IsNot Nothing AndAlso mainVM.ImportedGroups.Contains(_importedGroup) Then
            mainVM.ImportedGroups.Remove(_importedGroup)
        End If
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        If _importedGroup Is Nothing Then Return

        For Each drawable In _allImportedDrawables
            If Not _manager.DrawableCollection.Contains(drawable) Then
                _manager.DrawableCollection.Add(drawable)
            End If
        Next

        Dim mainVM = TryCast(_manager, MainViewModel)
        If mainVM IsNot Nothing AndAlso Not mainVM.ImportedGroups.Contains(_importedGroup) Then
            If _indexInImportedGroups >= 0 AndAlso _indexInImportedGroups <= mainVM.ImportedGroups.Count Then
                mainVM.ImportedGroups.Insert(_indexInImportedGroups, _importedGroup)
            Else
                mainVM.ImportedGroups.Add(_importedGroup)
            End If
        End If
    End Sub

End Class
