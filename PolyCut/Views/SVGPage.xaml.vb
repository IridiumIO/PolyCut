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
Class SVGPage

    Public ReadOnly Property MainViewModel As MainViewModel

    Public ReadOnly Property SVGPageViewModel As SVGPageViewModel

    Sub New(viewmodel As SVGPageViewModel)
        Me.SVGPageViewModel = viewmodel
        Me.MainViewModel = viewmodel.MainVM
        Me.DataContext = viewmodel

        InitializeComponent()
        zoomPanControl.Scale = 2
        zoomPanControl.TranslateTransform.X = -MainViewModel.Printer.BedWidth / 2
        zoomPanControl.TranslateTransform.Y = -MainViewModel.Printer.BedHeight / 2
        AddHandler MainViewModel.CuttingMat.PropertyChanged, AddressOf PropertyChangedHandler
        AddHandler MainViewModel.Printer.PropertyChanged, AddressOf PropertyChangedHandler
        AddHandler MainViewModel.Configuration.PropertyChanged, AddressOf PropertyChangedHandler
        'AddHandler viewmodel.PropertyChanged, AddressOf MainVMPropertyChangedHandler
        AddHandler zoomPanControl.DrawingManager.DrawingFinished, AddressOf DrawingFinishedHandler

        Transform()
    End Sub

    Private Sub DrawingFinishedHandler(sender As Object, shape As UIElement)
        If sender Is Nothing Then Return
        MainViewModel.AddDrawableElement(shape)
    End Sub

    Private Sub PropertyChangedHandler(sender As Object, e As PropertyChangedEventArgs)

        Dim alignmentPropertyNames = {
            NameOf(MainViewModel.CuttingMat.SelectedVerticalAlignment),
            NameOf(MainViewModel.CuttingMat.SelectedHorizontalAlignment),
            NameOf(MainViewModel.CuttingMat.SelectedRotation),
            NameOf(MainViewModel.CuttingMat)}

        If alignmentPropertyNames.Contains(e.PropertyName) Then
            Transform()
        End If
        MainViewModel.GCodePaths.Clear()
        MainViewModel.GCode = ""

    End Sub

    Private Sub Transform()
        Dim ret = CalculateOutputs(MainViewModel.CuttingMat.SelectedRotation, MainViewModel.CuttingMat.SelectedHorizontalAlignment, MainViewModel.CuttingMat.SelectedVerticalAlignment)


        CuttingMat_RenderTransform.X = ret.Item1
        CuttingMat_RenderTransform.Y = ret.Item2
    End Sub

    Shared Function CalculateOutputs(rotation As Integer, alignmentH As String, alignmentV As String) As Tuple(Of Double, Double)
        Dim x As Double = 0
        Dim y As Double = 0

        'TODO
        'Dim CuttingMatWidth = MainViewModel.CuttingMat.Width 
        'Dim CuttingMatHeight = MainViewModel.CuttingMat.Height

        Select Case rotation
            Case 0
            ' No rotation
            Case 90
                ' 90 degrees rotation
                Select Case alignmentV
                    Case "Top"
                        If alignmentH = "Left" Then
                            x = 355.6
                        ElseIf alignmentH = "Right" Then
                            x = 330.2
                        End If
                    Case "Bottom"
                        If alignmentH = "Left" Then
                            x = 355.6
                            y = 25.4
                        ElseIf alignmentH = "Right" Then
                            x = 330.2
                            y = 25.4
                        End If
                End Select
            Case 180
                ' 180 degrees rotation
                x = 330.2
                y = 355.6
            Case 270
                ' 270 degrees rotation
                Select Case alignmentV
                    Case "Top"
                        If alignmentH = "Left" Then
                            y = 330.2
                        ElseIf alignmentH = "Right" Then
                            x = -25.4
                            y = 330.2
                        End If
                    Case "Bottom"
                        If alignmentH = "Left" Then
                            y = 355.6
                        ElseIf alignmentH = "Right" Then
                            x = -25.4
                            y = 355.6
                        End If
                End Select
        End Select

        Return Tuple.Create(x, y)
    End Function



    Dim translation As Point

    Private Sub HoverAlignment(sender As Object, e As MouseEventArgs) Handles cuttingMat_AlignmentBoxes.MouseEnter, cuttingMat_AlignmentBoxes.MouseLeave

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
        If MainViewModel.CanvasToolMode <> CanvasMode.Selection Then
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
        MainViewModel.CanvasToolMode = CanvasMode.Selection
    End Sub

    Private Sub NumberBox_LostFocus(sender As Object, e As RoutedEventArgs)
        'Need to explicitly call the `Enter` keypress as pressing `Tab` doesn't commit the new number before switching focus

        Dim numberBox As WPF.Ui.Controls.NumberBox = TryCast(sender, WPF.Ui.Controls.NumberBox)
        If numberBox IsNot Nothing Then

            numberBox.RaiseEvent(New KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(numberBox), 0, Key.Enter) With {
            .RoutedEvent = Keyboard.KeyDownEvent
        })


            Dim bindingExpression = numberBox.GetBindingExpression(WPF.Ui.Controls.NumberBox.ValueProperty)
            bindingExpression?.UpdateSource()
        End If
    End Sub
End Class
