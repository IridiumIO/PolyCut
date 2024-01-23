Public Module Helpers

    Public Function BuildURI(address As String, Optional DestinationPort As Integer = Nothing) As Uri

        If Not address.StartsWith("http://") AndAlso Not address.StartsWith("https://") Then
            address = "http://" & address
        End If

        If DestinationPort <> Nothing Then
            address &= ":" & DestinationPort.ToString
        End If

        Return New Uri(address)

    End Function
End Module
