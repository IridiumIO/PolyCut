Imports System.Collections.Generic
Imports System.Windows
Imports System.Windows.Media

Public Class ProjectData
        Public Property Version As String = "1.0"
        Public Property CreatedDate As DateTime = DateTime.Now
        Public Property ModifiedDate As DateTime = DateTime.Now
        Public Property Drawables As List(Of DrawableData) = New List(Of DrawableData)
        Public Property Groups As List(Of GroupData) = New List(Of GroupData)
    End Class


    Public Class DrawableData
        Public Property Id As Guid = Guid.NewGuid()
        Public Property Name As String
        Public Property Type As String ' "Path", "Rectangle", "Ellipse", "Text", "Line"
        Public Property Left As Double
        Public Property Top As Double
        Public Property Width As Double
        Public Property Height As Double
        Public Property RotationAngle As Double
        Public Property ScaleX As Double = 1.0
        Public Property ScaleY As Double = 1.0
        Public Property IsHidden As Boolean
        Public Property ZIndex As Integer

        ' Visual properties
        Public Property StrokeColor As String
        Public Property StrokeThickness As Double
        Public Property FillColor As String

        ' Type-specific data
        Public Property PathData As String ' For Path elements
        Public Property TextContent As String ' For TextBox
        Public Property FontFamily As String
        Public Property FontSize As Double
        Public Property LineX1 As Double ' For Line
        Public Property LineY1 As Double
        Public Property LineX2 As Double
        Public Property LineY2 As Double

        ' Group membership
        Public Property ParentGroupId As Guid?
    End Class


Public Class GroupData
    Public Property Id As Guid = Guid.NewGuid()
    Public Property Name As String

    ' "DrawableGroup" or "NestedDrawableGroup"
    Public Property GroupType As String = "DrawableGroup"

    ' Hierarchy
    Public Property ChildIds As List(Of Guid) = New List(Of Guid)
    Public Property ParentGroupId As Guid?

    ' Visual state for the group's WRAPPER (so transforms persist)
    Public Property Left As Double
    Public Property Top As Double
    Public Property Width As Double
    Public Property Height As Double
    Public Property RotationAngle As Double
    Public Property IsHidden As Boolean
    Public Property ZIndex As Integer

    ' For NestedDrawableGroup with Viewbox: inner-canvas "native" size (pre-scale)
    Public Property NativeWidth As Double
    Public Property NativeHeight As Double
End Class

Public Class RuntimeProjectModel
    Public Property Drawables As List(Of IDrawable)
    Public Property Groups As List(Of IDrawable)

    ' Exact lookup by saved IDs:
    Public Property DrawableById As Dictionary(Of Guid, IDrawable)
    Public Property GroupById As Dictionary(Of Guid, IDrawable)
End Class

