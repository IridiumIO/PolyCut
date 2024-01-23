Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Text
Public Class Moonraker : Implements INetworkPrinter

    Public Property Name As String Implements INetworkPrinter.Name
    Public Property UploadURL As String Implements INetworkPrinter.UploadURL
    Public Property AutoPrint As Boolean Implements INetworkPrinter.AutoPrint

    Public Function SendGcode(gcode As String) As Integer Implements INetworkPrinter.SendGcode

        Dim endPath = UploadURL.TrimEnd("/") & "/server/files/upload"

        Dim virtualFile As New MemoryStream(Encoding.UTF8.GetBytes(gcode))

        Using client As New HttpClient

            Using formdata As New MultipartFormDataContent

                formdata.Add(New StreamContent(virtualFile), "file", "virtual.gcode")
                formdata.Add(New StringContent(AutoPrint.ToString.ToLower), "print")
                Dim response As HttpResponseMessage = client.PostAsync(endPath, formdata).Result
                If response.StatusCode <> HttpStatusCode.Created Then
                    Return 1
                End If
            End Using


        End Using

        Return 0

    End Function

End Class
