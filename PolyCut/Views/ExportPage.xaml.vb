Class ExportPage


    Sub OnNavigatedTo(e As NavigationEventArgs)

        Me.DataContext = e.ExtraData

    End Sub

End Class
