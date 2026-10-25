Partial Public Class ToolConfigurationWindow
    Public Sub New(title As String, options As FrameworkElement)
        InitializeComponent()
        Me.Title = title
        OptionsHost.Content = options
    End Sub

    Private Sub Save_Click(sender As Object, e As RoutedEventArgs)
        DirectCast(sender, UIElement).Focus()
        DialogResult = True
    End Sub
End Class
