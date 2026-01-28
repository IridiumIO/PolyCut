Imports System.ComponentModel
Imports System.Globalization
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

        UpdateGCodeDocument()


        AddHandler viewmodel.PropertyChanged, AddressOf MainViewModel_PropertyChanged
        AddHandler viewmodel.PropertyChanged, AddressOf PropertyChangedHandler

        SubscribeToPrinter(viewmodel.Printer)

        If viewmodel.GCode?.Length <> 0 Then
            cancellationTokenSource.Cancel()
            viewmodel.GCodePaths.Clear()
            DrawToolPaths()
        End If

    End Sub

    Private Sub MainViewModel_PropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        If e Is Nothing Then Return

        If String.Equals(e.PropertyName, NameOf(ViewModel.Printer), StringComparison.OrdinalIgnoreCase) Then
            SubscribeToPrinter(ViewModel.Printer)
            Transform()
        End If
    End Sub

    Private Sub SubscribeToPrinter(pr As Printer)

        If _subscribedPrinter IsNot Nothing Then
            RemoveHandler _subscribedPrinter.PropertyChanged, AddressOf PropertyChangedHandler
        End If

        _subscribedPrinter = pr

        If _subscribedPrinter IsNot Nothing Then
            AddHandler _subscribedPrinter.PropertyChanged, AddressOf PropertyChangedHandler
        End If

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
            UpdateGCodeDocument()
        End If


    End Sub

    Private gcodeListCancellation As CancellationTokenSource = Nothing

    Private Async Sub UpdateGCodeDocument()

        If gcodeListCancellation IsNot Nothing Then
            Try
                gcodeListCancellation.Cancel()
            Catch
            End Try
            gcodeListCancellation.Dispose()
        End If
        gcodeListCancellation = New CancellationTokenSource()
        Dim cToken = gcodeListCancellation.Token

        Dim text = If(ViewModel?.GCode?.ToString(), "")
        If String.IsNullOrWhiteSpace(text) Then
            GCodeListView.ItemsSource = Nothing
            Return
        End If


        Dim lines = text.Replace(vbCr, "").Split(New String() {vbLf}, StringSplitOptions.None)


        Dim tokenized As List(Of InlineBuilder.LineTokens) = Nothing
        Try
            tokenized = Await Task.Run(Function()
                                           Return TokenizeLinesForList(lines, cToken)
                                       End Function, cToken)
        Catch ex As OperationCanceledException
            Return
        End Try


        If cToken.IsCancellationRequested Then Return
        GCodeListView.ItemsSource = tokenized
    End Sub


    Private Shared ReadOnly LocalTokenizerRegex As New Regex(
     "(?ix)
            (?<Comment>        ;.*$ )
          | (?<ParenComment>   \(.*?\) )
          | (?<KlipperExpr>    \[[^\]]+\] )
          | (?<KlipperParam>   \b[A-Z_][A-Z0-9_]*=[^\s]+ )
          | (?<GCode>          \b[GM]\d+(?:\.\d+)?\b )
          | (?<Axis>           \b[XYZ][+-]?\d+(?:\.\d+)?\b )
          | (?<Feed>           \b[FSE][+-]?\d+(?:\.\d+)?\b )
          | (?<Macro>          \b[A-Z_]{2,}[A-Z0-9_]*\b )
          | (?<Number>         [+-]?\d+(?:\.\d+)? )
        ",
        RegexOptions.Compiled)

    Private Function TokenizeLinesForList(lines As String(), cToken As CancellationToken) As List(Of InlineBuilder.LineTokens)
        Dim out As New List(Of InlineBuilder.LineTokens)(lines.Length)

        For Each line As String In lines
            cToken.ThrowIfCancellationRequested()

            Dim trimmed = If(line, "").Trim()
            If String.Equals(trimmed, ";######################################", StringComparison.Ordinal) Then
                Dim hr As New InlineBuilder.LineTokens With {.IsHorizontalRule = True}
                out.Add(hr)
                Continue For
            End If

            Dim lt As New InlineBuilder.LineTokens()
            Dim matches = LocalTokenizerRegex.Matches(line)
            Dim last = 0

            For Each m As Match In matches
                If m.Index > last Then
                    lt.Tokens.Add(New InlineBuilder.TokenDto(0, line.Substring(last, m.Index - last)))
                End If

                If m.Groups("Comment").Success Then
                    lt.Tokens.Add(New InlineBuilder.TokenDto(1, m.Value))
                    last = line.Length
                    Exit For
                End If

                Dim ttype As Integer = 0
                If m.Groups("ParenComment").Success Then
                    ttype = 2
                ElseIf m.Groups("KlipperExpr").Success Then
                    ttype = 3
                ElseIf m.Groups("KlipperParam").Success Then
                    ttype = 4
                ElseIf m.Groups("GCode").Success Then
                    ttype = 5
                ElseIf m.Groups("Axis").Success Then
                    ttype = 6
                ElseIf m.Groups("Feed").Success Then
                    ttype = 7
                ElseIf m.Groups("Macro").Success Then
                    ttype = 8
                ElseIf m.Groups("Number").Success Then
                    ttype = 9
                Else
                    ttype = 0
                End If

                lt.Tokens.Add(New InlineBuilder.TokenDto(ttype, m.Value))
                last = m.Index + m.Length
            Next

            If last < line.Length Then
                lt.Tokens.Add(New InlineBuilder.TokenDto(0, line.Substring(last)))
            End If

            out.Add(lt)
        Next

        Return out
    End Function

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