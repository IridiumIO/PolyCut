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

            'DrawToolpaths(cancellationTokenSource.Token, ViewModel.GCode)
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


    Private Function compileGCodes(gCode As String) As GCodeGeometry

        Dim gcG As New GCodeGeometry(gCode)

        Return gcG

    End Function

    Private Function DrawToolPaths()
        Dim gc = compileGCodes(ViewModel.GCode)

        For Each line In gc.Paths
            line.Visibility = Visibility.Visible
            If Not TravelMovesVisibilityToggle.IsChecked Then
                If line.Stroke Is Brushes.OrangeRed Then
                    line.Visibility = Visibility.Collapsed
                End If
            End If
        Next

        ViewModel.GCodePaths = New ObservableCollection(Of Line)(gc.Paths)
        Return 0
    End Function


    Private Async Function PreviewToolpaths(cToken As CancellationToken) As Task(Of Integer)
        ViewModel.GCodePaths.Clear()

        ' Define the drawing speed (milliseconds per unit length)
        Dim speed As Double = 5 ' Adjust this value to control the overall drawing speed

        ' Define the segment length (adjust as needed for smoothness)
        Dim segmentLength As Double = 5.0

        For Each line In ViewModel.GCodeGeometry.Paths
            If cToken.IsCancellationRequested Then
                Return 1
            End If

            ' Get the start and end points of the line
            Dim startPoint As Point = New Point(line.X1, line.Y1)
            Dim endPoint As Point = New Point(line.X2, line.Y2)

            ' Calculate the total length of the line
            Dim totalLength As Double = Math.Sqrt((endPoint.X - startPoint.X) ^ 2 + (endPoint.Y - startPoint.Y) ^ 2)

            ' Calculate the number of segments
            Dim numSegments As Integer = Math.Ceiling(totalLength / segmentLength)

            ' Temporary collection to store segments for the current line
            Dim segments As New List(Of Line)

            ' Generate and draw each segment
            For i As Integer = 0 To numSegments - 1
                If cToken.IsCancellationRequested Then
                    Return 1
                End If

                ' Calculate the start and end points of the segment
                Dim t1 As Double = i / numSegments
                Dim t2 As Double = (i + 1) / numSegments

                Dim segmentStart As Point = New Point(
                startPoint.X + (endPoint.X - startPoint.X) * t1,
                startPoint.Y + (endPoint.Y - startPoint.Y) * t1
            )

                Dim segmentEnd As Point = New Point(
                startPoint.X + (endPoint.X - startPoint.X) * t2,
                startPoint.Y + (endPoint.Y - startPoint.Y) * t2
            )

                ' Create a new line segment
                Dim segment As New Line() With {
                .X1 = segmentStart.X,
                .Y1 = segmentStart.Y,
                .X2 = segmentEnd.X,
                .Y2 = segmentEnd.Y,
                .Stroke = line.Stroke,
                .StrokeThickness = line.StrokeThickness,
                .Visibility = If(Not TravelMovesVisibilityToggle.IsChecked AndAlso line.Stroke Is Brushes.OrangeRed, Visibility.Collapsed, Visibility.Visible)
            }


                segments.Add(segment)
                Await Application.Current.Dispatcher.InvokeAsync(Sub()
                                                                     ViewModel.GCodePaths.Add(segment)
                                                                 End Sub)


                ' Calculate the length of the segment
                Dim segmentLengthActual As Double = Math.Sqrt((segmentEnd.X - segmentStart.X) ^ 2 + (segmentEnd.Y - segmentStart.Y) ^ 2)

                Dim renderSpeed = 10 / (ViewModel.PreviewRenderSpeed)
                Dim delay As Integer = CInt(segmentLengthActual * renderSpeed)
                Await Task.Delay(delay)
            Next

            ' Replace all segments with the original line
            Await Application.Current.Dispatcher.InvokeAsync(Sub()
                                                                 ' Remove all segments from GCodePaths
                                                                 For Each segment In segments
                                                                     ViewModel.GCodePaths.Remove(segment)
                                                                 Next

                                                                 ' Add the original line back to GCodePaths
                                                                 Dim finalLine As New Line() With {
                                                                 .X1 = startPoint.X,
                                                                 .Y1 = startPoint.Y,
                                                                 .X2 = endPoint.X,
                                                                 .Y2 = endPoint.Y,
                                                                 .Stroke = line.Stroke,
                                                                 .StrokeThickness = line.StrokeThickness,
                                                                 .Visibility = line.Visibility
                                                             }
                                                                 ViewModel.GCodePaths.Add(finalLine)
                                                             End Sub)
        Next

        Return 0
    End Function




    Dim isDragging As Boolean = False
    Dim translation As Point

    Private Sub TravelMovesVisibilityToggle_Checked(sender As Object, e As RoutedEventArgs) Handles TravelMovesVisibilityToggle.Checked, TravelMovesVisibilityToggle.Unchecked

        If TravelMovesVisibilityToggle.IsChecked Then
            For Each obj In ViewModel.GCodePaths
                obj.Visibility = Visibility.Visible
            Next
        Else
            For Each obj In ViewModel.GCodePaths.Where(Function(x) x.Stroke Is Brushes.OrangeRed)
                obj.Visibility = Visibility.Collapsed
            Next
        End If

    End Sub
End Class

