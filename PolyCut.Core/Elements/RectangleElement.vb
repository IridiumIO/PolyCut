Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes

Imports PolyCut.[Shared]

Imports Svg

Public Class RectangleElement : Implements IPathBasedElement


    Public ReadOnly Property FlattenedLines As List(Of GeoLine) Implements IPathBasedElement.FlattenedLines
        Get
            Return Figures.SelectMany(Of GeoLine)(Function(x) x).ToList
        End Get
    End Property
    Public Property Figures As New List(Of List(Of GeoLine)) Implements IPathBasedElement.Figures
    Public Property IsFilled As Boolean = False Implements IPathBasedElement.IsFilled
    Public Property FillColor As String Implements IPathBasedElement.FillColor


End Class

