Imports System.Windows.Threading

Friend NotInheritable Class InteractionRateLimiter
    Private ReadOnly _timer As DispatcherTimer
    Private ReadOnly _apply As Action
    Private _pending As Boolean

    Public Sub New(apply As Action, priority As DispatcherPriority)
        _apply = apply
        _timer = New DispatcherTimer(priority) With {.Interval = TimeSpan.FromMilliseconds(17)}
        AddHandler _timer.Tick, AddressOf OnTick
    End Sub

    Public Sub Request()
        If _timer.IsEnabled Then
            _pending = True
        Else
            _timer.Start()
            _apply()
        End If
    End Sub

    Private Sub OnTick(sender As Object, e As EventArgs)
        If _pending Then
            _pending = False
            _apply()
        Else
            _timer.Stop()
        End If
    End Sub

    Public Sub Flush()
        Cancel()
        _apply()
    End Sub

    Public Sub Cancel()
        _timer.Stop()
        _pending = False
    End Sub
End Class
