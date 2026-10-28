Option Strict On
Imports System.IO
Imports System.Threading
Imports System.Windows.Threading

Imports PolyCut.RichCanvas
Imports PolyCut.Shared

Imports Wpf.Ui
Imports Wpf.Ui.Controls

Public Module WeedingBoxTool
    Public Async Function AddAsync(document As Document, canvas As PolyCanvas, selection As SelectionManager, configuration As WeedingBoxConfiguration, dialogs As IContentDialogService, snackbars As SnackbarService) As Task(Of IUndoableAction)
        Return Await RunAsync(document, canvas, selection, configuration, dialogs, snackbars, regenerate:=False)
    End Function

    Public Async Function RegenerateAsync(document As Document, canvas As PolyCanvas, selection As SelectionManager, configuration As WeedingBoxConfiguration, dialogs As IContentDialogService, snackbars As SnackbarService) As Task(Of IUndoableAction)
        Return Await RunAsync(document, canvas, selection, configuration, dialogs, snackbars, regenerate:=True)
    End Function

    Public Async Function OpenOptionsAsync(document As Document, canvas As PolyCanvas, selection As SelectionManager, history As UndoRedoService, snackbars As SnackbarService) As Task
        If canvas Is Nothing Then Return
        canvas.CommitActiveText()
        canvas.UpdateLayout()

        Dim current = SettingsHandler.WeedingBoxConfiguration
        Dim draft As New WeedingBoxConfiguration With {
            .DesiredExteriorMargin = current.DesiredExteriorMargin,
            .MinimumInterObjectMargin = current.MinimumInterObjectMargin,
            .Colour = current.Colour,
            .SettingsFolder = current.SettingsFolder
        }
        Dim preview As IUndoableAction = Nothing
        Dim undoPreview As Action =
            Sub()
                Using ModelBatch.Begin()
                    preview?.Undo()
                End Using
                preview = Nothing
            End Sub
        Dim options As New WeedingBoxOptions With {.DataContext = draft}
        options.Regenerate =
            Async Function(token)
                Using cancellation = CancellationTokenSource.CreateLinkedTokenSource(token)
                    Dim targets = If(document.Drawables.Any(Function(item) TypeOf item IsNot DrawableGroup AndAlso WeedingBoxGenerator.IsWeedingItem(item)),
                        AddWeedingBoxesAction.GetSources(document), If(selection.HasSelection, selection.SelectedItems.ToList(), document.Drawables.ToList()))
                    Dim boxes = Await GenerateAsync(document, canvas, targets, draft, cancellation, skipExisting:=False)
                    history.ExecuteWithoutRecording(
                        Sub()
                            undoPreview()
                            preview = AddWeedingBoxesAction.Replace(document, selection, boxes)
                        End Sub)
                End Using
            End Function

        Dim dialog As New ToolConfigurationWindow("Weeding Box Options".LT(), options) With {.Owner = Application.Current.MainWindow}
        AddHandler dialog.Closing, Sub(sender, args) options.CancelRegeneration()
        Try
            Dim saved = dialog.ShowDialog()
            Await options.RegenerationTask
            If saved <> True Then Return
            Await draft.SetValue(draft.Name, draft)
            SettingsHandler.WeedingBoxConfiguration = draft
            history.Push(preview)
            preview = Nothing
        Catch ex As IOException
            snackbars.GenerateError("Error".LT(), ex.Message, 5)
        Finally
            history.ExecuteWithoutRecording(undoPreview)
        End Try
    End Function

    Private Async Function RunAsync(document As Document, canvas As PolyCanvas, selection As SelectionManager, configuration As WeedingBoxConfiguration, dialogs As IContentDialogService, snackbars As SnackbarService, regenerate As Boolean) As Task(Of IUndoableAction)
        If canvas Is Nothing Then Return Nothing
        canvas.CommitActiveText()
        canvas.UpdateLayout()

        Dim targets = If(regenerate, AddWeedingBoxesAction.GetSources(document),
            If(selection.HasSelection, selection.SelectedItems.ToList(), document.Drawables.ToList()))

        Using cancellation As New CancellationTokenSource()
            Dim dialog As New ContentDialog With {.Template = New ControlTemplate(GetType(ContentDialog))}
            Dim showing = dialogs.ShowAsync(dialog, CancellationToken.None)
            Dim cancelOnClosing As TypedEventHandler(Of ContentDialog, ContentDialogClosingEventArgs) = Sub(sender, args) cancellation.Cancel()
            AddHandler dialog.Closing, cancelOnClosing
            Dim result As IUndoableAction = Nothing

            Try
                ShowProgress(snackbars, dialog, If(regenerate, "Regenerating...".LT(), "Generating...".LT()))
                Dim boxes = Await GenerateAsync(document, canvas, targets, configuration, cancellation, Not regenerate, showing)
                If boxes IsNot Nothing AndAlso dialogs.GetDialogHostEx().Content Is dialog Then
                    result = ApplyBoxes(document, selection, boxes, regenerate)
                End If
            Catch ex As OperationCanceledException When cancellation.IsCancellationRequested
                ' cancel
            Finally
                RemoveHandler dialog.Closing, cancelOnClosing
                snackbars._snackbar?.TemplateButtonCommand.Execute(Nothing)
                dialog.Hide()
            End Try
            Await showing
            Return result

        End Using

    End Function

    Private Function ApplyBoxes(document As Document, selection As SelectionManager, boxes As List(Of Shape), regenerate As Boolean) As IUndoableAction
        If regenerate Then Return AddWeedingBoxesAction.Replace(document, selection, boxes)
        Dim action As New AddWeedingBoxesAction(document, boxes)
        Return If(action.Execute(), action, Nothing)
    End Function

    Private Async Function GenerateAsync(document As Document, canvas As PolyCanvas, targets As IEnumerable(Of IDrawable), configuration As WeedingBoxConfiguration, cancellation As CancellationTokenSource, skipExisting As Boolean, Optional showing As Task = Nothing) As Task(Of List(Of Shape))
        Dim documentChanged As EventHandler = Sub() cancellation.Cancel()
        Dim boxes As List(Of Shape)

        AddHandler document.Changed, documentChanged

        Try
            Await Dispatcher.Yield(DispatcherPriority.Background)
            If showing IsNot Nothing AndAlso showing.IsCompleted Then
                Await showing
                Return Nothing
            End If
            boxes = Await New WeedingBoxGenerator(configuration).GenerateAsync(
                targets, canvas, canvasItems:=document.Drawables, skipExisting:=skipExisting, cancellationToken:=cancellation.Token)
            Await Dispatcher.Yield(DispatcherPriority.Background)
            cancellation.Token.ThrowIfCancellationRequested()
            If showing IsNot Nothing AndAlso showing.IsCompleted Then Return Nothing
        Finally
            RemoveHandler document.Changed, documentChanged
        End Try

        Return boxes

    End Function

    Private Sub ShowProgress(snackbars As SnackbarService, dialog As ContentDialog, message As String)
        Dim content As New StackPanel With {.Orientation = Orientation.Horizontal}

        content.Children.Add(New ProgressRing With {
            .IsIndeterminate = True,
            .Width = 18,
            .Height = 18,
            .Margin = New Thickness(0, 0, 20, 0)
        })

        content.Children.Add(New System.Windows.Controls.TextBlock With {.Text = message, .VerticalAlignment = VerticalAlignment.Center})

        content.Children.Add(New Wpf.Ui.Controls.Button With {
            .Content = "Cancel".LT(),
            .Margin = New Thickness(20, 0, 0, 0),
            .Command = dialog.TemplateButtonCommand,
            .CommandParameter = ContentDialogButton.Close
        })

        snackbars.GetSnackbarPresenter().IsEnabled = True
        snackbars.ShowCustom(content, "Weeding Boxes".LT(), ControlAppearance.Dark, timeout:=Timeout.InfiniteTimeSpan)
        snackbars._snackbar.IsCloseButtonEnabled = False
    End Sub
End Module
