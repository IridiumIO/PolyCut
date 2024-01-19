Imports System.Runtime.CompilerServices

Public Module Extensions

    <Extension()>
    Public Sub AddRange(Of T)(collection As ICollection(Of T), items As IEnumerable(Of T))
        If collection Is Nothing Then
            Throw New ArgumentNullException(NameOf(collection))
        End If

        If items IsNot Nothing Then
            For Each item In items
                collection.Add(item)
            Next
        End If
    End Sub


    <Extension()>
    Public Sub ForEach(Of T)(source As IEnumerable(Of T), action As Action(Of T))
            For Each item In source
                action(item)
            Next
        End Sub

End Module
