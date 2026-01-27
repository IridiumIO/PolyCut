Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Text

Public Class MoonrakerExporter : Implements INetworkExporter

    Private ReadOnly Property ProcessorConfiguration As ProcessorConfiguration Implements INetworkExporter.ProcessorConfiguration
    Public Property DestinationHost As String Implements INetworkExporter.DestinationHost
    Public Property DestinationPort As Integer Implements INetworkExporter.DestinationPort
    Public Property UploadEndpoint As String = "/server/files/upload" Implements INetworkExporter.UploadEndpoint
    Public Property AutoPrint As Boolean Implements INetworkExporter.AutoPrint

    Public Sub New(config As ProcessorConfiguration)
        ProcessorConfiguration = config
        DestinationHost = config.ExportConfig.DestinationIP
        DestinationPort = config.ExportConfig.DestinationPort
        AutoPrint = config.ExportConfig.AutoPrint

    End Sub

    Public Async Function Export(gcodes As String, fileName As String) As Task(Of Integer)
        Dim virtualFile As New MemoryStream(Encoding.UTF8.GetBytes(gcodes))

        Using client As New HttpClient With {.BaseAddress = BuildURI(DestinationHost, DestinationPort)}

            Using formdata As New MultipartFormDataContent
                Try
                    formdata.Add(New StreamContent(virtualFile), "file", $"{fileName}")
                    formdata.Add(New StringContent(AutoPrint.ToString.ToLower), "print")
                    Dim response As HttpResponseMessage = Await client.PostAsync(UploadEndpoint, formdata)
                    Return If(response.StatusCode = HttpStatusCode.Created, 0, response.StatusCode)
                Catch ex As HttpRequestException
                    Return -1
                End Try
            End Using
        End Using

        Return 1

    End Function

    Public Async Function Export(gcodes As List(Of GCode), fileName As String) As Task(Of Integer) Implements INetworkExporter.Export

        If gcodes Is Nothing OrElse gcodes.Count = 0 Then Return 418

        Dim sb As New StringBuilder()
        For Each gcode In gcodes
            sb.AppendLine(gcode.ToString)
        Next
        Dim flattenedGCode As String = sb.ToString()

        Return Await Export(flattenedGCode, fileName)
    End Function


End Class
