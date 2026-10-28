Imports System.Threading

Partial Public Class WeedingBoxOptions
    Public Property Regenerate As Func(Of CancellationToken, Task)
    Public ReadOnly Property RegenerationTask As Task
        Get
            Return _regeneration
        End Get
    End Property

    Private _regeneration As Task = Task.CompletedTask
    Private _cancellation As CancellationTokenSource

    Public Sub New()
        InitializeComponent()
    End Sub

    Public Sub CancelRegeneration()
        _cancellation?.Cancel()
    End Sub

    Private Async Sub Regenerate_Click(sender As Object, e As RoutedEventArgs)
        If _cancellation IsNot Nothing Then
            CancelRegeneration()
            Return
        End If
        If Regenerate Is Nothing Then Return
        DirectCast(sender, UIElement).Focus()
        _regeneration = RunRegenerationAsync()
        Await _regeneration
    End Sub

    Private Async Function RunRegenerationAsync() As Task
        Using cancellation As New CancellationTokenSource()
            _cancellation = cancellation
            SetBusy(True)
            Try
                Await Regenerate(cancellation.Token)
            Catch ex As OperationCanceledException
                ' Keep the last completed preview.
            Finally
                _cancellation = Nothing
                SetBusy(False)
            End Try
        End Using
    End Function

    Private Sub SetBusy(busy As Boolean)
        SettingsPanel.IsEnabled = Not busy
        RegenerateButton.Content = If(busy, "Cancel regeneration".LT(), "Regenerate Weeding Boxes".LT())
        TryCast(Window.GetWindow(Me), ToolConfigurationWindow)?.SetBusy(busy)
    End Sub
End Class
