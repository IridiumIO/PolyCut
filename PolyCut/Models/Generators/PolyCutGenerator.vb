
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


        Dim config = Configuration.Clone()
        Dim snapshots = DrawableObjects.Where(Function(drawable) Not TypeOf drawable Is DrawableGroup).
            SelectMany(Function(drawable) GeometryExtractor.CaptureFromDrawable(drawable, config, MainCanvas)).ToList()
        Dim codes = Await Task.Run(Function()
                                   Dim processedElements = snapshots.SelectMany(Function(item) GeometryExtractor.ProcessSnapshot(item, config)).ToList()
                                   If processedElements.Count = 0 Then Return Nothing
                                   Dim processorManager As New ProcessorManager(config)
                                   Dim processedLines = processorManager.Process(processedElements)
                                   Return processorManager.GenerateGCode(processedLines).GCodes
                               End Function)
        If codes Is Nothing Then Return (1, "No paths on canvas".LT())
        GCodes = codes

        Return (0, "")

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
