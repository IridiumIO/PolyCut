Imports System.ComponentModel
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Windows.Threading

Imports WPF.Ui.Abstractions.Controls

Class PreviewPage : Implements INavigableView(Of MainViewModel)

    Public ReadOnly Property ViewModel As MainViewModel Implements INavigableView(Of MainViewModel).ViewModel
    Private cancellationTokenSource As CancellationTokenSource = New CancellationTokenSource

    Private _subscribedPrinter As Printer
    Private _subscribedPrinterCuttingMat As CuttingMat

    Sub New(viewmodel As MainViewModel)

        Me.ViewModel = viewmodel
        DataContext = viewmodel
        InitializeComponent()
        zoomPanControl.Scale = 2
        zoomPanControl.TranslateTransform.X = -viewmodel.Printer.BedWidth / 2
        zoomPanControl.TranslateTransform.Y = -viewmodel.Printer.BedHeight / 2
        InitializeDrawingVisual()




        ' Subscribe main VM property changes to the correct handler that knows how to re-subscribe printers
        AddHandler viewmodel.PropertyChanged, AddressOf MainViewModel_PropertyChanged
        AddHandler viewmodel.PropertyChanged, AddressOf PropertyChangedHandler
        ' Subscribe to the currently selected printer (sets up Printer.PropertyChanged -> PropertyChangedHandler)
        SubscribeToPrinter(viewmodel.Printer)

        If viewmodel.GCode?.Length <> 0 Then
            cancellationTokenSource.Cancel()
            viewmodel.GCodePaths.Clear()
            DrawToolPaths()
        End If

    End Sub

    Private Sub MainViewModel_PropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        If e Is Nothing Then Return

        ' When MainViewModel.Printer reference changes, re-subscribe to the new instance
        If String.Equals(e.PropertyName, NameOf(ViewModel.Printer), StringComparison.OrdinalIgnoreCase) Then
            SubscribeToPrinter(ViewModel.Printer)
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


        If e.PropertyName = NameOf(ViewModel.GCode) Then
            cancellationTokenSource.Cancel()
            ViewModel.GCodePaths.Clear()
            DrawToolPaths()

        End If


    End Sub

    Private Sub Transform()
        Dim ret = CalculateOutputs(ViewModel.Printer.CuttingMatRotation, ViewModel.Printer.CuttingMatHorizontalAlignment, ViewModel.Printer.CuttingMatVerticalAlignment)

        CuttingMat_RenderTransform.X = ret.Item1
        CuttingMat_RenderTransform.Y = ret.Item2
    End Sub

    Function CalculateOutputs(rotation As Integer, alignmentH As String, alignmentV As String) As Tuple(Of Double, Double)
        Dim x As Double = 0
        Dim y As Double = 0

        Dim CuttingMatWidth = ViewModel.Printer.CuttingMat.Width
        Dim CuttingMatHeight = ViewModel.Printer.CuttingMat.Height

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



    Private ReadOnly regexG01 As New Regex("G01.*?X([\d.]+).*?Y([\d.]+)")
    Private ReadOnly regexG00 As New Regex("G00.*?X([\d.]+).*?Y([\d.]+)")

    Private isRendering As Boolean = False

    Private Async Sub PreviewToolpath(sender As Object, e As RoutedEventArgs)

        Await cancellationTokenSource.CancelAsync()

        cancellationTokenSource = New CancellationTokenSource
        Dim ret = Await PreviewToolpaths(cancellationTokenSource.Token)

        If ret <> -1 Then
            isRendering = False
        End If

    End Sub

    Private Sub StopPreviewToolpath(sender As Object, e As RoutedEventArgs)
        cancellationTokenSource.Cancel()
        isRendering = False
        ' Clear the visuals
        visualHost.ClearVisuals()
        travelMoveVisuals.Clear()
        DrawToolPaths()
        cancellationTokenSource = New CancellationTokenSource
    End Sub


    Private travelMoveVisuals As New List(Of DrawingVisual)()

    Private Function DrawToolPaths()
        ' Clear existing visuals in the VisualHost
        visualHost.ClearVisuals()
        travelMoveVisuals.Clear()

        ' Compile the GCode into paths
        If ViewModel.GCode Is Nothing Then Return 1
        Dim gc = New GCodeGeometry(ViewModel.GCode)

        For Each line In gc.Paths
            ' Create a new DrawingVisual for the line
            Dim lineVisual As New DrawingVisual()
            Using dc As DrawingContext = lineVisual.RenderOpen()
                Dim pen As New Pen(line.Stroke, line.StrokeThickness)
                pen.StartLineCap = PenLineCap.Round
                pen.EndLineCap = PenLineCap.Round
                dc.DrawLine(pen, New Point(line.X1, line.Y1), New Point(line.X2, line.Y2))
            End Using

            ' Add the visual to the VisualHost
            visualHost.AddVisual(lineVisual)

            ' Handle travel lines
            If line.Stroke Is Brushes.OrangeRed Then
                ' Add to travel move visuals
                travelMoveVisuals.Add(lineVisual)

                ' Set initial visibility based on the toggle state
                If Not TravelMovesVisibilityToggle.IsChecked Then
                    lineVisual.Opacity = 0 ' Hide the travel line
                End If
            End If
        Next

        Debug.WriteLine($"Total segments: {visualHost.ChildrenCount}")

        Return 0
    End Function



    Dim isDragging As Boolean = False
    Dim translation As Point

    Private Sub TravelMovesVisibilityToggle_Checked(sender As Object, e As RoutedEventArgs) Handles TravelMovesVisibilityToggle.Checked, TravelMovesVisibilityToggle.Unchecked
        If TravelMovesVisibilityToggle.IsChecked Then
            For Each visual In travelMoveVisuals
                visual.Opacity = 1
            Next
        Else
            For Each visual In travelMoveVisuals
                visual.Opacity = 0
            Next
        End If
    End Sub


    Private Sub InitializeDrawingVisual()
        visualHost.ClearVisuals()
        Canvas.SetLeft(visualHost, 0)
        Canvas.SetTop(visualHost, 0)
    End Sub


    Private Async Function PreviewToolpaths(cToken As CancellationToken) As Task(Of Integer)
        ' Clear existing visuals
        visualHost.ClearVisuals()
        travelMoveVisuals.Clear()

        If ViewModel.GCodeGeometry Is Nothing Then Return 1

        ' Accumulated delay time
        Dim accumulatedDelay As Single = 0
        Dim stopwatch As New Stopwatch()

        For Each line In ViewModel.GCodeGeometry.Paths
            If cToken.IsCancellationRequested Then Return 1

            Dim startPoint As New Point(line.X1, line.Y1)
            Dim endPoint As New Point(line.X2, line.Y2)

            Dim isTravelMove As Boolean = (line.Stroke Is Brushes.OrangeRed)
            Dim hasTravelMoveBeenAdded As Boolean = False

            ' Calculate the total length of the line
            Dim totalLength As Single = Math.Sqrt((endPoint.X - startPoint.X) ^ 2 + (endPoint.Y - startPoint.Y) ^ 2)

            Dim segmentLength As Single = 0.5
            ' Calculate the number of segments
            Dim numSegments As Integer = Math.Ceiling(totalLength / segmentLength)

            Dim lineVisual As New DrawingVisual
            Dim hasVisualBeenAdded As Boolean = False
            Dim lvIndex As Integer = 0

            ' Generate and draw each segment
            For i As Integer = 0 To numSegments - 1
                If cToken.IsCancellationRequested Then Return 1
                stopwatch.Restart()

                Dim t2 As Single = (i + 1) / numSegments

                Dim segmentEnd As New Point(
                startPoint.X + (endPoint.X - startPoint.X) * t2,
                startPoint.Y + (endPoint.Y - startPoint.Y) * t2
            )
                If Not hasVisualBeenAdded Then
                    lineVisual = New DrawingVisual()
                    visualHost.AddVisual(lineVisual)
                    hasVisualBeenAdded = True
                End If

                Using dc As DrawingContext = lineVisual.RenderOpen()
                    Dim pen As New Pen(line.Stroke, line.StrokeThickness)
                    pen.StartLineCap = PenLineCap.Round
                    pen.EndLineCap = PenLineCap.Round
                    dc.DrawLine(pen, startPoint, segmentEnd)
                End Using

                ' Handle travel lines
                If isTravelMove Then
                    If Not hasTravelMoveBeenAdded Then
                        travelMoveVisuals.Add(lineVisual)
                        hasTravelMoveBeenAdded = True
                    End If
                    If Not TravelMovesVisibilityToggle.IsChecked Then
                        lineVisual.Opacity = 0 ' Hide the travel line
                    End If
                End If

                ' Calculate the delay for this segment in milliseconds
                Dim delayTime As Single = Math.Min(segmentLength, totalLength) / ViewModel.LogarithmicPreviewSpeed * 1000
                accumulatedDelay += delayTime
                stopwatch.Stop()
                accumulatedDelay -= stopwatch.Elapsed.TotalMilliseconds

                ' Render the batch if the accumulated delay exceeds 1 millisecond
                If accumulatedDelay >= 1 Then
                    Try
                        Await Task.Delay(Math.Max(CInt(accumulatedDelay), 1), cToken)
                    Catch ex As TaskCanceledException
                        Return 1
                    End Try

                    accumulatedDelay = 0
                End If
            Next

        Next

        Return 0
    End Function

    Dim colourWhenHovered As Color = CType(ColorConverter.ConvertFromString("#31323b"), Color)
    Dim colourWhenUnhovered As Color = CType(ColorConverter.ConvertFromString("#0DFFFFFF"), Color)
    Dim targetColor As Color = colourWhenUnhovered

    Private Sub BackgroundHitTest()
        Dim transform = PreviewControlBar.TransformToAncestor(Me)

        Dim pos1 = transform.Transform(New Point(0, 0))
        Dim pos2 = transform.Transform(New Point(PreviewControlBar.ActualWidth, PreviewControlBar.ActualHeight))
        Dim rect As New Rect(pos1, pos2)

        Dim tf2 = mainCanvas.TransformToAncestor(Me)
        Dim pos3 = tf2.Transform(New Point(0, 0))
        Dim pos4 = tf2.Transform(New Point(mainCanvas.ActualWidth, mainCanvas.ActualHeight))
        Dim rect2 As New Rect(pos3, pos4)

        Dim mainCanvasHit As Boolean = Not Rect.Intersect(rect, rect2).IsEmpty


        Dim newColor As Color

        If mainCanvasHit Then
            newColor = colourWhenHovered
        Else
            CaptureWindowWithBackgroundEffect()
            newColor = colourWhenUnhovered
        End If

        targetColor = newColor
        PreviewControlBar.Background = New SolidColorBrush(targetColor)

    End Sub


    Private Sub CaptureWindowWithBackgroundEffect()
        Dim parentWindow As Window = Window.GetWindow(Me)
        If parentWindow Is Nothing Then Return

        Dim windowBounds = parentWindow.RestoreBounds
        Dim screenLeft = CInt(windowBounds.Left)
        Dim screenTop = CInt(windowBounds.Top)
        Dim screenWidth = CInt(windowBounds.Width)
        Dim screenHeight = CInt(windowBounds.Height)

        Dim controlBounds = PreviewControlBar.TransformToAncestor(Window.GetWindow(Me)).TransformBounds(New Rect(0, 0, PreviewControlBar.ActualWidth, PreviewControlBar.ActualHeight))

        Using bmp As New System.Drawing.Bitmap(screenWidth, screenHeight)
                               Using g As System.Drawing.Graphics = System.Drawing.Graphics.FromImage(bmp)
                                   g.CopyFromScreen(screenLeft + controlBounds.Left + controlBounds.Width / 2, screenTop + controlBounds.Top + 5, 0, 0, New System.Drawing.Size(1, 1))
                               End Using

                               Dim pixelColor As System.Drawing.Color = bmp.GetPixel(0, 0)
                               colourWhenHovered = Color.FromArgb(255, pixelColor.R, pixelColor.G, pixelColor.B)
                           End Using

    End Sub

    Private mouseMoveTimer As DispatcherTimer = Nothing
    Private initialmove As Boolean = True
    Private Sub Page_MouseMove(sender As Object, e As MouseEventArgs)
        If initialmove Then
            initialmove = False
            CaptureWindowWithBackgroundEffect()
            Return
        End If

        If e.MiddleButton <> MouseButtonState.Pressed Then Return

        If mouseMoveTimer Is Nothing Then
            mouseMoveTimer = New DispatcherTimer With {.Interval = TimeSpan.FromMilliseconds(50)}
            AddHandler mouseMoveTimer.Tick, Sub()
                                                mouseMoveTimer.Stop()
                                                BackgroundHitTest()
                                            End Sub
        End If

        If Not mouseMoveTimer.IsEnabled Then mouseMoveTimer.Start()
    End Sub


    Private Sub Page_PreviewMouseWheel(sender As Object, e As MouseWheelEventArgs) Handles Me.MouseWheel

        If Not Window.GetWindow(Me).IsActive Then Return
        BackgroundHitTest()
    End Sub


    Private Sub OnLoaded() Handles Me.Loaded
        Me.Focus()
        AddHandler Window.GetWindow(Me).LostKeyboardFocus, Sub()

                                                               PreviewControlBar.Background = New SolidColorBrush(CType(ColorConverter.ConvertFromString("#323232"), Color))
                                                           End Sub
        AddHandler Window.GetWindow(Me).GotKeyboardFocus, Sub()
                                                              BackgroundHitTest()

                                                          End Sub
    End Sub

End Class


Public Class VisualHost
    Inherits FrameworkElement

    Private ReadOnly _visuals As New VisualCollection(Me)

    Public Sub New()
    End Sub

    Public Sub AddVisual(visual As Visual)
        _visuals.Add(visual)
    End Sub

    Public Sub RemoveVisual(visual As Visual)
        _visuals.Remove(visual)
    End Sub

    Public Sub ClearVisuals()
        _visuals.Clear()
    End Sub

    Public Function ChildrenCount() As Integer
        Return _visuals.Count
    End Function

    Protected Overrides ReadOnly Property VisualChildrenCount As Integer
        Get
            Return _visuals.Count
        End Get
    End Property

    Protected Overrides Function GetVisualChild(index As Integer) As Visual
        Return _visuals(index)
    End Function
End Class