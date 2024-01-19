Imports System.ComponentModel
Imports System.Data.SqlTypes
Imports System.Xml
Imports Wpf.Ui.Controls
Imports SharpVectors
Imports System.Windows.Media.Animation
Imports System.IO
Class SVGPage : Implements INavigableView(Of MainViewModel)
    Protected Sub OnNavigatedTo(e As NavigationEventArgs)

        Me.DataContext = e.ExtraData

    End Sub

    Sub New(viewmodel As MainViewModel)

        Me.ViewModel = viewmodel
        DataContext = viewmodel

        InitializeComponent()
        zoomPanControl.Scale = 2
        zoomPanControl._translateTransform.X = -viewmodel.Printer.BedWidth / 2
        zoomPanControl._translateTransform.Y = -viewmodel.Printer.BedHeight / 2
        AddHandler viewmodel.CuttingMat.PropertyChanged, AddressOf PropertyChangedHandler
        AddHandler viewmodel.Printer.PropertyChanged, AddressOf PropertyChangedHandler
        AddHandler viewmodel.Configuration.PropertyChanged, AddressOf PropertyChangedHandler
        AddHandler viewmodel.PropertyChanged, AddressOf MainVMPropertyChangedHandler


        Transform()
    End Sub



    Private Sub MainVMPropertyChangedHandler(sender As Object, e As PropertyChangedEventArgs)
        'Handle SVGComponents being updated
        If e.PropertyName = NameOf(ViewModel.SVGComponents) Then
            svgDrawing.Children.Clear()
            For Each cl In ViewModel.SVGComponents
                If cl.IsVisualElement Then
                    cl.SetCanvas()
                    svgDrawing.Children.Add(cl.ECanvas)
                    cl.ECanvas.SubscribeToZoomBorderScaling(zoomPanControl)

                End If
            Next
        End If
    End Sub

    Private Sub PropertyChangedHandler(sender As Object, e As PropertyChangedEventArgs)

        Dim alignmentPropertyNames = {
            NameOf(ViewModel.CuttingMat.SelectedVerticalAlignment),
            NameOf(ViewModel.CuttingMat.SelectedHorizontalAlignment),
            NameOf(ViewModel.CuttingMat.SelectedRotation),
            NameOf(ViewModel.CuttingMat)}


        If alignmentPropertyNames.Contains(e.PropertyName) Then
            Transform()
        End If
        ViewModel.GCodePaths.Clear()
        ViewModel.GCode = ""

    End Sub

    Private Sub Transform()
        Dim ret = CalculateOutputs(ViewModel.CuttingMat.SelectedRotation, ViewModel.CuttingMat.SelectedHorizontalAlignment, ViewModel.CuttingMat.SelectedVerticalAlignment)


        CuttingMat_RenderTransform.X = ret.Item1
        CuttingMat_RenderTransform.Y = ret.Item2
    End Sub

    Function CalculateOutputs(rotation As Integer, alignmentH As String, alignmentV As String) As Tuple(Of Double, Double)
        Dim x As Double = 0
        Dim y As Double = 0

        Dim CuttingMatWidth = ViewModel.CuttingMat.Width
        Dim CuttingMatHeight = ViewModel.CuttingMat.Height

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
    Public ReadOnly Property ViewModel As MainViewModel Implements INavigableView(Of MainViewModel).ViewModel


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
            Dim opacityAnimation As New DoubleAnimation(0, TimeSpan.FromSeconds(1))
            opacityAnimation.EasingFunction = New ExponentialEase() With {.EasingMode = EasingMode.EaseIn, .Exponent = 4}
            DupCuttingMatBounds.BeginAnimation(UIElement.OpacityProperty, opacityAnimation)
        End If

    End Sub



    Private Sub svgElementContextMenu(sender As Object, e As MouseButtonEventArgs) Handles mainCanvas.PreviewMouseRightButtonUp

        If TypeOf (e.Source) Is resizableSVGCanvas AndAlso e.Source.Parent Is svgDrawing Then
            e.Handled = True

        End If
    End Sub




    Private Sub BoundsCheck()

        If DoControlsOverlap(SVGFileBox, mainCanvas) Then
            If DirectCast(SVGFileBox.Background, SolidColorBrush).Color.A = 255 Then Return

            SVGFileBox.Background = New SolidColorBrush(Color.FromArgb(255, 48, 54, 62))
        Else
            If DirectCast(SVGFileBox.Background, SolidColorBrush).Color = Color.FromArgb(20, 255, 255, 255) Then Return
            SVGFileBox.Background = New SolidColorBrush(Color.FromArgb(20, 255, 255, 255))

        End If

    End Sub

    Private Sub ZBMouseMove(sender As Object, e As MouseEventArgs) Handles zoomPanControl.MouseMove
        If Not e.MiddleButton = MouseButtonState.Pressed Then Return
        BoundsCheck()
    End Sub
    Private Sub ZBMouseWheel(sender As Object, e As EventArgs) Handles zoomPanControl.ScaleChanged
        If Not Me.IsLoaded Then
            Return
        End If
        BoundsCheck()
    End Sub

    Function DoControlsOverlap(control1 As FrameworkElement, control2 As FrameworkElement) As Boolean
        Dim rect1 As Rect = GetControlBounds(control1)
        Dim rect2 As Rect = GetControlBounds(control2)

        Return rect1.IntersectsWith(rect2)
    End Function

    ' Get the bounding box of a control in screen coordinates
    Function GetControlBounds(control As FrameworkElement) As Rect
        Dim transformToDevice As Matrix = PresentationSource.FromVisual(control).CompositionTarget.TransformToDevice
        Dim topLeft As Point = control.PointToScreen(New Point(0, 0))
        Dim bottomRight As Point = control.PointToScreen(New Point(control.ActualWidth, control.ActualHeight))

        ' Convert points to device-independent pixels
        topLeft = transformToDevice.Transform(topLeft)
        bottomRight = transformToDevice.Transform(bottomRight)

        ' Create a Rect using the screen coordinates
        Return New Rect(topLeft, bottomRight)
    End Function



End Class
