Imports System.ComponentModel
Imports System.Globalization
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Windows.Threading

Imports MeasurePerformance.IL.Weaver


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

        AddHandler viewmodel.UIConfiguration.PropertyChanged, Sub(s, e)
                                                                  If e.PropertyName = NameOf(UIConfiguration.PreviewDrawingBrush) Then
                                                                      _RenderPen = CreatePenWithBrush(_RenderPen, viewmodel.UIConfiguration.PreviewDrawingBrush)
                                                                      cancellationTokenSource.Cancel()
                                                                      viewmodel.GCodePaths.Clear()
                                                                      DrawToolPaths()
                                                                  ElseIf e.PropertyName = NameOf(UIConfiguration.PreviewTravelBrush) Then
                                                                      _TravelPen = CreatePenWithBrush(_TravelPen, viewmodel.UIConfiguration.PreviewTravelBrush)
                                                                      cancellationTokenSource.Cancel()
                                                                      viewmodel.GCodePaths.Clear()
                                                                      DrawToolPaths()
                                                                  ElseIf e.PropertyName = NameOf(UIConfiguration.PreviewCursorBrush) Then
                                                                      _CursorPen = CreatePenWithBrush(_CursorPen, viewmodel.UIConfiguration.PreviewCursorBrush)
                                                                  End If
                                                              End Sub
    End Sub

    Function CreatePenWithBrush(basePen As Pen, brushHex As String) As Pen
        Dim brushColor As Color = CType(ColorConverter.ConvertFromString(brushHex), Color)
        Dim brsh = New SolidColorBrush(brushColor)
        brsh.Freeze()
        Dim newPen = basePen.Clone()
        newPen.Brush = brsh
        newPen.Freeze()
        Return newPen
    End Function

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


        If e.PropertyName = NameOf(ViewModel.GCodeGeometry) Then
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

        If cToken.IsCancellationRequested Then Return out

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

    Private Sub TogglePlayPauseSymbol()
        If _IsPlaying Then
            If _IsPaused Then
                PlayPreviewIcon.Symbol = WPF.Ui.Controls.SymbolRegular.Play16
            Else
                PlayPreviewIcon.Symbol = WPF.Ui.Controls.SymbolRegular.Pause16
            End If
        Else
            PlayPreviewIcon.Symbol = WPF.Ui.Controls.SymbolRegular.Play16
        End If
    End Sub



    Private Async Sub PreviewToolpath(sender As Object, e As RoutedEventArgs)

        If _IsPlaying Then
            If Not _IsPaused Then
                ' Pause
                _IsPaused = True
                _pauseTcs = New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
            Else
                ' Resume
                _IsPaused = False
                _pauseTcs?.TrySetResult(True)
                _pauseTcs = Nothing
            End If
            TogglePlayPauseSymbol()

        Else
            _IsPlaying = True
            _IsPaused = False
            _pauseTcs = Nothing
            TogglePlayPauseSymbol()

            Try

                Await cancellationTokenSource.CancelAsync()

                cancellationTokenSource = New CancellationTokenSource
                Dim ret = Await PreviewToolpaths(cancellationTokenSource.Token)

            Finally
                _IsPlaying = False
                _IsPaused = False
                _pauseTcs = Nothing
                TogglePlayPauseSymbol()

            End Try
        End If
    End Sub

    Private Sub StopPreviewToolpath(sender As Object, e As RoutedEventArgs)
        cancellationTokenSource.Cancel()
        _IsPlaying = False
        _IsPaused = False
        _pauseTcs = Nothing
        visualHost.ClearVisuals()
        travelMoveVisuals.Clear()
        DrawToolPaths()
        cancellationTokenSource = New CancellationTokenSource
    End Sub


    Private Sub StepForwardPreviewButton_Click(sender As Object, e As RoutedEventArgs)
        If Not _IsPaused Then Return
        Interlocked.Increment(_stepForwardCount)
        _pauseTcs?.TrySetResult(True)
    End Sub


    Private Sub StepBackPreviewButton_Click(sender As Object, e As RoutedEventArgs)
        If Not _IsPaused Then Return
        Interlocked.Increment(_stepBackCount)
        _pauseTcs?.TrySetResult(True)
    End Sub


    Private travelMoveVisuals As New List(Of DrawingVisual)()


    Private _RenderPen As New Pen() With {
        .Thickness = 0.2,
        .StartLineCap = PenLineCap.Round,
        .EndLineCap = PenLineCap.Round
    }


    Private _TravelPen As New Pen() With {
        .Thickness = 0.1,
        .StartLineCap = PenLineCap.Round,
        .EndLineCap = PenLineCap.Round
    }

    Private _CursorPen As New Pen() With {
        .Thickness = 0.5,
        .StartLineCap = PenLineCap.Round,
        .EndLineCap = PenLineCap.Round
    }


    <MeasurePerformance>
    Private Function DrawToolPaths()

        ' Clear existing visuals in the VisualHost
        visualHost.ClearVisuals()
        travelMoveVisuals.Clear()

        ' Compile the GCode into paths
        If ViewModel.GCodeGeometry Is Nothing Then Return 1
        Dim gc = ViewModel.GCodeGeometry

        For Each line In gc.Paths
            ' Create a new DrawingVisual for the line
            Dim lineVisual As New DrawingVisual()
            Using dc As DrawingContext = lineVisual.RenderOpen()

                If line.IsRapidMove Then
                    dc.DrawLine(_TravelPen, New Point(line.X1, line.Y1), New Point(line.X2, line.Y2))
                Else
                    dc.DrawLine(_RenderPen, New Point(line.X1, line.Y1), New Point(line.X2, line.Y2))
                End If

            End Using

            ' Add the visual to the VisualHost
            visualHost.AddVisual(lineVisual)

            ' Handle travel lines
            If line.IsRapidMove Then
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


        ' Ensure _RenderPen has a brush
        If _RenderPen.Brush Is Nothing Then
            _RenderPen = CreatePenWithBrush(_RenderPen, ViewModel.UIConfiguration.PreviewDrawingBrush)
        End If

        ' Ensure _TravelPen has a brush
        If _TravelPen.Brush Is Nothing Then
            _TravelPen = CreatePenWithBrush(_TravelPen, ViewModel.UIConfiguration.PreviewTravelBrush)
        End If

        ' Ensure _CursorPen has a brush
        If _CursorPen.Brush Is Nothing Then
            _CursorPen = CreatePenWithBrush(_CursorPen, ViewModel.UIConfiguration.PreviewCursorBrush)
        End If

        visualHost.ClearVisuals()
        Canvas.SetLeft(visualHost, 0)
        Canvas.SetTop(visualHost, 0)
    End Sub



    Private _IsPlaying As Boolean
    Private _IsPaused As Boolean
    Private _pauseTcs As TaskCompletionSource(Of Boolean)

    Private _stepForwardCount As Integer = 0
    Private _stepBackCount As Integer = 0

    Private _lineVisuals As List(Of DrawingVisual)
    Private _currentIndex As Integer = 0 ' NEXT line to start drawing

    Private Async Function PreviewToolpaths(cToken As CancellationToken) As Task(Of Integer)
        visualHost.ClearVisuals()
        travelMoveVisuals.Clear()

        _cursorVisual = Nothing
        EnsureCursor()
        ClearCursor()

        Dim paths = ViewModel?.GCodeGeometry?.Paths
        If paths Is Nothing OrElse paths.Count = 0 Then Return 0

        _lineVisuals = Enumerable.Repeat(Of DrawingVisual)(Nothing, paths.Count).ToList()
        _currentIndex = 0

        Dim accumulatedDelay As Single = 0
        Dim stopwatch As New Stopwatch()

        While _currentIndex < paths.Count
            If cToken.IsCancellationRequested Then Return 1

            ' --------- PAUSE GATE BEFORE STARTING THE LINE ----------
            ' --------- PAUSE GATE BEFORE STARTING THE LINE ----------
            While _IsPaused

                ' 1) Apply ALL queued step-backs
                Dim backCount = Interlocked.Exchange(_stepBackCount, 0)
                If backCount > 0 Then
                    For n As Integer = 1 To backCount
                        If _currentIndex <= 0 Then Exit For
                        _currentIndex -= 1
                        RemoveLineVisual(_currentIndex)

                        Dim ln2 = paths(_currentIndex)
                        UpdateCursor(New Point(ln2.X1, ln2.Y1))
                    Next
                    Continue While
                End If

                ' 2) Step forward: jump whole lines
                Dim fw = Interlocked.Exchange(_stepForwardCount, 0)
                If fw > 0 Then
                    While fw > 0 AndAlso _currentIndex < paths.Count
                        DrawLineInstant(paths(_currentIndex))
                        _currentIndex += 1
                        fw -= 1
                    End While

                    ' stay paused after stepping
                    Continue While
                End If

                ' 3) Otherwise wait (resume/step/back will complete _pauseTcs)
                If _pauseTcs Is Nothing Then
                    _pauseTcs = New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
                End If

                Dim tcs = _pauseTcs
                Try : Await tcs.Task.ConfigureAwait(True) : Catch : End Try
                If ReferenceEquals(_pauseTcs, tcs) Then _pauseTcs = Nothing

            End While

            If cToken.IsCancellationRequested Then Return 1

            ' --------- DRAW CURRENT LINE (_currentIndex) ----------
            Dim line = paths(_currentIndex)

            Dim startPoint As New Point(line.X1, line.Y1)

            UpdateCursor(startPoint)

            Dim endPoint As New Point(line.X2, line.Y2)
            Dim isTravelMove As Boolean = line.IsRapidMove

            Dim totalLength As Single = Math.Sqrt((endPoint.X - startPoint.X) ^ 2 + (endPoint.Y - startPoint.Y) ^ 2)
            Dim segmentLength As Single = 0.5
            Dim numSegments As Integer = Math.Max(1, CInt(Math.Ceiling(totalLength / segmentLength)))

            Dim lineVisual As New DrawingVisual()
            _lineVisuals(_currentIndex) = lineVisual
            visualHost.AddVisual(lineVisual)

            BringCursorToFront()

            If isTravelMove Then
                travelMoveVisuals.Add(lineVisual)
                If Not TravelMovesVisibilityToggle.IsChecked Then lineVisual.Opacity = 0
            End If

            Dim restartOuter As Boolean = False


            For i As Integer = 0 To numSegments - 1
                If cToken.IsCancellationRequested Then Return 1

                ' --------- SEGMENT PAUSE GATE (pause can happen mid-line) ----------
                While _IsPaused

                    ' Step Back during partial line: delete current line visual, then move back
                    Dim backCount2 = Interlocked.Exchange(_stepBackCount, 0)
                    If backCount2 > 0 Then
                        ' 1) Always reset the CURRENT line first (consume 1 back press)
                        RemoveLineVisual(_currentIndex)

                        Dim lnCur = paths(_currentIndex)
                        UpdateCursor(New Point(lnCur.X1, lnCur.Y1))
                        BringCursorToFront()

                        backCount2 -= 1

                        ' 2) Any remaining back presses go to previous completed lines
                        For n As Integer = 1 To backCount2
                            If _currentIndex <= 0 Then Exit For
                            _currentIndex -= 1
                            RemoveLineVisual(_currentIndex)

                            Dim ln2 = paths(_currentIndex)
                            UpdateCursor(New Point(ln2.X1, ln2.Y1))
                            BringCursorToFront()
                        Next

                        restartOuter = True
                        Exit For
                    End If

                    ' Step Forward while paused mid-line:
                    Dim fw2 = Interlocked.Exchange(_stepForwardCount, 0)
                    If fw2 > 0 Then
                        ' remove the partially drawn current line
                        RemoveLineVisual(_currentIndex)

                        ' draw this line (and additional lines) instantly
                        While fw2 > 0 AndAlso _currentIndex < paths.Count
                            DrawLineInstant(paths(_currentIndex))
                            _currentIndex += 1
                            fw2 -= 1
                        End While

                        restartOuter = True
                        Exit For
                    End If

                    If _pauseTcs Is Nothing Then
                        _pauseTcs = New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
                    End If

                    Dim tcs2 = _pauseTcs
                    Try
                        Await tcs2.Task.ConfigureAwait(True)
                    Catch
                    End Try
                    If ReferenceEquals(_pauseTcs, tcs2) Then _pauseTcs = Nothing
                End While

                If restartOuter Then Exit For

                ' --------- segment draw + delay ----------
                stopwatch.Restart()

                Dim t2 As Single = CSng((i + 1) / numSegments)
                Dim segmentEnd As New Point(
                    startPoint.X + (endPoint.X - startPoint.X) * t2,
                    startPoint.Y + (endPoint.Y - startPoint.Y) * t2
                )

                Using dc As DrawingContext = lineVisual.RenderOpen()
                    dc.DrawLine(If(isTravelMove, _TravelPen, _RenderPen), startPoint, segmentEnd)
                End Using

                UpdateCursor(segmentEnd)

                Dim delayTime As Single = Math.Min(segmentLength, totalLength) / ViewModel.LogarithmicPreviewSpeed * 1000
                accumulatedDelay += delayTime
                stopwatch.Stop()
                accumulatedDelay -= CSng(stopwatch.Elapsed.TotalMilliseconds)

                If accumulatedDelay >= 1 Then
                    Try
                        Await Task.Delay(Math.Max(CInt(accumulatedDelay), 1), cToken)
                    Catch ex As TaskCanceledException
                        Return 1
                    End Try
                    accumulatedDelay = 0
                End If
            Next

            If restartOuter Then
                Continue While ' resume from new _currentIndex
            End If

            ' finished the line
            _currentIndex += 1


        End While

        Return 0

    End Function

    Private _cursorVisual As DrawingVisual
    Private ReadOnly _cursorFill As Brush = Brushes.Transparent
    Private Const _cursorRadius As Double = 2.2
    Private Const _cursorCrosshairHalfSize As Double = 3.8

    Private Sub EnsureCursor()
        If _cursorVisual Is Nothing Then
            _cursorVisual = New DrawingVisual()
            visualHost.AddVisual(_cursorVisual)
        End If
        BringCursorToFront()
    End Sub

    Private Sub BringCursorToFront()
        If _cursorVisual Is Nothing Then Return
        visualHost.RemoveVisual(_cursorVisual)
        visualHost.AddVisual(_cursorVisual) ' last = top-most
    End Sub

    Private Sub UpdateCursor(p As Point)
        If _cursorVisual Is Nothing Then Return
        Using dc = _cursorVisual.RenderOpen()
            dc.DrawEllipse(_cursorFill, _cursorPen, p, _cursorRadius, _cursorRadius)
            dc.DrawLine(_cursorPen, New Point(p.X - _cursorCrosshairHalfSize, p.Y), New Point(p.X + _cursorCrosshairHalfSize, p.Y))
            dc.DrawLine(_cursorPen, New Point(p.X, p.Y - _cursorCrosshairHalfSize), New Point(p.X, p.Y + _cursorCrosshairHalfSize))
        End Using
    End Sub

    Private Sub ClearCursor()
        If _cursorVisual Is Nothing Then Return
        Using dc = _cursorVisual.RenderOpen()
            ' draw nothing
        End Using
    End Sub

    Private Sub RemoveLineVisual(i As Integer)
        If _lineVisuals Is Nothing OrElse i < 0 OrElse i >= _lineVisuals.Count Then Return

        Dim v = _lineVisuals(i)
        If v Is Nothing Then Return

        visualHost.RemoveVisual(v)
        travelMoveVisuals.Remove(v)
        _lineVisuals(i) = Nothing
    End Sub

    Private Sub DrawLineInstant(line As GCodeLine)
        Dim startPoint As New Point(line.X1, line.Y1)
        Dim endPoint As New Point(line.X2, line.Y2)
        Dim isTravelMove As Boolean = line.IsRapidMove

        Dim v As New DrawingVisual()
        _lineVisuals(_currentIndex) = v
        visualHost.AddVisual(v)

        If isTravelMove Then
            travelMoveVisuals.Add(v)
            If Not TravelMovesVisibilityToggle.IsChecked Then v.Opacity = 0
        End If

        Using dc As DrawingContext = v.RenderOpen()
            dc.DrawLine(If(isTravelMove, _TravelPen, _RenderPen), startPoint, endPoint)
        End Using

        UpdateCursor(endPoint)
        BringCursorToFront()
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