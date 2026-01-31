Imports System.Text.Json

Imports PolyCut.[Shared]

Public Class ClipboardService

    Private Const ClipFormat As String = "application/x-polycut-projectdata+json"


    Private ReadOnly _mainVM As MainViewModel
    Private ReadOnly _undoRedo As UndoRedoService
    Private ReadOnly _projectService As ProjectSerializationService

    Private ReadOnly _jsonOpts As New JsonSerializerOptions With {
        .PropertyNameCaseInsensitive = True,
        .WriteIndented = False
    }

    Public Sub New(mainVM As MainViewModel, undoRedo As UndoRedoService, projectService As ProjectSerializationService)
        _mainVM = mainVM
        _undoRedo = undoRedo
        _projectService = projectService
    End Sub

    Public Sub CopySelectionToClipboard()

        Dim selectedDrawables = _mainVM.SelectedDrawables
        If selectedDrawables.Count = 0 Then Return

        Dim ret = _projectService.CreateProjectDataForClipboard(selectedDrawables, _mainVM.DrawingGroup)

        Dim json As String = JsonSerializer.Serialize(ret, _jsonOpts)
        Dim data As New DataObject()
        data.SetData(ClipFormat, json)
        Clipboard.SetDataObject(data, copy:=True)

    End Sub

    Public Sub CutSelectionToClipboard()
        Dim selectedDrawables = _mainVM.SelectedDrawables
        If selectedDrawables.Count = 0 Then Return

        CopySelectionToClipboard()

        _mainVM.RemoveSelectedDrawables()

    End Sub

    Private _lastData As ProjectData
    Private offset As Integer = 0
    Public Sub PasteFromClipboard()
        Dim data = Clipboard.GetDataObject()
        If data Is Nothing OrElse Not data.GetDataPresent(ClipFormat) Then Return

        Dim json = TryCast(data.GetData(ClipFormat), String)
        If String.IsNullOrWhiteSpace(json) Then Return

        Dim pd = JsonSerializer.Deserialize(Of ProjectData)(json, _jsonOpts)
        If pd Is Nothing Then Return

        Dim designerStyle = TryCast(Application.Current?.TryFindResource("DesignerItemStyle"), Style)

        If _lastData Is Nothing OrElse _lastData.Hash = pd.Hash Then
            offset += 10
        Else
            offset = 10
        End If

        Dim act As New PasteDrawablesAction(_mainVM, _projectService, pd, designerStyle)


        If act.Execute() Then

            Dim items As New List(Of (IDrawable, TransformAction.Snapshot, TransformAction.Snapshot))

            For Each sel In _mainVM.SelectedDrawables
                Dim wrapper = TryCast(sel.DrawableElement.Parent, ContentControl)
                If wrapper Is Nothing Then Continue For

                Dim before = TransformAction.MakeSnapshotFromWrapper(wrapper)
                TransformAction.ApplyMove(wrapper, offset, offset)
                Dim after = TransformAction.MakeSnapshotFromWrapper(wrapper)
                items.Add((sel, before, after))
            Next
            Dim tfact As New TransformAction(items)

            Dim comboAct As New CompositeAction(New List(Of IUndoableAction) From {act, tfact})

            _undoRedo.Push(comboAct)
        End If
        _lastData = pd
    End Sub


End Class
