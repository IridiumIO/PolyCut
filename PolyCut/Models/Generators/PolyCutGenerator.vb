
Imports System.Windows

Imports PolyCut.Core
Imports PolyCut.Shared

Public Class PolyCutGenerator : Implements IGenerator

    Private Property Configuration As ProcessorConfiguration Implements IGenerator.Configuration
    Private Property Printer As Printer Implements IGenerator.Printer
    Private Property GCodes As List(Of GCode) Implements IGenerator.GCodes
    Private Property DrawableObjects As List(Of IDrawable)
    Private Property MainCanvas As UIElement

    Public Async Function GenerateGcode() As Task(Of (StatusCode As Integer, Message As String)) Implements IGenerator.GenerateGcodeAsync


        Dim processedElements = GeneratePathBasedElements()

        If processedElements.Count = 0 Then
            Return (1, "No paths on canvas")
        End If

        Dim processedlines As List(Of GeoLine)

        Dim processorManager As New ProcessorManager(Configuration)
        processedlines = processorManager.Process(processedElements)

        Dim GCodeData = processorManager.GenerateGCode(processedlines)

        GCodes = GCodeData.GCodes

        Return (0, "")

    End Function

    Public Function GeneratePathBasedElements() As List(Of IPathBasedElement)
        Dim elements As New List(Of IPathBasedElement)

        For Each drawable In DrawableObjects
            If TypeOf drawable Is DrawableGroup Then Continue For

            Dim pathElements = GeometryExtractor.ExtractFromDrawable(drawable, Configuration, MainCanvas)
            If pathElements IsNot Nothing AndAlso pathElements.Count > 0 Then
                elements.AddRange(pathElements)
            End If
        Next

        Return elements
    End Function

    Public Function GetGCode() As List(Of GCode) Implements IGenerator.GetGCode
        Return GCodes
    End Function

    Public Sub New(Configuration As ProcessorConfiguration, Printer As Printer, DrawableObjects As List(Of IDrawable), MainCanvas As UIElement)
        Me.Configuration = Configuration
        Me.Printer = Printer
        Me.DrawableObjects = DrawableObjects
        Me.MainCanvas = MainCanvas
    End Sub

End Class
