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
    Private _subscribedPrinterCuttingMat As CuttingMat


    Sub New(viewmodel As SVGPageViewModel)
        Me.SVGPageViewModel = viewmodel
        Me.MainViewModel = viewmodel.MainVM
        Me.DataContext = viewmodel

        InitializeComponent()
        zoomPanControl.Scale = 2
        zoomPanControl.TranslateTransform.X = -MainViewModel.Printer.BedWidth / 2
        zoomPanControl.TranslateTransform.Y = -MainViewModel.Printer.BedHeight / 2
        AddHandler MainViewModel.PropertyChanged, AddressOf MainViewModel_PropertyChanged

        AddHandler MainViewModel.PrinterConfigOpened, Sub()

                                                          Dim opacityAnimation As New DoubleAnimation(0.5, New Duration(TimeSpan.FromSeconds(0.3)))

                                                          DupCuttingMatBounds.BeginAnimation(UIElement.OpacityProperty, opacityAnimation)
                                                      End Sub

        AddHandler MainViewModel.PrinterConfigClosed, Sub()
                                                          ' Stop any existing animations and set final state when config is saved
                                                          Dim op = DupCuttingMatBounds.Opacity
                                                          DupCuttingMatBounds.BeginAnimation(UIElement.OpacityProperty, Nothing)
                                                          DupCuttingMatBounds.Opacity = op
                                                          ' Opacity animation for config saved
                                                          Dim opacityAnimation As New DoubleAnimation(0, TimeSpan.FromSeconds(1)) With {
                                                                           .EasingFunction = New ExponentialEase() With {.EasingMode = EasingMode.EaseIn, .Exponent = 4}
                                                                       }
                                                          DupCuttingMatBounds.BeginAnimation(UIElement.OpacityProperty, opacityAnimation)
                                                      End Sub

        SubscribeToPrinter(MainViewModel.Printer)

        AddHandler MainViewModel.Configuration.PropertyChanged, AddressOf PropertyChangedHandler
        AddHandler zoomPanControl.DrawingManager.DrawingFinished, AddressOf DrawingFinishedHandler
        AddHandler MainSidebar.CuttingMatAlignmentMouseEnter, AddressOf HoverAlignment
        AddHandler MainSidebar.CuttingMatAlignmentMouseLeave, AddressOf HoverAlignment
        AddHandler DesignerItemDecorator.CurrentSelectedChanged, AddressOf OnDesignerItemDecoratorCurrentSelectedChanged
        Transform()
    End Sub

    Private Sub MainViewModel_PropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        If e Is Nothing Then Return

        ' When MainViewModel.Printer reference changes, re-subscribe to the new instance
        If String.Equals(e.PropertyName, NameOf(MainViewModel.Printer), StringComparison.OrdinalIgnoreCase) Then
            SubscribeToPrinter(MainViewModel.Printer)
            Transform()
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

        ' Subscribe to the cutting mat on the printer (if present) and manage changes
        SubscribeToPrinterCuttingMat(If(pr IsNot Nothing, pr.CuttingMat, Nothing))
    End Sub

    Private Sub SubscribeToPrinterCuttingMat(mat As CuttingMat)
        If _subscribedPrinterCuttingMat IsNot Nothing Then
            RemoveHandler _subscribedPrinterCuttingMat.PropertyChanged, AddressOf PropertyChangedHandler
        End If

        _subscribedPrinterCuttingMat = mat

        If _subscribedPrinterCuttingMat IsNot Nothing Then
            AddHandler _subscribedPrinterCuttingMat.PropertyChanged, AddressOf PropertyChangedHandler
        End If
    End Sub

    Private Sub OnDesignerItemDecoratorCurrentSelectedChanged(sender As Object, e As EventArgs)
        Dim isShiftPressed As Boolean = Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift)
        Dim currentSelected = DesignerItemDecorator.CurrentSelected

        If currentSelected IsNot Nothing Then
            Dim selectedDrawable As IDrawable = Nothing

            For Each drawable In MainViewModel.DrawableCollection
                If drawable.DrawableElement IsNot Nothing Then
                    Dim parent = TryCast(drawable.DrawableElement.Parent, ContentControl)
                    If parent IsNot Nothing AndAlso parent Is currentSelected Then
                        selectedDrawable = drawable
                        Exit For
                    End If
                End If
            Next

            If selectedDrawable IsNot Nothing Then
                If TypeOf selectedDrawable Is DrawableGroup Then Return

                If isShiftPressed Then
                    PolyCanvas.ToggleSelection(selectedDrawable)
                Else
                    PolyCanvas.ClearSelection()
                    PolyCanvas.AddToSelection(selectedDrawable)
                End If

                SelectionHelper.SyncSelectionStates(MainViewModel.DrawableCollection)
                MainSidebar.ElementsTab.SyncListViewSelection(PolyCanvas.SelectedItems)

                MainViewModel.NotifyPropertyChanged(NameOf(MainViewModel.SelectedDrawable))
                MainViewModel.NotifyPropertyChanged(NameOf(MainViewModel.SelectedDrawables))
                MainViewModel.NotifyPropertyChanged(NameOf(MainViewModel.HasMultipleSelected))
            End If
        ElseIf Not isShiftPressed Then
            PolyCanvas.ClearSelection()
            SelectionHelper.SyncSelectionStates(MainViewModel.DrawableCollection)
            MainSidebar.ElementsTab.SyncListViewSelection(PolyCanvas.SelectedItems)

            MainViewModel.NotifyPropertyChanged(NameOf(MainViewModel.SelectedDrawable))
            MainViewModel.NotifyPropertyChanged(NameOf(MainViewModel.SelectedDrawables))
            MainViewModel.NotifyPropertyChanged(NameOf(MainViewModel.HasMultipleSelected))
        End If

        SVGPageViewModel.OnDesignerItemDecoratorCurrentSelectedChanged(currentSelected)
    End Sub

    Private Sub DrawingFinishedHandler(sender As Object, shape As UIElement)
        If sender Is Nothing Then Return
        MainViewModel.AddDrawableElement(shape)
    End Sub

    Private Sub PropertyChangedHandler(sender As Object, e As PropertyChangedEventArgs)

        Dim prop = If(e?.PropertyName, "")

        If prop.IndexOf("CuttingMat", StringComparison.OrdinalIgnoreCase) >= 0 _
           OrElse prop.IndexOf("Rotation", StringComparison.OrdinalIgnoreCase) >= 0 _
           OrElse prop.IndexOf("Alignment", StringComparison.OrdinalIgnoreCase) >= 0 _
           OrElse String.Equals(prop, NameOf(MainViewModel.Printer), StringComparison.OrdinalIgnoreCase) Then

            ' If the printer's CuttingMat reference changed, resubscribe its events
            If TypeOf sender Is Printer Then
                Dim p = TryCast(sender, Printer)
                If p IsNot Nothing Then
                    SubscribeToPrinterCuttingMat(p.CuttingMat)
                End If
            End If

            Transform()
        End If

        MainViewModel.GCodePaths.Clear()
        MainViewModel.GCode = ""

    End Sub

    Private Sub Transform()
        Dim ret = CalculateOutputs(MainViewModel.Printer.CuttingMatRotation, MainViewModel.Printer.CuttingMatHorizontalAlignment, MainViewModel.Printer.CuttingMatVerticalAlignment)


        CuttingMat_RenderTransform.X = ret.Item1
        CuttingMat_RenderTransform.Y = ret.Item2
    End Sub

    Function CalculateOutputs(rotation As Integer, alignmentH As String, alignmentV As String) As Tuple(Of Double, Double)
        Dim x As Double = 0
        Dim y As Double = 0

        'TODO
        Dim CuttingMatWidth = MainViewModel.Printer.CuttingMat.Width
        Dim CuttingMatHeight = MainViewModel.Printer.CuttingMat.Height

        Select Case rotation
            Case 0
            ' No rotation
            Case 90
                ' 90 degrees rotation
                Select Case alignmentV
                    Case "Top"
                        If alignmentH = "Left" Then
                            x = CuttingMatHeight
                        ElseIf alignmentH = "Right" Then
                            x = CuttingMatWidth
                        End If
                    Case "Bottom"
                        If alignmentH = "Left" Then
                            x = CuttingMatHeight
                            y = 25.4
                        ElseIf alignmentH = "Right" Then
                            x = CuttingMatWidth
                            y = 25.4
                        End If
                End Select
            Case 180
                ' 180 degrees rotation
                x = CuttingMatWidth
                y = CuttingMatHeight
            Case 270
                ' 270 degrees rotation
                Select Case alignmentV
                    Case "Top"
                        If alignmentH = "Left" Then
                            y = CuttingMatWidth
                        ElseIf alignmentH = "Right" Then
                            x = -25.4
                            y = CuttingMatWidth
                        End If
                    Case "Bottom"
                        If alignmentH = "Left" Then
                            y = CuttingMatHeight
                        ElseIf alignmentH = "Right" Then
                            x = -25.4
                            y = CuttingMatHeight
                        End If
                End Select
        End Select

        Return Tuple.Create(x, y)
    End Function



    Dim translation As Point

    Private Sub HoverAlignment(sender As Object, e As MouseEventArgs)

        If e.RoutedEvent Is MouseEnterEvent Then

            Dim opacityAnimation As New DoubleAnimation(0.5, New Duration(TimeSpan.FromSeconds(0.3)))

            DupCuttingMatBounds.BeginAnimation(UIElement.OpacityProperty, opacityAnimation)



        Else
            ' Stop any existing animations and set final state when MouseLeave
            Dim op = DupCuttingMatBounds.Opacity
            DupCuttingMatBounds.BeginAnimation(UIElement.OpacityProperty, Nothing)
            DupCuttingMatBounds.Opacity = op

            ' Opacity animation for MouseLeave
            Dim opacityAnimation As New DoubleAnimation(0, TimeSpan.FromSeconds(1)) With {
                .EasingFunction = New ExponentialEase() With {.EasingMode = EasingMode.EaseIn, .Exponent = 4}
            }
            DupCuttingMatBounds.BeginAnimation(UIElement.OpacityProperty, opacityAnimation)
        End If

    End Sub



    Private Sub Page_Drop(sender As Object, e As DragEventArgs)
        If Not e.Data.GetDataPresent(DataFormats.FileDrop) Then Return
        SVGPageViewModel.ProcessDroppedFiles(TryCast(e.Data.GetData(DataFormats.FileDrop), String()))
    End Sub



    Private StartPos As Point

    Private Sub MainView_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles zoomPanControl.MouseDown

        zoomPanControl.MoveFocus(New TraversalRequest(FocusNavigationDirection.Previous))

    End Sub

    Private Sub DrawingCanvas_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles zoomPanControl.MouseDown
        StartPos = e.GetPosition(mainCanvas)

        Debug.WriteLine(MainViewModel.DrawableCollection.Count)

        ' Check multi-select
        Dim isShiftPressed As Boolean = Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift)

        If SVGPageViewModel.CanvasToolMode <> CanvasMode.Selection Then
            ' If not in selection mode, deselect all items
            PolyCanvas.ClearSelection()
            For Each child In MainViewModel.DrawableCollection
                If TypeOf child.DrawableElement.Parent Is ContentControl Then
                    child.IsSelected = False
                End If
            Next
        ElseIf Not isShiftPressed Then
            ' If in selection mode but SHIFT not pressed, this will be handled by PolyCanvas_MouseDown
            ' which will clear selection if clicking on canvas background
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

End Class
