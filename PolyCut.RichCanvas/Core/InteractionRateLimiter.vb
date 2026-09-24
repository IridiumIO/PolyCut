Imports System.Windows.Media

Friend NotInheritable Class InteractionRateLimiter
    Private ReadOnly _apply As Action
    Private _pending As Boolean
    Private _hooked As Boolean

    Public Sub New(apply As Action)
        _apply = apply
    End Sub

    Public Sub Request()
        _pending = True
        If Not _hooked Then
            AddHandler CompositionTarget.Rendering, AddressOf OnRendering
            _hooked = True
        End If
    End Sub

    Private Sub OnRendering(sender As Object, e As EventArgs)
        If Not _pending Then
            Unhook()
            Return
        End If
        _pending = False
        _apply()
    End Sub

    Public Sub Flush()
        Unhook()
        _pending = False
        _apply()
    End Sub

    Public Sub Cancel()
        Unhook()
        _pending = False
    End Sub

    Private Sub Unhook()
        If Not _hooked Then Return
        RemoveHandler CompositionTarget.Rendering, AddressOf OnRendering
        _hooked = False
    End Sub
End Class
