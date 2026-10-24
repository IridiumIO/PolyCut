Option Strict On
Imports PolyCut.[Shared]

Public Class ImportSVGAction : Implements IUndoableAction

    Private ReadOnly _manager As IDrawableManager
    Private ReadOnly _svgService As IVectorImportService
    Private ReadOnly _filePath As String
    Private ReadOnly _svgData As Svg.SvgDocument
    Private _importedGroup As DrawableGroup
    Private _allImportedDrawables As List(Of IDrawable)
    Private _indexInImportedGroups As Integer
    Private _parentMap As New Dictionary(Of IDrawable, DrawableGroup)()

    Public Sub New(manager As IDrawableManager, svgService As IVectorImportService, filePath As String)
        _manager = manager
        _svgService = svgService
        _filePath = filePath
        _allImportedDrawables = New List(Of IDrawable)()
    End Sub

    Public Sub New(manager As IDrawableManager, svgService As IVectorImportService, svgdata As Svg.SvgDocument, filePath As String)
        _manager = manager
        _svgService = svgService
        _svgData = svgdata
        _filePath = filePath 'we use the data from the doc but still want the filename for the group naming
        _allImportedDrawables = New List(Of IDrawable)()
    End Sub

    Public ReadOnly Property Description As String Implements IUndoableAction.Description
        Get
            Return $"Import SVG: {IO.Path.GetFileName(_filePath)}"
        End Get
    End Property

    Public Function Execute() As Boolean Implements IUndoableAction.Execute
        If String.IsNullOrEmpty(_filePath) AndAlso _svgData Is Nothing Then Return False
        If Not String.IsNullOrEmpty(_filePath) AndAlso Not System.IO.File.Exists(_filePath) Then Return False

        Dim imported = If(_svgData IsNot Nothing, _svgService.ParseFromDocument(_svgData), _svgService.ParseFromFile(_filePath))
        If imported Is Nothing OrElse Not imported.Any() Then Return False

        Using ModelBatch.Begin()
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

            For Each d In _allImportedDrawables
                Dim pg = TryCast(d.ParentGroup, DrawableGroup)
                If pg IsNot Nothing Then
                    _parentMap(d) = pg
                End If
            Next

            'Importing the same file twice must not leave two roots sharing a name as this causes ambigutiy with copy/paste (TODO: stop using name lookup for paste)
            Dim baseName As String = _importedGroup.Name
            Dim suffix As Integer = 2
            While _manager.ImportRoots.Any(Function(g) String.Equals(g.Name, _importedGroup.Name, StringComparison.OrdinalIgnoreCase))
                _importedGroup.Name = $"{baseName}_{suffix}"
                suffix += 1
            End While

            _indexInImportedGroups = _manager.ImportRoots.Count()
            _manager.AddImportRoot(_importedGroup)
        End Using

        Return True
    End Function

    Public Sub Undo() Implements IUndoableAction.Undo
        If _importedGroup Is Nothing Then Return

        For Each drawable In _allImportedDrawables
            _manager.RemoveDrawableFromCollection(drawable)

            Dim parentGrp As DrawableGroup = Nothing
            If _parentMap.TryGetValue(drawable, parentGrp) Then
                parentGrp.RemoveChild(drawable)
            End If

            _manager.ClearDrawableParent(drawable)
        Next

        If _manager.ContainsImportRoot(_importedGroup) Then
            _manager.RemoveImportRoot(_importedGroup)
        End If
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        If _importedGroup Is Nothing Then Return

        For Each drawable In _allImportedDrawables

            Dim parentGrp As DrawableGroup = Nothing
            If _parentMap.TryGetValue(drawable, parentGrp) Then
                If Not _manager.ContainsChild(parentGrp, drawable) Then
                    parentGrp.AddChild(drawable)
                End If
            End If

            _manager.AddDrawableToCollection(drawable, -1)
        Next

        If Not _manager.ContainsImportRoot(_importedGroup) Then
            _manager.AddImportRoot(_importedGroup, _indexInImportedGroups)
        End If
    End Sub

End Class