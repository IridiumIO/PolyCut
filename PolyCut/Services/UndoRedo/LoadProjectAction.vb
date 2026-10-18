Option Strict On
Imports PolyCut.RichCanvas
Imports PolyCut.Shared

Public Class LoadProjectAction

    Private ReadOnly _manager As IDrawableManager
    Private ReadOnly _projectService As ProjectSerializationService
    Private ReadOnly _filePath As String

    Public Property LoadedConfiguration As PolyCut.Core.ProcessorConfiguration

    Public Sub New(manager As IDrawableManager, projectService As ProjectSerializationService, filePath As String)
        _manager = manager
        _projectService = projectService
        _filePath = filePath
    End Sub

    Public Function Execute() As Boolean
        Dim projectData = _projectService.LoadProject(_filePath)
        If projectData Is Nothing Then Return False

        ClearCurrentProject()
        Return RestoreProjectState(projectData)
    End Function

    Private Shared Function DesignerStyle() As Style
        Return TryCast(Application.Current?.TryFindResource("DesignerItemStyle"), Style)
    End Function

    Private Sub ClearCurrentProject()
        Dim drawablesToRemove = _manager.DrawableCollection.ToList()

        _manager.RemoveDrawablesFromCollection(drawablesToRemove)
        For Each root In _manager.ImportRoots.ToList()
            _manager.RemoveImportRoot(root)
        Next
    End Sub

    Private Function RestoreProjectState(projectData As ProjectData) As Boolean
        Try
            Dim built As RuntimeProjectModel = _projectService.BuildRuntimeModel(projectData, DesignerStyle())

            Dim allGroups As List(Of IDrawable) = built.Groups

            For Each root In _manager.ImportRoots.ToList()
                _manager.RemoveImportRoot(root)
            Next

            For Each g In allGroups
                If g.ParentGroup Is Nothing Then _manager.AddImportRoot(g)
            Next

            ' --- Ensure DrawingGroup exists 
            _manager.DrawingGroup = Nothing

            For Each g In allGroups
                Dim dg = TryCast(g, DrawableGroup)
                If dg IsNot Nothing AndAlso _manager.IsDrawingGroup(dg) Then
                    _manager.DrawingGroup = dg
                    Exit For
                End If
            Next

            If _manager.DrawingGroup Is Nothing Then
                Dim drawingGroup As New DrawableGroup(Document.DrawingGroupName)
                _manager.DrawingGroup = drawingGroup
                _manager.AddImportRoot(drawingGroup)
                allGroups.Add(drawingGroup)
            End If

            Dim toAdd As List(Of IDrawable) = ProjectSerializationService.CanvasItemsInDocumentOrder(projectData, built)

            _manager.AddDrawablesToCollection(toAdd)

            Dim drawing As IDrawable = _manager.DrawingGroup
            If drawing IsNot Nothing Then
                _manager.RemoveDrawableFromCollection(drawing)
                _manager.AddDrawableToCollection(drawing, 0)
            End If

            If projectData.Configuration IsNot Nothing Then
                LoadedConfiguration = Text.Json.JsonSerializer.Deserialize(Of PolyCut.Core.ProcessorConfiguration)(projectData.Configuration)
            End If

            Application.Current.Dispatcher.BeginInvoke(New Action(Sub()
                                                                      ProjectApplyHelper.ApplyWrapperStateForPaste(projectData, built, selectRestoredItems:=False)
                                                                      PolyCanvas.ClearSelection()

                                                                      Try : Application.Current.MainWindow?.UpdateLayout() : Catch : End Try
                                                                      _manager.NotifyChanged()
                                                                  End Sub),
                                        Threading.DispatcherPriority.Normal)

            Return True

        Catch ex As Exception
            Debug.WriteLine($"Failed to restore project state: {ex.Message}")
            Return False
        End Try
    End Function

End Class


