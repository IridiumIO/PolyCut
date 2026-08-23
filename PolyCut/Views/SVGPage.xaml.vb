Imports System.ComponentModel
Imports System.Data.SqlTypes
Imports System.Xml
Imports WPF.Ui.Controls
Imports SharpVectors
Imports System.Windows.Media.Animation
Imports System.IO
Imports System.Linq
Imports CommunityToolkit.Mvvm.ComponentModel
Imports Svg
Imports System.Windows.Controls.Primitives
Imports PolyCut.Shared
Imports WPF
Imports PolyCut.RichCanvas
Class SVGPage

    Public ReadOnly Property MainViewModel As MainViewModel
    Public ReadOnly Property SVGPageViewModel As SVGPageViewModel

    Private _subscribedPrinter As Printer

    Sub New(viewmodel As SVGPageViewModel)
        Me.SVGPageViewModel = viewmodel
        Me.MainViewModel = viewmodel.MainVM
        Me.DataContext = viewmodel

        InitializeComponent()
        zoomPanControl.Scale = 2
        zoomPanControl.TranslateTransform.X = -MainViewModel.Printer.BedWidth / 2
        zoomPanControl.TranslateTransform.Y = -MainViewModel.Printer.BedHeight / 2
        AddHandler MainViewModel.PropertyChanged, AddressOf MainViewModel_PropertyChanged


        SubscribeToPrinter(MainViewModel.Printer)

        AddHandler MainViewModel.Configuration.PropertyChanged, AddressOf PropertyChangedHandler
        AddHandler zoomPanControl.DrawingManager.DrawingFinished, AddressOf DrawingFinishedHandler
        AddHandler zoomPanControl.DrawingManager.TextEditor.EditRequested, AddressOf OnTextEditRequested
        AddHandler zoomPanControl.DrawingManager.TextEditor.TextEdited, AddressOf OnTextEdited
        AddHandler zoomPanControl.ContextMenuOpening, AddressOf OnCanvasContextMenuOpening
        AddHandler zoomPanControl.ContextMenu.Opened, AddressOf OnCanvasContextMenuOpened
        AddHandler PolyCanvas.SelectionCountChanged, AddressOf OnSelectionCountChanged

    End Sub

    Private Sub OnSelectionCountChanged(sender As Object, e As EventArgs)

        MainSidebar.ElementsTab.SyncListViewSelection(PolyCanvas.SelectedItems)
        SyncTextStyleOverlayToSelection()
    End Sub

    Private Sub SyncTextStyleOverlayToSelection()
        Dim selected = PolyCanvas.SelectedItems
        If selected Is Nothing OrElse selected.Count = 0 Then Return
        Dim drawable = selected.FirstOrDefault()
        If drawable Is Nothing Then Return
        Dim textBox = TryCast(drawable.DrawableElement, System.Windows.Controls.TextBox)
        If textBox IsNot Nothing Then
            SyncOverlayTo(textBox)
        End If
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
        If Not e.Data.GetDataPresent(DataFormats.FileDrop) Then Return
        SVGPageViewModel.ProcessDroppedFiles(TryCast(e.Data.GetData(DataFormats.FileDrop), String()))
    End Sub



    Private StartPos As Point

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
            PolyCanvas.ClearSelection()
            For Each child In MainViewModel.DrawableCollection
                If TypeOf child.DrawableElement.Parent Is ContentControl Then
                    child.IsSelected = False
                End If
            Next
        End If
    End Sub





    Private Sub SVGPageView_Unloaded(sender As Object, e As RoutedEventArgs)
        For Each child In MainViewModel.DrawableCollection
            child.IsSelected = False

        Next
        mainCanvas.RaiseEvent(New MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) With {
            .RoutedEvent = Mouse.MouseDownEvent,
            .Source = mainCanvas
        })
        SVGPageViewModel.CanvasToolMode = CanvasMode.Selection
    End Sub

    Private Sub zoomPanControl_PreviewMouseDown(sender As Object, e As MouseButtonEventArgs)
        Dim rx = sender.GetHashCode
        Debug.WriteLine($"zoomPanControl_PreviewMouseDown: Sender HashCode={rx}")
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
            SVGPageViewModel.CanvasTextBox.FontSize = textBox.FontSize
            SVGPageViewModel.CanvasTextBox.FontStyle = textBox.FontStyle
            SVGPageViewModel.CanvasTextBox.FontWeight = textBox.FontWeight
        Finally
            If editor IsNot Nothing Then editor.SuppressStyleSourceApply = False
        End Try

        SelectTextDrawable(textBox)
    End Sub

    Private Sub OnTextEdited(sender As Object, textBox As System.Windows.Controls.TextBox, oldText As String, oldChars As TextCharacteristics, newText As String, newChars As TextCharacteristics)
        If textBox Is Nothing Then Return
        SVGPageViewModel.RecordTextEdit(textBox, oldText, oldChars, newText, newChars)
    End Sub

    Private Sub SelectTextDrawable(textBox As System.Windows.Controls.TextBox)

        If textBox Is Nothing Then Return
        Dim wrapper = TryCast(textBox.Parent, ContentControl)
        If wrapper Is Nothing Then Return
        Dim drawable = MetadataHelper.GetDrawableReference(wrapper)
        If drawable Is Nothing Then Return
        If Not PolyCanvas.SelectedItems.Contains(drawable) Then
            PolyCanvas.ClearSelection()
            PolyCanvas.AddToSelection(drawable)
        End If
    End Sub

    Private Sub SyncOverlayTo(textBox As System.Windows.Controls.TextBox)
        If textBox Is Nothing Then Return

        Dim editor = GetTextEditor()
        If editor IsNot Nothing Then editor.SuppressStyleSourceApply = True
        SVGPageViewModel.BeginOverlaySync()
        Try
            SVGPageViewModel.CanvasFontFamily = textBox.FontFamily
            SVGPageViewModel.CanvasFontSize = textBox.FontSize.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
            SVGPageViewModel.CanvasFontWeightIsBold = textBox.FontWeight = FontWeights.Bold
            SVGPageViewModel.CanvasFontStyleIsItalic = textBox.FontStyle = FontStyles.Italic
            OverlayFontPicker.SelectedFont = textBox.FontFamily
            OverlayFontSizeComboBox.Text = textBox.FontSize.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
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
        Dim hasSelection = MainViewModel.HasSelection
        Dim hasMultiSelection = MainViewModel.HasMultipleSelected

        CombineMenuItem.Visibility = If(hasMultiSelection, Visibility.Visible, Visibility.Collapsed)
        StyleMenuItem.Visibility = If(hasSelection, Visibility.Visible, Visibility.Collapsed)
        ArrangeMenuItem.Visibility = If(hasSelection, Visibility.Visible, Visibility.Collapsed)
        CanvasContextMenuSeparator.Visibility = If(hasSelection, Visibility.Visible, Visibility.Collapsed)
        ZoomMenuItem.Visibility = If(hasSelection, Visibility.Collapsed, Visibility.Visible)

        ContextCutButton.IsEnabled = hasSelection
        ContextCopyButton.IsEnabled = hasSelection
        ContextPasteButton.IsEnabled = Clipboard.ContainsData(ClipboardService.ClipFormat)

        If hasSelection Then
            Dim selected = MainViewModel.SelectedDrawables.ToList()
            ContextFillPicker.IsEnabled = Not selected.All(Function(d) RegistrationMarkHelper.IsRegistrationMark(d))
        End If
    End Sub

    Private Sub ContextStrokePicker_PopupOpening(sender As Object, e As EventArgs)
        Dim leaves = ExpandSelectionToLeaves(MainViewModel.SelectedDrawables)
        _contextStrokeBeforeEditMap = leaves.ToDictionary(Function(d) d, Function(d) d.Stroke)
    End Sub

    Private Sub ContextStrokePicker_ColorSelected(sender As Object, e As ColorSelectedEventArgs)
        SVGPageViewModel.ApplyStroke(e.SelectedBrush, _contextStrokeBeforeEditMap)
        _contextStrokeBeforeEditMap = Nothing
    End Sub

    Private Sub ContextFillPicker_PopupOpening(sender As Object, e As EventArgs)
        Dim leaves = ExpandSelectionToLeaves(MainViewModel.SelectedDrawables)
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
