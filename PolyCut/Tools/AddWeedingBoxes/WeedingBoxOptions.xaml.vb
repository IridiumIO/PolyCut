Partial Public Class WeedingBoxOptions
    Public Event RegenerateRequested()

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Sub Regenerate_Click(sender As Object, e As RoutedEventArgs)
        DirectCast(sender, UIElement).Focus()
        RaiseEvent RegenerateRequested()
    End Sub
End Class
