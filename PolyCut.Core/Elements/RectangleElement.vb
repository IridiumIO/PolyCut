﻿Imports Svg
Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes

Public Class RectangleElement : Implements IPathBasedElement


    Public ReadOnly Property FlattenedLines As List(Of Line) Implements IPathBasedElement.FlattenedLines
        Get
            Return Figures.SelectMany(Of Line)(Function(x) x).ToList
        End Get
    End Property
    Public Property Geo As PathGeometry Implements IPathBasedElement.Geo
    Public Property Config As ProcessorConfiguration Implements IPathBasedElement.Config
    Public Property Figures As New List(Of List(Of Line)) Implements IPathBasedElement.Figures

    Public Sub CompileFromSVGElement(element As SvgVisualElement, cfg As ProcessorConfiguration) Implements IPathBasedElement.CompileFromSVGElement
        Dim rect = DirectCast(element, SvgRectangle)
        Config = cfg
        If rect.CornerRadiusX <> 0 OrElse rect.CornerRadiusY <> 0 Then
            Throw New NotImplementedException("Rounded corners not implemented for rectangle objects. Convert to a path")
        End If

        Figures.Add(New List(Of Line) From {
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
                })

        Figures = Figures.Select(Function(fig) TransformLines(fig, element.Transforms.GetMatrix).ToList).ToList()

    End Sub



End Class

