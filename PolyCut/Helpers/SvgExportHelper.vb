Imports PolyCut.RichCanvas
Imports PolyCut.Shared

Imports Svg
Imports Svg.Transforms


'Unified SVG export helper. The WPF visual tree (TransformToVisual) is the single accumulator for geometry.
'Groups stay as organizational SvgGroups.
Public Module SvgExportHelper


    'Bakes the leaf's local SVG geometry to document-root space.
    Public Function BakeToRoot(localSvg As SvgVisualElement, element As FrameworkElement, Optional root As UIElement = Nothing) As SvgVisualElement

        Dim component As SvgVisualElement = If(localSvg IsNot Nothing, localSvg.DeepCopy(), Nothing)
        If component Is Nothing OrElse element Is Nothing Then Return component

        If root Is Nothing Then root = FindDocumentRoot(element)
        If root Is Nothing Then Return component

        Dim m As Matrix = Matrix.Identity

        '1) Fit source path geometry to its geometric layout size.
        'FUTURE ME - always use the WPF geometry bounds (Path.Data.Bounds), NOT the SVG library's component.Bounds which pads by half the stroke width!!! You always get tripped up by this!!!! IT DOES NOT WORK DON'T WASTE TIME CHANGING IT AGAIN
        Dim geometric = TryCast(element, GeometricShape)
        If geometric IsNot Nothing Then
            If geometric.Kind = "Path" Then
                m = If(geometric.Data.Transform?.Value, Matrix.Identity)
                m.Append(geometric.BoundsTransform)
            End If
        End If

        '2) Element ?>> document root via the visual tree (canvas position, wrapper rotation, element mirror scale, viewbox scaling, etc).
        Dim t2v = TransformMath.GetAccumulatedMatrix(element, root)
        m = Matrix.Multiply(m, t2v)

        ApplySvgMatrix(component, m)
        Return component
    End Function


    Public Function FindDocumentRoot(element As FrameworkElement) As UIElement
        If element Is Nothing Then Return Nothing

        Dim current As DependencyObject = element
        Dim topCanvas As Canvas = Nothing
        Dim root As UIElement = Nothing

        While current IsNot Nothing
            Dim c = TryCast(current, Canvas)
            If c IsNot Nothing Then
                topCanvas = c
                If TypeOf c Is PolyCanvas Then
                    root = c
                    Exit While
                End If
            End If
            root = TryCast(current, UIElement)
            current = VisualTreeHelper.GetParent(current)
        End While

        Return If(root IsNot Nothing, root, topCanvas)
    End Function


    Public Sub ApplySvgMatrix(svgElem As SvgVisualElement, m As Matrix)
        If svgElem.Transforms Is Nothing Then svgElem.Transforms = New SvgTransformCollection()

        Dim values As New List(Of Single) From {
            CSng(m.M11), CSng(m.M12),
            CSng(m.M21), CSng(m.M22),
            CSng(m.OffsetX), CSng(m.OffsetY)
        }

        svgElem.Transforms.Insert(0, New SvgMatrix(values))
    End Sub

End Module
