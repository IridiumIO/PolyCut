Imports System.ComponentModel
Imports System.Data.SqlTypes
Imports System.Xml
Imports WPF.Ui.Controls
Imports SharpVectors
Imports System.Windows.Media.Animation
Imports System.IO
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
        AddHandler PolyCanvas.SelectionCountChanged, AddressOf OnSelectionCountChanged

    End Sub

    Private Sub OnSelectionCountChanged(sender As Object, e As EventArgs)

        MainSidebar.ElementsTab.SyncListViewSelection(PolyCanvas.SelectedItems)
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
            Debug.WriteLine("MainView_MouseDown: TextBox is focused - NOT moving focus")
            Return
        End If

        zoomPanControl.MoveFocus(New TraversalRequest(FocusNavigationDirection.Previous))
    End Sub

    Private Sub DrawingCanvas_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles zoomPanControl.MouseDown
        Dim originalSource = TryCast(e.OriginalSource, DependencyObject)
        If originalSource IsNot Nothing Then

            Dim current = originalSource
            While current IsNot Nothing

                If TypeOf current Is ContentControl Then
                    Dim wrapper = CType(current, ContentControl)

                    Dim visualParent = VisualTreeHelper.GetParent(wrapper)

                    If visualParent Is mainCanvas Then
                        Dim textBox = FindTextBoxInWrapper(wrapper)

                        If textBox IsNot Nothing Then
                            If textBox.IsFocused OrElse textBox.IsKeyboardFocusWithin Then
                                Exit While
                            End If
                        End If

                        Return
                    End If
                End If

                If current Is mainCanvas Then
                    Exit While
                End If

                current = VisualTreeHelper.GetParent(current)
            End While
        End If
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

    Private Function FindVisualChild(Of T As Visual)(parent As DependencyObject) As T
        If parent Is Nothing Then Return Nothing

        For i As Integer = 0 To VisualTreeHelper.GetChildrenCount(parent) - 1
            Dim child = VisualTreeHelper.GetChild(parent, i)
            Dim result = TryCast(child, T)
            If result IsNot Nothing Then
                Return result
            End If

            result = FindVisualChild(Of T)(child)
            If result IsNot Nothing Then
                Return result
            End If
        Next

        Return Nothing
    End Function

    Private Function FindTextBoxInWrapper(wrapper As ContentControl) As System.Windows.Controls.TextBox

        If TypeOf wrapper.Content Is System.Windows.Controls.TextBox Then
            Return CType(wrapper.Content, System.Windows.Controls.TextBox)
        End If

        Dim result = FindVisualChild(Of System.Windows.Controls.TextBox)(wrapper)
        Return result
    End Function

    Private Sub zoomPanControl_PreviewMouseDown(sender As Object, e As MouseButtonEventArgs)
        Dim rx = sender.GetHashCode
        Debug.WriteLine($"zoomPanControl_PreviewMouseDown: Sender HashCode={rx}")
    End Sub
End Class
