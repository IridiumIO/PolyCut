Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Net
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Windows.Threading
Imports SharpVectors.Runtime
Imports WPF.Ui.Controls
Imports PolyCut.Core.Extensions
Imports WPF.Ui.Abstractions.Controls
Imports PolyCut.Core

Class PreviewPage : Implements INavigableView(Of MainViewModel)

    Public ReadOnly Property ViewModel As MainViewModel Implements INavigableView(Of MainViewModel).ViewModel
    Private cancellationTokenSource As CancellationTokenSource = New CancellationTokenSource


    Sub New(viewmodel As MainViewModel)

        Me.ViewModel = viewmodel
        DataContext = viewmodel
        InitializeComponent()
        zoomPanControl.Scale = 2
        zoomPanControl.TranslateTransform.X = -viewmodel.Printer.BedWidth / 2
        zoomPanControl.TranslateTransform.Y = -viewmodel.Printer.BedHeight / 2
        InitializeDrawingVisual()
        AddHandler viewmodel.CuttingMat.PropertyChanged, AddressOf PropertyChangedHandler
        AddHandler viewmodel.PropertyChanged, AddressOf PropertyChangedHandler
        Transform()

        If viewmodel.GCode?.Length <> 0 Then
            cancellationTokenSource.Cancel()
            viewmodel.GCodePaths.Clear()
            DrawToolPaths()
            'DrawToolpaths(cancellationTokenSource.Token, viewmodel.GCode)
        End If

    End Sub

    Private Sub PropertyChangedHandler(sender As Object, e As PropertyChangedEventArgs)
        Dim alignmentPropertyNames = {
            NameOf(ViewModel.CuttingMat.SelectedVerticalAlignment),
            NameOf(ViewModel.CuttingMat.SelectedHorizontalAlignment),
            NameOf(ViewModel.CuttingMat.SelectedRotation),
            NameOf(ViewModel.CuttingMat)}

        If e.PropertyName = NameOf(ViewModel.GCode) Then
            cancellationTokenSource.Cancel()
            ViewModel.GCodePaths.Clear()
            DrawToolPaths()

        End If
        If alignmentPropertyNames.Contains(e.PropertyName) Then
            Transform()
        End If


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
                ' Draw the line
                dc.DrawLine(New Pen(line.Stroke, line.StrokeThickness), New Point(line.X1, line.Y1), New Point(line.X2, line.Y2))
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
        Dim accumulatedDelay As Double = 0
        Dim stopwatch As New Stopwatch()

        For Each line In ViewModel.GCodeGeometry.Paths
            If cToken.IsCancellationRequested Then Return 1

            Dim startPoint As New Point(line.X1, line.Y1)
            Dim endPoint As New Point(line.X2, line.Y2)

            Dim isTravelMove As Boolean = (line.Stroke Is Brushes.OrangeRed)

            ' Calculate the total length of the line
            Dim totalLength As Double = Math.Sqrt((endPoint.X - startPoint.X) ^ 2 + (endPoint.Y - startPoint.Y) ^ 2)

            Dim segmentLength As Double = 0.5
            ' Calculate the number of segments
            Dim numSegments As Integer = Math.Ceiling(totalLength / segmentLength)

            ' List to store segment visuals for this line
            Dim segmentVisuals As New List(Of DrawingVisual)()

            Dim lineVisual As New DrawingVisual
            ' Generate and draw each segment
            For i As Integer = 0 To numSegments - 1
                If cToken.IsCancellationRequested Then Return 1
                stopwatch.Restart()
                ' Calculate the start and end points of the segment
                Dim t1 As Double = i / numSegments
                Dim t2 As Double = (i + 1) / numSegments

                Dim segmentStart As New Point(
                startPoint.X + (endPoint.X - startPoint.X) * t1,
                startPoint.Y + (endPoint.Y - startPoint.Y) * t1
            )


                Dim segmentEnd As New Point(
                startPoint.X + (endPoint.X - startPoint.X) * t2,
                startPoint.Y + (endPoint.Y - startPoint.Y) * t2
            )
                visualHost.RemoveVisual(lineVisual)
                lineVisual = New DrawingVisual()
                visualHost.AddVisual(lineVisual)


                Using dc As DrawingContext = lineVisual.RenderOpen()
                    dc.DrawLine(New Pen(line.Stroke, line.StrokeThickness), startPoint, segmentEnd)
                End Using

                ' Handle travel lines
                If isTravelMove Then
                    If Not travelMoveVisuals.Contains(lineVisual) Then
                        travelMoveVisuals.Add(lineVisual)
                    End If
                    If Not TravelMovesVisibilityToggle.IsChecked Then
                        lineVisual.Opacity = 0 ' Hide the travel line
                    End If
                End If

                ' Calculate the delay for this segment in milliseconds
                Dim delayTime As Double = Math.Min(segmentLength, totalLength) / (ViewModel.LogarithmicPreviewSpeed) * 1000
                accumulatedDelay += delayTime
                stopwatch.Stop()
                accumulatedDelay -= stopwatch.Elapsed.TotalMilliseconds

                ' Render the batch if the accumulated delay exceeds 1 millisecond
                If accumulatedDelay >= 1 Then
                    Try
                        Await Task.Delay(Math.Max(CInt(accumulatedDelay), 1), cToken)
                    Catch ex As TaskCanceledException
                        ' Exit gracefully if the task is canceled
                        Return 1
                    End Try
                    accumulatedDelay = 0 ' Reset the accumulated delay
                End If
            Next

        Next

        Debug.WriteLine($"Total segments: {visualHost.ChildrenCount}")
        Return 0
    End Function



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