Public Class EventAggregator

    Private Shared Subscribers As New List(Of Action(Of Object))

    Public Shared Sub Subscribe(subscriber As Action(Of Object))
        Subscribers.Add(subscriber)
    End Sub

    Public Shared Sub Publish(message As Object)
        For Each subscriber In Subscribers
            subscriber(message)
        Next
    End Sub


End Class
