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

    End Sub



    Private Sub PropertyChangedHandler(sender As Object, e As PropertyChangedEventArgs)

        If e.PropertyName = NameOf(ViewModel.GCodeGeometry) Then
            cancellationTokenSource.Cancel()
            ViewModel.GCodePaths.Clear()
            DrawToolPaths()
            UpdateGCodeDocument()
        End If


    End Sub

    Private Sub UpdateGCodeDocument()
        Dim text = If(ViewModel?.GCode?.ToString(), "")
        If String.IsNullOrWhiteSpace(text) Then
            GCodeListView.ItemsSource = Nothing
            Return
        End If


        Dim lines = text.Replace(vbCr, "").Split({vbLf}, StringSplitOptions.None)



        Dim rows(lines.Length - 1) As LazyGCodeLine
        For i = 0 To lines.Length - 1
            rows(i) = New LazyGCodeLine(lines(i))
        Next

        GCodeListView.ItemsSource = rows
    End Sub







    Private ReadOnly regexG01 As New Regex("G01.*?X([\d.]+).*?Y([\d.]+)")
    Private ReadOnly regexG00 As New Regex("G00.*?X([\d.]+).*?Y([\d.]+)")

    Private Sub TogglePlayPauseSymbol()
        If _IsPlaying Then
            If _IsPaused Then
                PlayPreviewIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.Play16
            Else
                PlayPreviewIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.Pause16
            End If
        Else
            PlayPreviewIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.Play16
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
        ShowStillToolpath()
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



    Private Const DefaultChunkMoves As Integer = 64

    Private Shared Sub AppendRun(ctx As StreamGeometryContext, paths As IReadOnlyList(Of GCodeLine), fromIndex As Integer, toIndex As Integer, travel As Boolean, ByRef any As Boolean)

        Dim started As Boolean = False
        Dim lastX As Single = 0
        Dim lastY As Single = 0

        For i = fromIndex To toIndex
            Dim ln = paths(i)

            If ln.IsRapidMove <> travel Then
                started = False
                Continue For
            End If

            If Not started OrElse ln.X1 <> lastX OrElse ln.Y1 <> lastY Then
                ctx.BeginFigure(New Point(ln.X1, ln.Y1), False, False)
                started = True
                any = True
            End If

            ctx.LineTo(New Point(ln.X2, ln.Y2), True, False)
            lastX = ln.X2
            lastY = ln.Y2
        Next
    End Sub

    Private Sub DrawChunkedStatic(paths As IReadOnlyList(Of GCodeLine))

        Dim chunkSize As Integer = DefaultChunkMoves
        Dim first As Integer = 0

        While first < paths.Count

            Dim lastIndex As Integer = Math.Min(first + chunkSize, paths.Count) - 1

            Dim cut As New StreamGeometry()
            Dim anyCut As Boolean = False
            Using ctx = cut.Open()
                AppendRun(ctx, paths, first, lastIndex, False, anyCut)
            End Using
            cut.Freeze()

            If anyCut Then
                Dim cutVisual As New DrawingVisual()
                Using dc = cutVisual.RenderOpen()
                    dc.DrawGeometry(Nothing, _RenderPen, cut)
                End Using
                visualHost.AddVisual(cutVisual)
            End If

            Dim travel As New StreamGeometry()
            Dim anyTravel As Boolean = False
            Using ctx = travel.Open()
                AppendRun(ctx, paths, first, lastIndex, True, anyTravel)
            End Using
            travel.Freeze()

            If anyTravel Then
                Dim travelVisual As New DrawingVisual()
                Using dc = travelVisual.RenderOpen()
                    dc.DrawGeometry(Nothing, _TravelPen, travel)
                End Using
                ' Set initial visibility based on the toggle state
                If Not TravelMovesVisibilityToggle.IsChecked Then travelVisual.Opacity = 0
                visualHost.AddVisual(travelVisual)
                travelMoveVisuals.Add(travelVisual)
            End If

            first = lastIndex + 1
        End While
    End Sub



    Private Const CacheLineThreshold As Integer = 40000
    Private Const CacheScale As Double = 40.0

    Private Sub ApplyToolpathCache()
        Dim geo = ViewModel?.GCodeGeometry
        If geo Is Nothing OrElse geo.Paths.Count < CacheLineThreshold Then
            visualHost.CacheMode = Nothing
            Return
        End If

        If Not TypeOf visualHost.CacheMode Is BitmapCache Then
            visualHost.CacheMode = New BitmapCache() With {.RenderAtScale = CacheScale}
        End If
    End Sub


    Private Sub ShowStillToolpath()
        visualHost.CacheMode = Nothing
        visualHost.ClearVisuals()
        travelMoveVisuals.Clear()
        _folded.Clear()
        _lineVisuals = Nothing
        DrawToolPaths()
    End Sub

    Private Function DrawToolPaths()

        ' Clear existing visuals in the VisualHost
        visualHost.ClearVisuals()
        visualHostCursor.ClearVisuals()
        travelMoveVisuals.Clear()

        ' Compile the GCode into paths
        If ViewModel.GCodeGeometry Is Nothing Then
            visualHost.CacheMode = Nothing
            Return 1
        End If
        Dim gc = ViewModel.GCodeGeometry

        DrawChunkedStatic(gc.Paths)

        ApplyToolpathCache()

        Debug.WriteLine(visualHost.ChildrenCount() & " visuals drawn.")

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

    'Playback collapses each completed group of 64 lines into one geometry, so a long playback does not accumulate one
    'visual per line. Stepping back reopens the group, so a step is never more than a single line.
    Private Const PlaybackChunk As Integer = 64
    Private ReadOnly _folded As New Dictionary(Of Integer, List(Of DrawingVisual))()
    Private _nextFold As Integer = 0

    Private Async Function PreviewToolpaths(cToken As CancellationToken) As Task(Of Integer)
        visualHost.CacheMode = Nothing
        visualHost.ClearVisuals()
        visualHostCursor.ClearVisuals()
        travelMoveVisuals.Clear()

        _cursorVisual = Nothing
        EnsureCursor()
        ClearCursor()

        Dim paths = ViewModel?.GCodeGeometry?.Paths
        If paths Is Nothing OrElse paths.Count = 0 Then Return 0

        _lineVisuals = Enumerable.Repeat(Of DrawingVisual)(Nothing, paths.Count).ToList()
        _currentIndex = 0
        _folded.Clear()
        _nextFold = 0

        Dim accumulatedDelay As Single = 0
        Dim stopwatch As New Stopwatch()

        While _currentIndex < paths.Count
            If cToken.IsCancellationRequested Then Return 1

        FoldCompletedChunks(paths)

            ' --------- PAUSE GATE BEFORE STARTING THE LINE ----------
            ' --------- PAUSE GATE BEFORE STARTING THE LINE ----------
            While _IsPaused

                ' 1) Apply ALL queued step-backs
                Dim backCount = Interlocked.Exchange(_stepBackCount, 0)
                If backCount > 0 Then
                    For n As Integer = 1 To backCount
                        If _currentIndex <= 0 Then Exit For
                        _currentIndex -= 1
                        RemoveLineVisualOrUnfold(_currentIndex, paths)

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

                        backCount2 -= 1

                        ' 2) Any remaining back presses go to previous completed lines
                        For n As Integer = 1 To backCount2
                            If _currentIndex <= 0 Then Exit For
                            _currentIndex -= 1
                            RemoveLineVisualOrUnfold(_currentIndex, paths)

                            Dim ln2 = paths(_currentIndex)
                            UpdateCursor(New Point(ln2.X1, ln2.Y1))
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

        ShowStillToolpath()

        Return 0

    End Function

    Private _cursorVisual As DrawingVisual
    Private ReadOnly _cursorFill As Brush = Brushes.Transparent
    Private Const _cursorRadius As Double = 2.2
    Private Const _cursorCrosshairHalfSize As Double = 3.8

    Private Sub EnsureCursor()
        If _cursorVisual Is Nothing Then
            _cursorVisual = New DrawingVisual()
            visualHostCursor.AddVisual(_cursorVisual)
        End If
    End Sub


    Private Sub UpdateCursor(p As Point)
        If _cursorVisual Is Nothing Then Return
        Using dc = _cursorVisual.RenderOpen()
            dc.DrawEllipse(_cursorFill, _CursorPen, p, _cursorRadius, _cursorRadius)
            dc.DrawLine(_CursorPen, New Point(p.X - _cursorCrosshairHalfSize, p.Y), New Point(p.X + _cursorCrosshairHalfSize, p.Y))
            dc.DrawLine(_CursorPen, New Point(p.X, p.Y - _cursorCrosshairHalfSize), New Point(p.X, p.Y + _cursorCrosshairHalfSize))
        End Using
    End Sub

    Private Sub ClearCursor()
        If _cursorVisual Is Nothing Then Return
        Using dc = _cursorVisual.RenderOpen()
            ' draw nothing
        End Using
    End Sub

    Private Sub FoldCompletedChunks(paths As IReadOnlyList(Of GCodeLine))
        While (_nextFold + 1) * PlaybackChunk <= _currentIndex
            If Not _folded.ContainsKey(_nextFold) Then FoldChunk(_nextFold, paths)
            _nextFold += 1
        End While
    End Sub

    Private Sub FoldChunk(chunk As Integer, paths As IReadOnlyList(Of GCodeLine))
        Dim fromIndex As Integer = chunk * PlaybackChunk
        Dim toIndex As Integer = Math.Min(fromIndex + PlaybackChunk, paths.Count) - 1
        If fromIndex > toIndex Then Return

        Dim cut As New StreamGeometry()
        Dim anyCut As Boolean = False
        Using ctx = cut.Open()
            AppendRun(ctx, paths, fromIndex, toIndex, False, anyCut)
        End Using
        cut.Freeze()

        Dim travel As New StreamGeometry()
        Dim anyTravel As Boolean = False
        Using ctx = travel.Open()
            AppendRun(ctx, paths, fromIndex, toIndex, True, anyTravel)
        End Using
        travel.Freeze()

        Dim visuals As New List(Of DrawingVisual)()

        If anyCut Then
            Dim v As New DrawingVisual()
            Using dc = v.RenderOpen()
                dc.DrawGeometry(Nothing, _RenderPen, cut)
            End Using
            visualHost.AddVisual(v)
            visuals.Add(v)
        End If

        If anyTravel Then
            Dim v As New DrawingVisual()
            Using dc = v.RenderOpen()
                dc.DrawGeometry(Nothing, _TravelPen, travel)
            End Using
            If Not TravelMovesVisibilityToggle.IsChecked Then v.Opacity = 0
            visualHost.AddVisual(v)
            travelMoveVisuals.Add(v)
            visuals.Add(v)
        End If

        For i = fromIndex To toIndex
            Dim v = _lineVisuals(i)
            If v IsNot Nothing Then
                visualHost.RemoveVisual(v)
                travelMoveVisuals.Remove(v)
                _lineVisuals(i) = Nothing
            End If
        Next

        _folded(chunk) = visuals
    End Sub

    Private Sub UnfoldChunk(chunk As Integer, paths As IReadOnlyList(Of GCodeLine), keepUpTo As Integer)
        Dim visuals As List(Of DrawingVisual) = Nothing
        If Not _folded.TryGetValue(chunk, visuals) Then Return

        For Each v In visuals
            visualHost.RemoveVisual(v)
            travelMoveVisuals.Remove(v)
        Next
        _folded.Remove(chunk)
        _nextFold = Math.Min(_nextFold, chunk)

        Dim fromIndex As Integer = chunk * PlaybackChunk
        Dim toIndex As Integer = Math.Min(keepUpTo, Math.Min(fromIndex + PlaybackChunk, paths.Count)) - 1

        For i = fromIndex To toIndex
            Dim line = paths(i)
            Dim v As New DrawingVisual()
            Using dc = v.RenderOpen()
                dc.DrawLine(If(line.IsRapidMove, _TravelPen, _RenderPen), New Point(line.X1, line.Y1), New Point(line.X2, line.Y2))
            End Using
            If line.IsRapidMove Then
                travelMoveVisuals.Add(v)
                If Not TravelMovesVisibilityToggle.IsChecked Then v.Opacity = 0
            End If
            visualHost.AddVisual(v)
            _lineVisuals(i) = v
        Next
    End Sub

    'Hides line i again, reopening its group first if it was collapsed, so a step back is never a whole group.
    Private Sub RemoveLineVisualOrUnfold(i As Integer, paths As IReadOnlyList(Of GCodeLine))
        If _lineVisuals Is Nothing OrElse i < 0 OrElse i >= _lineVisuals.Count Then Return

        Dim chunk As Integer = i \ PlaybackChunk
        If _folded.ContainsKey(chunk) Then
            UnfoldChunk(chunk, paths, i)
        Else
            RemoveLineVisual(i)
        End If
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