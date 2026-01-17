Public Class EventAggregator
    Private Shared ReadOnly Subscribers As New Dictionary(Of Type, List(Of Action(Of Object)))()

    Public Shared Sub Subscribe(Of T)(handler As Action(Of T))
        Dim key = GetType(T)
        SyncLock Subscribers
            If Not Subscribers.ContainsKey(key) Then
                Subscribers(key) = New List(Of Action(Of Object))()
            End If
            Dim wrapper As Action(Of Object) = Sub(m) handler(DirectCast(m, T))
            Subscribers(key).Add(wrapper)
        End SyncLock
    End Sub

    Public Shared Sub Publish(Of T)(message As T)
        Dim msgType = GetType(T)
        Dim toInvoke As List(Of Action(Of Object)) = Nothing
        SyncLock Subscribers

            toInvoke = Subscribers.Where(Function(kvp) kvp.Key.IsAssignableFrom(msgType)) _
                                  .SelectMany(Function(kvp) kvp.Value).ToList()
        End SyncLock

        For Each h In toInvoke
            h(message)
        Next
    End Sub
End Class