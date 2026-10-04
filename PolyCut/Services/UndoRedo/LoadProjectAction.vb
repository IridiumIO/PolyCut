Imports System.Collections.Generic
Imports System.Linq

Imports PolyCut.RichCanvas
Imports PolyCut.Shared
Imports PolyCut.Shared.Project

Namespace Services.UndoRedo

    Public Class LoadProjectAction
        Implements IUndoableAction

        Private ReadOnly _manager As IDrawableManager
        Private ReadOnly _projectService As ProjectSerializationService
        Private ReadOnly _filePath As String
        Private _projectData As ProjectData
        Private _previousProjectData As ProjectData

        Public Sub New(manager As IDrawableManager, projectService As ProjectSerializationService, filePath As String)
            _manager = manager
            _projectService = projectService
            _filePath = filePath
        End Sub

        Public ReadOnly Property Description As String Implements IUndoableAction.Description
            Get
                Return $"Load Project: {IO.Path.GetFileName(_filePath)}"
            End Get
        End Property

        Public Function Execute() As Boolean Implements IUndoableAction.Execute

            _previousProjectData = CaptureCurrentProject()

            _projectData = _projectService.LoadProject(_filePath)
            If _projectData Is Nothing Then Return False

            ClearCurrentProject()
            Return RestoreProjectState(_projectData)
        End Function

        Public Sub Undo() Implements IUndoableAction.Undo
            If _previousProjectData Is Nothing Then Return

            ClearCurrentProject()
            RestoreProjectState(_previousProjectData)
        End Sub

        Private Function CaptureCurrentProject() As ProjectData
            Dim mainVM = TryCast(_manager, MainViewModel)

            Dim groups As IEnumerable(Of IDrawable) = Enumerable.Empty(Of IDrawable)()
            Dim config As Core.ProcessorConfiguration = Nothing
            If mainVM IsNot Nothing Then
                groups = mainVM.ImportedGroups
                config = mainVM.Configuration
            End If

            Return _projectService.CreateProjectData(_manager.DrawableCollection, groups, config)
        End Function

        Public Sub Redo() Implements IUndoableAction.Redo
            If _projectData Is Nothing Then Return

            ClearCurrentProject()
            RestoreProjectState(_projectData)
        End Sub

        Private Sub ClearCurrentProject()
            ' Clear DrawingGroup children first
            Dim mainVM = TryCast(_manager, MainViewModel)
            If mainVM IsNot Nothing AndAlso mainVM.DrawingGroup IsNot Nothing Then
                mainVM.DrawingGroup.GroupChildren.Clear()
            End If

            ' Clear all drawables
            Dim drawablesToRemove = _manager.DrawableCollection.ToList()
            If mainVM IsNot Nothing Then
                mainVM.RemoveDrawablesFromCollection(drawablesToRemove)
            Else
                For Each drawable In drawablesToRemove
                    _manager.RemoveDrawableFromCollection(drawable)
                Next
            End If

            ' Clear imported groups
            If mainVM IsNot Nothing Then
                mainVM.ImportedGroups.Clear()
            End If
        End Sub

        Private Function RestoreProjectState(projectData As ProjectData) As Boolean
            Try
                Dim mainVM = TryCast(_manager, MainViewModel)
                Dim designerStyle = TryCast(Application.Current?.TryFindResource("DesignerItemStyle"), Style)
                Dim built As RuntimeProjectModel = _projectService.BuildRuntimeModel(projectData, designerStyle)

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

End Namespace
