Imports System.Diagnostics
Imports System.Windows.Media
Imports System.Windows.Threading


Friend NotInheritable Class FrameMeter

    Private Const SampleMs As Double = 500.0
    Private Const IdleMs As Long = 1000

    Private ReadOnly _clock As Stopwatch = Stopwatch.StartNew()
    Private _timer As DispatcherTimer
    Private _hooked As Boolean
    Private _lastFrameMs As Double = -1
    Private _frameSum As Double
    Private _frameCount As Integer
    Private _frameMax As Double
    Private _lastActivityMs As Long
    Private _hasPulsed As Boolean

    Public Sub Start()
        If _timer IsNot Nothing Then Return
        _timer = New DispatcherTimer(DispatcherPriority.Background) With {.Interval = TimeSpan.FromMilliseconds(SampleMs)}
        AddHandler _timer.Tick, AddressOf OnSample
        _timer.Start()
    End Sub


    Public Sub Pulse()
        _hasPulsed = True
        _lastActivityMs = _clock.ElapsedMilliseconds
        If Not _hooked Then
            AddHandler CompositionTarget.Rendering, AddressOf OnFrame
            _hooked = True
        End If
    End Sub

    Private Sub OnFrame(sender As Object, e As EventArgs)
        Dim now = _clock.Elapsed.TotalMilliseconds
        If _lastFrameMs >= 0 Then
            Dim delta = now - _lastFrameMs
            If delta > 0 Then
                _frameSum += delta
                _frameCount += 1
                If delta > _frameMax Then _frameMax = delta
            End If
        End If
        _lastFrameMs = now
    End Sub

    Private Sub OnSample(sender As Object, e As EventArgs)
        Dim now = _clock.ElapsedMilliseconds

        If Not _hasPulsed OrElse now - _lastActivityMs > IdleMs Then
            If _hooked Then
                RemoveHandler CompositionTarget.Rendering, AddressOf OnFrame
                _hooked = False
            End If
            _lastFrameMs = -1
            _frameSum = 0
            _frameCount = 0
            _frameMax = 0
            Return
        End If

        If _frameCount = 0 Then Return

        Dim average As Double = _frameSum / _frameCount
        Dim worst As Double = _frameMax
        _frameSum = 0
        _frameCount = 0
        _frameMax = 0

        Debug.WriteLine(String.Format("frame {0,7:F2} ms avg {1,7:F2} ms max {2,6:F1} fps", average, worst, If(average > 0, 1000.0 / average, 0.0)))
    End Sub
End Class
