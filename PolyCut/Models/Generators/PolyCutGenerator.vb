
'Generator that uses PolyCut.Core
Imports PolyCut.Core

Imports WPF.Ui.Controls

Public Class PolyCutGenerator : Implements IGenerator

    Private Property Configuration As ProcessorConfiguration Implements IGenerator.Configuration
    Private Property Printer As Printer Implements IGenerator.Printer
    Private Property GCodes As List(Of GCode) Implements IGenerator.GCodes
    Private Property SVGText As String

    Public Async Function GenerateGcode() As Task(Of (StatusCode As Integer, Message As String)) Implements IGenerator.GenerateGcodeAsync


        Dim processedElements As List(Of List(Of Line)) = Await SVGProcessor.ProcessSVG(SVGText, Configuration)

        If processedElements.Count = 0 Then
            Return (1, "No paths on canvas")
        End If

        Dim processedlines As List(Of Line)

        Dim processorManager As New ProcessorManager(Configuration)
        processedlines = processorManager.Process(processedElements)

        Dim GCodeData = processorManager.GenerateGCode(processedlines)

        GCodes = GCodeData.GCodes

        Return (0, "")

    End Function

    Public Function GetGCode() As List(Of GCode) Implements IGenerator.GetGCode
        Return GCodes
    End Function

    Public Sub New(Configuration As ProcessorConfiguration, Printer As Printer, SVGText As String)
        Me.Configuration = Configuration
        Me.Printer = Printer
        Me.SVGText = SVGText
    End Sub

End Class
