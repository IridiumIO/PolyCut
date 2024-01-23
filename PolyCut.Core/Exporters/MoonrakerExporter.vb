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


    Public Async Function Export(gcodes As List(Of GCode), fileName As String) As Task(Of Integer) Implements INetworkExporter.Export

        Dim flattenedGCode As String = ""
        For Each gcode In gcodes
            flattenedGCode &= gcode.ToString & Environment.NewLine
        Next

        Dim virtualFile As New MemoryStream(Encoding.UTF8.GetBytes(flattenedGCode))

        Using client As New HttpClient With {.BaseAddress = BuildURI(DestinationHost, DestinationPort)}

            Using formdata As New MultipartFormDataContent

                formdata.Add(New StreamContent(virtualFile), "file", $"{fileName}")
                formdata.Add(New StringContent(AutoPrint.ToString.ToLower), "print")
                Dim response As HttpResponseMessage = Await client.PostAsync(UploadEndpoint, formdata)
                If response.StatusCode = HttpStatusCode.Created Then
                    Return 0
                End If

            End Using

        End Using

        Return 1

    End Function


End Class
