Class ExportPage

    Public Property ViewModel As MainViewModel
    Sub New(_viewmodel As MainViewModel)
        ViewModel = _viewmodel

        Me.DataContext = ViewModel

        ' This call is required by the designer.
        InitializeComponent()


    End Sub

End Class
