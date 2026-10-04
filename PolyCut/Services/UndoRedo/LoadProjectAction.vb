Imports PolyCut.RichCanvas
Imports PolyCut.Shared

Public Class LoadProjectAction

    Private ReadOnly _manager As IDrawableManager
    Private ReadOnly _projectService As ProjectSerializationService
    Private ReadOnly _filePath As String

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

        Dim mainVM = TryCast(_manager, MainViewModel)
        Dim drawablesToRemove = _manager.DrawableCollection.ToList()

        If mainVM IsNot Nothing Then
            mainVM.RemoveDrawablesFromCollection(drawablesToRemove)
            mainVM.ImportedGroups.Clear()
        Else
            For Each drawable In drawablesToRemove
                _manager.RemoveDrawableFromCollection(drawable)
            Next
        End If
    End Sub

    Private Function RestoreProjectState(projectData As ProjectData) As Boolean
        Try
            Dim mainVM = TryCast(_manager, MainViewModel)
            Dim built As RuntimeProjectModel = _projectService.BuildRuntimeModel(projectData, DesignerStyle())

            Dim allGroups As List(Of IDrawable) = built.Groups

            ' Populate VM group list (top-level groups only)
            If mainVM IsNot Nothing Then
                mainVM.ImportedGroups.Clear()

                For Each g In allGroups
                    If g.ParentGroup Is Nothing Then
                        mainVM.ImportedGroups.Add(g)
                    End If
                Next
            End If

            ' --- Ensure DrawingGroup exists 
            If mainVM IsNot Nothing Then
                mainVM.DrawingGroup = Nothing

                ' Prefer: find by name among existing DrawableGroup groups
                For Each g In allGroups.OfType(Of DrawableGroup)()
                    If String.Equals(g.Name, "Drawing Group", StringComparison.OrdinalIgnoreCase) Then
                        mainVM.DrawingGroup = g
                        Exit For
                    End If
                Next

                If mainVM.DrawingGroup Is Nothing Then
                    mainVM.DrawingGroup = New DrawableGroup("Drawing Group")
                    mainVM.ImportedGroups.Add(mainVM.DrawingGroup)
                    allGroups.Add(mainVM.DrawingGroup)
                End If
            End If

            Dim toAdd As List(Of IDrawable) = ProjectSerializationService.CanvasItemsInDocumentOrder(projectData, built)

            If mainVM IsNot Nothing Then
                mainVM.AddDrawablesToCollection(toAdd)
            Else
                For Each d In toAdd
                    _manager.AddDrawableToCollection(d, -1)
                Next
            End If

            If mainVM IsNot Nothing AndAlso mainVM.DrawingGroup IsNot Nothing Then
                If mainVM.DrawableCollection.Contains(mainVM.DrawingGroup) Then
                    mainVM.DrawableCollection.Remove(mainVM.DrawingGroup)
                End If
                mainVM.AddDrawableToCollection(mainVM.DrawingGroup, 0)
            End If

            If projectData.Configuration IsNot Nothing Then
                mainVM.Configuration = Text.Json.JsonSerializer.Deserialize(Of PolyCut.Core.ProcessorConfiguration)(projectData.Configuration)
            End If

            Application.Current.Dispatcher.BeginInvoke(New Action(Sub()
                                                                      ProjectApplyHelper.ApplyWrapperStateForPaste(projectData, built)
                                                                      PolyCanvas.ClearSelection()

                                                                      Try : Application.Current.MainWindow?.UpdateLayout() : Catch : End Try
                                                                      mainVM?.NotifyCollectionsChanged()
                                                                  End Sub),
                                        Threading.DispatcherPriority.Normal)

            Return True

        Catch ex As Exception
            Debug.WriteLine($"Failed to restore project state: {ex.Message}")
            Return False
        End Try
    End Function

End Class


