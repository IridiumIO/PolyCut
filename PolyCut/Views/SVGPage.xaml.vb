Imports System.ComponentModel

Imports PolyCut.RichCanvas
Imports PolyCut.Shared

Imports Wpf.Ui.Controls
Class SVGPage

    Public ReadOnly Property MainViewModel As MainViewModel
    Public ReadOnly Property SVGPageViewModel As SVGPageViewModel
    Public ReadOnly Property SelectionManager As SelectionManager
    Public ReadOnly Property Document As Document

    Private _subscribedPrinter As Printer

    Private _hasLoaded As Boolean = False
    Private _overlayTextBox As Controls.TextBox
    Private _overlayEffectiveFontSize As Double = Double.NaN
    Private _importPlacement As TaskCompletionSource(Of Point?)
    Private ReadOnly _importToolTip As New System.Windows.Controls.ToolTip With {
        .Placement = Controls.Primitives.PlacementMode.Relative,
        .StaysOpen = True,
        .IsHitTestVisible = False
    }

    Sub New(viewmodel As SVGPageViewModel, selectionManager As SelectionManager, document As Document)
        Me.SelectionManager = selectionManager
        Me.Document = document
        Me.SVGPageViewModel = viewmodel
        Me.MainViewModel = viewmodel.MainVM
        Me.DataContext = viewmodel

        InitializeComponent()
        MainViewModel.ImportPlacement = AddressOf RequestImportPlacement

        AddHandler MainViewModel.PropertyChanged, AddressOf MainViewModel_PropertyChanged


        SubscribeToPrinter(MainViewModel.Printer)

        AddHandler MainViewModel.Configuration.PropertyChanged, AddressOf PropertyChangedHandler
        AddHandler zoomPanControl.DrawingManager.DrawingFinished, AddressOf DrawingFinishedHandler
        AddHandler zoomPanControl.DrawingManager.TextEditor.EditRequested, AddressOf OnTextEditRequested
        AddHandler zoomPanControl.DrawingManager.TextEditor.TextEdited, AddressOf OnTextEdited
        AddHandler zoomPanControl.ContextMenuOpening, AddressOf OnCanvasContextMenuOpening
        AddHandler zoomPanControl.ContextMenu.Opened, AddressOf OnCanvasContextMenuOpened
        AddHandler selectionManager.SelectionChanged, AddressOf OnSelectionCountChanged

    End Sub

    Sub OnLoaded() Handles Me.Loaded
        If Not _hasLoaded Then zoomPanControl.Reset(skipAnimation:=True)
        _hasLoaded = True
    End Sub

    Private Sub OnSelectionCountChanged(sender As Object, e As EventArgs)

        MainSidebar.ElementsTab.SyncListViewSelection(SelectionManager.SelectedItems)
        SyncTextStyleOverlayToSelection()
    End Sub

    Private Sub SyncTextStyleOverlayToSelection()
        Dim selected = SelectionManager.SelectedItems
        Dim textBox = TryCast(selected?.FirstOrDefault()?.DrawableElement, Controls.TextBox)
        If _overlayTextBox IsNot textBox Then
            If _overlayTextBox IsNot Nothing Then RemoveHandler _overlayTextBox.LayoutUpdated, AddressOf OnOverlayTextLayoutUpdated
            _overlayTextBox = textBox
            If _overlayTextBox IsNot Nothing Then AddHandler _overlayTextBox.LayoutUpdated, AddressOf OnOverlayTextLayoutUpdated
        End If
        If textBox IsNot Nothing Then
            SyncOverlayTo(textBox)
        End If
    End Sub

    Private Sub OnOverlayTextLayoutUpdated(sender As Object, e As EventArgs)
        If _overlayTextBox Is Nothing OrElse OverlayFontSizeComboBox.IsKeyboardFocusWithin Then Return
        Dim size = TextGeometryHelper.GetEffectiveFontSize(_overlayTextBox, mainCanvas)
        If Math.Abs(size - _overlayEffectiveFontSize) > 0.000001 Then SyncOverlayTo(_overlayTextBox)
    End Sub




    Private Sub MainViewModel_PropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        If e Is Nothing Then Return

        ' When MainViewModel.Printer reference changes, re-subscribe to the new instance
        If String.Equals(e.PropertyName, NameOf(MainViewModel.Printer), StringComparison.OrdinalIgnoreCase) Then
            SubscribeToPrinter(MainViewModel.Printer)
        End If
    End Sub

    Private Sub SubscribeToPrinter(pr As Printer)
        ' Unsubscribe old printer events
        If _subscribedPrinter IsNot Nothing Then
            RemoveHandler _subscribedPrinter.PropertyChanged, AddressOf PropertyChangedHandler
        End If

        _subscribedPrinter = pr

        If _subscribedPrinter IsNot Nothing Then
            AddHandler _subscribedPrinter.PropertyChanged, AddressOf PropertyChangedHandler
        End If

    End Sub


    Private Sub DrawingFinishedHandler(sender As Object, shape As UIElement)
        If sender Is Nothing Then Return
        MainViewModel.AddDrawableElement(shape)
    End Sub

    Private Sub PropertyChangedHandler(sender As Object, e As PropertyChangedEventArgs)

        MainViewModel.GCodePaths.Clear()
        MainViewModel.GCode = ""

    End Sub

    Private Sub Page_Drop(sender As Object, e As DragEventArgs)
        _importToolTip.IsOpen = False
        If Not e.Data.GetDataPresent(DataFormats.FileDrop) Then Return
        Dim position As Point? = Nothing
        If (e.KeyStates And DragDropKeyStates.ShiftKey) <> 0 Then position = e.GetPosition(mainCanvas)
        SVGPageViewModel.ProcessDroppedFiles(TryCast(e.Data.GetData(DataFormats.FileDrop), String()), position)
    End Sub

    Private Sub Page_DragOver(sender As Object, e As DragEventArgs) Handles Me.DragOver
        If Not e.Data.GetDataPresent(DataFormats.FileDrop) OrElse (e.KeyStates And DragDropKeyStates.ShiftKey) = 0 Then
            _importToolTip.IsOpen = False
            Return
        End If

        Dim files = DirectCast(e.Data.GetData(DataFormats.FileDrop), String())
        Dim firstFile = files.FirstOrDefault(Function(f) ImportService.AllSupportedExtensions.Contains(IO.Path.GetExtension(f), StringComparer.InvariantCultureIgnoreCase))
        If firstFile Is Nothing Then
            _importToolTip.IsOpen = False
            Return
        End If

        Dim stackp As New StackPanel
        TextOptions.SetTextFormattingMode(stackp, TextFormattingMode.Display)

        stackp.Children.Add(New TextBlock With {.Text = "Click to place", .FontWeight = FontWeights.SemiBold, .FontSize = 13, .Margin = New Thickness(0, 0, 0, 4)})
        stackp.Children.Add(New TextBlock With {.Text = IO.Path.GetFileName(firstFile), .FontSize = 12})

        _importToolTip.Content = stackp
        UpdateImportToolTip(e.GetPosition(zoomPanControl))
    End Sub

    Private Sub Page_DragLeave(sender As Object, e As DragEventArgs) Handles Me.DragLeave
        _importToolTip.IsOpen = False
    End Sub


    Private StartPos As Point

    Private Function RequestImportPlacement(filename As String) As Task(Of Point?)
        FinishImportPlacement(Nothing)
        mainCanvas.CommitActiveText()
        SVGPageViewModel.CanvasToolMode = CanvasMode.Selection
        SelectionManager.ClearSelection()
        zoomPanControl.Focus()
        _importPlacement = New TaskCompletionSource(Of Point?)(TaskCreationOptions.RunContinuationsAsynchronously)

        Dim stackp As New StackPanel
        TextOptions.SetTextFormattingMode(stackp, TextFormattingMode.Display)

        stackp.Children.Add(New TextBlock With {.Text = "Click to place", .FontWeight = FontWeights.SemiBold, .FontSize = 13, .Margin = New Thickness(0, 0, 0, 4)})
        stackp.Children.Add(New TextBlock With {.Text = filename, .FontSize = 12})

        _importToolTip.Content = stackp

        UpdateImportToolTip()
        Return _importPlacement.Task
    End Function

    Private Sub PlaceImport(sender As Object, e As MouseButtonEventArgs) Handles Me.PreviewMouseDown
        If _importPlacement Is Nothing OrElse e.ChangedButton <> MouseButton.Left OrElse Not zoomPanControl.IsMouseOver Then Return
        e.Handled = True
        SVGPageViewModel.CanvasToolMode = CanvasMode.Selection
        FinishImportPlacement(e.GetPosition(mainCanvas))
    End Sub

    Private Sub CancelImport(sender As Object, e As KeyEventArgs) Handles Me.PreviewKeyDown
        If _importPlacement Is Nothing OrElse e.Key <> Key.Escape Then Return
        e.Handled = True
        FinishImportPlacement(Nothing)
    End Sub

    Private Sub FinishImportPlacement(position As Point?)
        Dim pending = _importPlacement
        _importPlacement = Nothing
        _importToolTip.IsOpen = False
        pending?.TrySetResult(position)
    End Sub

    Private Sub ImportCursorMoved(sender As Object, e As MouseEventArgs) Handles zoomPanControl.MouseMove, zoomPanControl.MouseLeave
        UpdateImportToolTip()
    End Sub

    Private Sub UpdateImportToolTip(Optional dragPosition As Point? = Nothing)
        Dim position = If(dragPosition, Mouse.GetPosition(zoomPanControl))
        _importToolTip.PlacementTarget = zoomPanControl
        _importToolTip.HorizontalOffset = position.X + 16
        _importToolTip.VerticalOffset = position.Y + 20
        _importToolTip.IsOpen = dragPosition.HasValue OrElse (_importPlacement IsNot Nothing AndAlso zoomPanControl.IsMouseOver)
    End Sub

    Private Sub MainView_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles zoomPanControl.MouseDown
        ' DON'T steal focus if a TextBox is currently in edit mode
        Dim focusedElement = TryCast(Keyboard.FocusedElement, System.Windows.Controls.TextBox)
        If focusedElement IsNot Nothing AndAlso (focusedElement.IsFocused OrElse focusedElement.IsKeyboardFocusWithin) Then
            Return
        End If

        zoomPanControl.MoveFocus(New TraversalRequest(FocusNavigationDirection.Previous))
    End Sub

    Private Sub DrawingCanvas_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles zoomPanControl.MouseDown
        If mainCanvas.IsClickOnCanvasChild(e.OriginalSource) Then Return

        StartPos = e.GetPosition(mainCanvas)

        Dim isShiftPressed As Boolean = Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift)

        If SVGPageViewModel.CanvasToolMode <> CanvasMode.Selection Then
            SelectionManager.ClearSelection()
            SelectionManager.ApplySelection(Nothing, Document.Drawables)
        End If
    End Sub





    Private Sub SVGPageView_Unloaded(sender As Object, e As RoutedEventArgs)
        FinishImportPlacement(Nothing)
        SelectionManager.ApplySelection(Nothing, Document.Drawables)
        mainCanvas.RaiseEvent(New MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) With {
            .RoutedEvent = Mouse.MouseDownEvent,
            .Source = mainCanvas
        })
        SVGPageViewModel.CanvasToolMode = CanvasMode.Selection
    End Sub


    Private Sub TextStyleControl_DropDownClosed(sender As Object, e As EventArgs)
        GetTextEditor()?.RefocusActiveTextBox()
    End Sub

    Private Sub TextStyleCard_IsKeyboardFocusWithinChanged(sender As Object, e As DependencyPropertyChangedEventArgs)
        If Not CBool(e.NewValue) Then
            GetTextEditor()?.EvaluateTextCommit()
        End If
    End Sub

    Private Sub OnTextEditRequested(sender As Object, textBox As System.Windows.Controls.TextBox)
        If textBox Is Nothing Then Return
        SVGPageViewModel.CanvasToolMode = CanvasMode.Text

        SyncOverlayTo(textBox)

        zoomPanControl.DrawingManager.TextEditor.BeginEditingText(textBox, mainCanvas)

        Dim editor = GetTextEditor()
        If editor IsNot Nothing Then editor.SuppressStyleSourceApply = True
        Try
            SVGPageViewModel.CanvasTextBox.FontFamily = textBox.FontFamily
            SVGPageViewModel.CanvasTextBox.FontSize = TextGeometryHelper.GetEffectiveFontSize(textBox, mainCanvas)
            SVGPageViewModel.CanvasTextBox.FontStyle = textBox.FontStyle
            SVGPageViewModel.CanvasTextBox.FontWeight = textBox.FontWeight
        Finally
            If editor IsNot Nothing Then editor.SuppressStyleSourceApply = False
        End Try

        SelectTextDrawable(textBox)
    End Sub

    Private Sub OnTextEdited(sender As Object, textBox As Controls.TextBox, before As TextEditState, after As TextEditState)
        If textBox Is Nothing Then Return
        SVGPageViewModel.RecordTextEdit(textBox, before, after)
    End Sub

    Private Sub SelectTextDrawable(textBox As Controls.TextBox)

        If textBox Is Nothing Then Return
        Dim wrapper = TryCast(textBox.Parent, ContentControl)
        If wrapper Is Nothing Then Return
        Dim drawable = MetadataHelper.GetDrawableReference(wrapper)
        If drawable Is Nothing Then Return
        If Not SelectionManager.SelectedItems.Contains(drawable) Then
            SelectionManager.ClearSelection()
            SelectionManager.SelectItem(drawable, True)
        End If
    End Sub

    Private Sub SyncOverlayTo(textBox As System.Windows.Controls.TextBox)
        If textBox Is Nothing Then Return

        Dim editor = GetTextEditor()
        If editor IsNot Nothing Then editor.SuppressStyleSourceApply = True
        SVGPageViewModel.BeginOverlaySync()
        Try
            _overlayEffectiveFontSize = TextGeometryHelper.GetEffectiveFontSize(textBox, mainCanvas)
            Dim fontSize = _overlayEffectiveFontSize.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            SVGPageViewModel.CanvasFontFamily = textBox.FontFamily
            SVGPageViewModel.CanvasFontSize = fontSize
            SVGPageViewModel.CanvasFontWeightIsBold = textBox.FontWeight = FontWeights.Bold
            SVGPageViewModel.CanvasFontStyleIsItalic = textBox.FontStyle = FontStyles.Italic
            OverlayFontPicker.SelectedFont = textBox.FontFamily
            OverlayFontSizeComboBox.Text = fontSize
            OverlayBoldToggleButton.IsChecked = textBox.FontWeight = FontWeights.Bold
            OverlayItalicToggleButton.IsChecked = textBox.FontStyle = FontStyles.Italic
        Finally
            SVGPageViewModel.EndOverlaySync()
            If editor IsNot Nothing Then editor.SuppressStyleSourceApply = False
        End Try
    End Sub

    Private Function GetTextEditor() As TextEditingController
        Return zoomPanControl?.DrawingManager.TextEditor
    End Function


    ' ===== Canvas context menu =====

    Private _contextStrokeBeforeEditMap As Dictionary(Of IDrawable, Brush)
    Private _contextFillBeforeEditMap As Dictionary(Of IDrawable, Brush)

    Private Sub OnCanvasContextMenuOpening(sender As Object, e As ContextMenuEventArgs)
        ' A text box being edited owns its own context menu.
        If GetTextEditor()?.ActiveTextBox IsNot Nothing Then
            e.Handled = True
            Return
        End If

        UpdateCanvasContextMenuState()
    End Sub

    Private Sub OnCanvasContextMenuOpened(sender As Object, e As RoutedEventArgs)
        ' The gizmo opens the menu directly when a right-click lands on it, which bypasses
        ' ContextMenuOpening, so refresh the state here too.
        UpdateCanvasContextMenuState()
    End Sub

    Private Sub UpdateCanvasContextMenuState()
        Dim hasSelection = SelectionManager.HasSelection
        Dim hasMultiSelection = SelectionManager.HasMultipleSelection

        CombineMenuItem.Visibility = If(hasMultiSelection, Visibility.Visible, Visibility.Collapsed)
        StyleMenuItem.Visibility = If(hasSelection, Visibility.Visible, Visibility.Collapsed)
        ArrangeMenuItem.Visibility = If(hasSelection, Visibility.Visible, Visibility.Collapsed)
        CanvasContextMenuSeparator.Visibility = If(hasSelection, Visibility.Visible, Visibility.Collapsed)
        ZoomMenuItem.Visibility = If(hasSelection, Visibility.Collapsed, Visibility.Visible)
        GroupMenuItem.Visibility = If(hasMultiSelection, Visibility.Visible, Visibility.Collapsed)

        ContextCutButton.IsEnabled = hasSelection
        ContextCopyButton.IsEnabled = hasSelection
        ContextPasteButton.IsEnabled = Clipboard.ContainsData(ClipboardService.ClipFormat)

        GroupMenuItem.IsEnabled = hasSelection

        If hasSelection Then
            Dim selected = SelectionManager.SelectedItems.ToList()
            ContextFillPicker.IsEnabled = Not selected.All(Function(d) RegistrationMarkHelper.IsRegistrationMark(d))
            UngroupMenuItem.Visibility = If(selected.Any(Function(d) TypeOf d Is NestedDrawableGroup), Visibility.Visible, Visibility.Collapsed)
        End If

        CanvasContextMenuGroupingSeparator.Visibility = If(UngroupMenuItem.Visibility = Visibility.Visible OrElse GroupMenuItem.Visibility = Visibility.Visible, Visibility.Visible, Visibility.Collapsed)

    End Sub

    Private Sub ContextStrokePicker_PopupOpening(sender As Object, e As EventArgs)
        Dim leaves = ExpandSelectionToLeaves(SelectionManager.SelectedItems)
        _contextStrokeBeforeEditMap = leaves.ToDictionary(Function(d) d, Function(d) d.Stroke)
    End Sub

    Private Sub ContextStrokePicker_ColorSelected(sender As Object, e As ColorSelectedEventArgs)
        SVGPageViewModel.ApplyStroke(e.SelectedBrush, _contextStrokeBeforeEditMap)
        _contextStrokeBeforeEditMap = Nothing
    End Sub

    Private Sub ContextFillPicker_PopupOpening(sender As Object, e As EventArgs)
        Dim leaves = ExpandSelectionToLeaves(SelectionManager.SelectedItems)
        _contextFillBeforeEditMap = leaves.ToDictionary(Function(d) d, Function(d) d.Fill)
    End Sub

    Private Sub ContextFillPicker_ColorSelected(sender As Object, e As ColorSelectedEventArgs)
        SVGPageViewModel.ApplyFill(e.SelectedBrush, _contextFillBeforeEditMap)
        _contextFillBeforeEditMap = Nothing
    End Sub

    Private Shared Function ExpandSelectionToLeaves(items As IEnumerable(Of IDrawable)) As List(Of IDrawable)
        Dim result As New List(Of IDrawable)()
        If items Is Nothing Then Return result

        For Each d In items
            If d Is Nothing Then Continue For

            Dim ng = TryCast(d, NestedDrawableGroup)
            If ng IsNot Nothing Then
                result.AddRange(ng.GetAllLeafChildren())
            Else
                result.Add(d)
            End If
        Next

        Return result.Where(Function(x) x IsNot Nothing).Distinct().ToList()
    End Function

    Private Sub MenuReduceZoomButton_Click(sender As Object, e As RoutedEventArgs)
        zoomPanControl.ZoomByLinear(-0.2)
    End Sub

    Private Sub MenuResetZoomButton_Click(sender As Object, e As RoutedEventArgs)
        zoomPanControl.Reset()
    End Sub

    Private Sub MenuIncreaseZoomButton_Click(sender As Object, e As RoutedEventArgs)
        zoomPanControl.ZoomByLinear(0.2)
    End Sub

    Private Sub MenuZoomTo100Percent_Click(sender As Object, e As RoutedEventArgs)
        zoomPanControl.ZoomByLinear(2.0 - zoomPanControl.Scale)
    End Sub

    Private Sub ContextSelectAllButton_Click(sender As Object, e As RoutedEventArgs)
        MainViewModel.SelectAllCommand.Execute(Nothing)
        UpdateCanvasContextMenuState()
    End Sub

    Private Sub ContextCutButton_Click(sender As Object, e As RoutedEventArgs)
        'close the context menu first to avoid issues with the clipboard
        zoomPanControl.ContextMenu.IsOpen = False
    End Sub
End Class
