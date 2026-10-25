Option Strict On
Imports PolyCut.Shared
Imports PolyCut.RichCanvas

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
            Dim names = _manager.DrawableCollection.Select(Function(item) item.Name).ToHashSet()
            For Each shape In _shapes
                Dim action As New AddDrawableAction(_manager, shape, _root)
                If Not action.Execute() Then Continue For
                Dim drawable = _root.GroupChildren.Last()
                WeedingBoxGenerator.TrackSources(drawable, DirectCast(shape.Tag, Guid()))
                Dim prefix = shape.Uid & "_wb"
                Dim index = 1
                While names.Contains(prefix & index)
                    index += 1
                End While
                drawable.Name = prefix & index
                names.Add(drawable.Name)
                _actions.Add(action)
            Next
            _rootIndex = _manager.IndexOfImportRoot(_root)
            _manager.NotifyChanged()
        End Using
        Return _actions.Count > 0
    End Function

    Public Shared Function Regenerate(document As Document, canvas As UIElement, selection As SelectionManager, configuration As WeedingBoxConfiguration) As IUndoableAction
        Dim boxes = document.Drawables.Where(Function(item) WeedingBoxGenerator.IsWeedingItem(item) AndAlso
            WeedingBoxGenerator.GetSourceIds(item).Length > 0).ToList()
        Dim leaves = document.Drawables.Where(Function(item) TypeOf item IsNot DrawableGroup).
            SelectMany(Function(item) If(item.IsAnyGroup(), item.GetLeafChildren(), New List(Of IDrawable) From {item})).ToHashSet()
        Dim ids = boxes.SelectMany(AddressOf WeedingBoxGenerator.GetSourceIds).ToHashSet()
        Dim sources = leaves.Where(Function(item) ids.Contains(item.Id)).ToList()
        Dim shapes = New WeedingBoxGenerator(configuration).Generate(sources, canvas, sceneItems:=document.Drawables)
        Dim actions As New List(Of IUndoableAction)
        Using ModelBatch.Begin()
            For Each box In boxes
                Dim removal As New RemoveDrawableAction(document, box, selection)
                If removal.Execute() Then actions.Add(removal)
            Next
            Dim addition As New AddWeedingBoxesAction(document, shapes)
            If addition.Execute() Then actions.Add(addition)
            document.NotifyChanged()
        End Using
        Return If(actions.Count > 0, New CompositeAction(actions), Nothing)
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
