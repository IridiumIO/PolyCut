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
    Public Property Geo As PathGeometry Implements IPathBasedElement.Geo
    Public Property Config As ProcessorConfiguration Implements IPathBasedElement.Config
    Public Property Figures As New List(Of List(Of GeoLine)) Implements IPathBasedElement.Figures
    Public Property IsFilled As Boolean = False Implements IPathBasedElement.IsFilled
    Public Sub CompileFromSVGElement(element As SvgVisualElement, cfg As ProcessorConfiguration) Implements IPathBasedElement.CompileFromSVGElement
        Dim rect = DirectCast(element, SvgRectangle)
        Config = cfg
        If rect.CornerRadiusX <> 0 OrElse rect.CornerRadiusY <> 0 Then
            Throw New NotImplementedException("Rounded corners not implemented for rectangle objects. Convert to a path")
        End If



        Dim fillcolor = ColorAndBrushHelpers.SVGPaintServerToString(element.Fill)

        Figures.Add(New List(Of GeoLine) From {
                    New GeoLine(rect.X, rect.Y, rect.X.Value + rect.Width.Value, rect.Y),
                    New GeoLine(rect.X.Value + rect.Width.Value, rect.Y, rect.X.Value + rect.Width.Value, rect.Y.Value + rect.Height.Value),
                    New GeoLine(rect.X.Value + rect.Width.Value, rect.Y.Value + rect.Height.Value, rect.X, rect.Y.Value + rect.Height.Value),
                    New GeoLine(rect.X, rect.Y.Value + rect.Height.Value, rect.X, rect.Y)
                })

        Figures = Figures.Select(Function(fig) TransformLines(fig, element.Transforms.GetMatrix).ToList()).ToList()

        For fi = 0 To Figures.Count - 1
            For li = 0 To Figures(fi).Count - 1
                Dim ln = Figures(fi)(li)
                ln = ln.WithTag(fillcolor)
                Figures(fi)(li) = ln
            Next
        Next
    End Sub



End Class

