Imports Svg
Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes

Public Class RectangleElement : Implements IPathBasedElement


    Public Property Lines As List(Of Line) Implements IPathBasedElement.Lines
    Public Property Geo As PathGeometry Implements IPathBasedElement.Geo
    Public Property Config As ProcessorConfiguration Implements IPathBasedElement.Config

    Public Sub CompileElement(element As SvgVisualElement, cfg As ProcessorConfiguration) Implements IPathBasedElement.CompileElement
        Dim rect = DirectCast(element, SvgRectangle)
        Config = cfg
        If rect.CornerRadiusX <> 0 OrElse rect.CornerRadiusY <> 0 Then
            Throw New NotImplementedException("Rounded corners not implemented for rectangle objects. Convert to a path")
        End If

        Lines = New List(Of Line) From {
                    New Line With {
                        .X1 = rect.X,
                        .Y1 = rect.Y,
                        .X2 = rect.X.Value + rect.Width.Value,
                        .Y2 = rect.Y
                    },
                    New Line With {
                        .X1 = rect.X.Value + rect.Width.Value,
                        .Y1 = rect.Y,
                        .X2 = rect.X.Value + rect.Width.Value,
                        .Y2 = rect.Y.Value + rect.Height.Value
                    },
                    New Line With {
                        .X1 = rect.X.Value + rect.Width.Value,
                        .Y1 = rect.Y.Value + rect.Height.Value,
                        .X2 = rect.X,
                        .Y2 = rect.Y.Value + rect.Height.Value
                    },
                    New Line With {
                        .X1 = rect.X,
                        .Y1 = rect.Y.Value + rect.Height.Value,
                        .X2 = rect.X,
                        .Y2 = rect.Y
                    }
                }

        Lines = TransformLines(Lines, element.Transforms.GetMatrix)
        Lines = OffsetProcessor.ProcessOffsets(Lines, Config.Offset, Config.Overcut)


    End Sub



End Class

