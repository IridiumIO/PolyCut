Public Module Helpers

    Public Function BuildURI(address As String, Optional DestinationPort As Integer = Nothing) As Uri

        If Not address.StartsWith("http://") AndAlso Not address.StartsWith("https://") Then
            address = "http://" & address
        End If

        If DestinationPort <> Nothing Then
            address &= ":" & DestinationPort.ToString
        End If
        Try

            Return New Uri(address)
        Catch ex As Exception
            Return Nothing
        End Try

    End Function
End Module
