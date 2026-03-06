Imports PolyCut.Shared

Imports Svg
Imports Svg.Transforms

Public Class DrawablePath : Inherits BaseDrawable : Implements IDrawable


    Public Overloads ReadOnly Property VisualName As String Implements IDrawable.VisualName
    Public Sub New(element As Path)
        DrawableElement = element
        VisualName = "Path"
        Name = VisualName
    End Sub

    Public Overloads Function DrawingToSVG() As SvgVisualElement Implements IDrawable.DrawingToSVG
        Dim ln = CType(DrawableElement, Path)

        Dim paths As Pathing.SvgPathSegmentList = SvgPathBuilder.Parse(ln.Data.ToString())

        Dim fillServer As SvgColourServer = Nothing
        Dim strokeServer As SvgColourServer = Nothing
        Dim strokeW As Single = 0.001F

        Try
            fillServer = ColorAndBrushHelpers.BrushToSvgColourServer(Me.Fill)
        Catch
        End Try

        ' Only set stroke if thickness > 0 and stroke is not Nothing
        If Me.StrokeThickness > 0.001 AndAlso Me.Stroke IsNot Nothing Then
            Try
                strokeServer = ColorAndBrushHelpers.BrushToSvgColourServer(Me.Stroke)
                strokeW = CSng(Me.StrokeThickness)
            Catch
            End Try
        End If

        Dim svgPath As New SvgPath With {
            .PathData = paths,
            .Stroke = SvgPaintServer.None,
            .Fill = If(fillServer, SvgPaintServer.None)
        }

        Dim d As String = ln.Data.ToString()
        If svgPath.Fill IsNot SvgPaintServer.None Then
            d = CloseSvgPathData(d)
        End If
        svgPath.PathData = SvgPathBuilder.Parse(d)


        ' Only set stroke properties if we have a stroke
        If strokeServer IsNot Nothing Then
            svgPath.Stroke = strokeServer
            svgPath.StrokeWidth = strokeW
        End If

        Return svgPath
    End Function

    Private Function CloseSvgPathData(d As String) As String
        If String.IsNullOrWhiteSpace(d) Then Return d

        ' Ensure each subpath (after an M/m) ends with Z/z before the next M/m or end of string
        Dim sb As New System.Text.StringBuilder()
        Dim i As Integer = 0

        Dim inSubpath As Boolean = False
        Dim subpathClosed As Boolean = False

        While i < d.Length
            Dim ch As Char = d(i)

            If ch = "M"c OrElse ch = "m"c Then
                If inSubpath AndAlso Not subpathClosed Then sb.Append(" Z ")
                inSubpath = True
                subpathClosed = False
            ElseIf ch = "Z"c OrElse ch = "z"c Then
                subpathClosed = True
            End If

            sb.Append(ch)
            i += 1
        End While

        If inSubpath AndAlso Not subpathClosed Then sb.Append(" Z")
        Return sb.ToString()
    End Function


    Public Overloads Function GetTransformedSVGElement() As SvgVisualElement Implements IDrawable.GetTransformedSVGElement

        Dim component As SvgVisualElement = DrawingToSVG().DeepCopy

        Return BakeTransforms(component, DrawableElement, 0, 0, False)

    End Function

    Private Function BakeTransforms(SVGelement As SvgVisualElement, drawableElement As FrameworkElement, Optional LCorrection As Double = 0, Optional TCorrection As Double = 0, Optional IgnoreDrawableScale As Boolean = False) As SvgVisualElement

        Dim component As SvgVisualElement = SVGelement.DeepCopy()
        If component.Transforms Is Nothing Then component.Transforms = New SvgTransformCollection()

        Dim container As ContentControl = TryCast(drawableElement.Parent, ContentControl)
        If container Is Nothing Then Return component

        Dim originX As Double = Canvas.GetLeft(container) - LCorrection
        Dim originY As Double = Canvas.GetTop(container) - TCorrection

        Dim width As Double = If(Double.IsNaN(container.Width), container.ActualWidth, container.Width)
        Dim height As Double = If(Double.IsNaN(container.Height), container.ActualHeight, container.Height)

        ' Canonical geometry bounds (no stroke, no layout)
        Dim path = CType(drawableElement, Path)
        Dim gb As Rect = path.Data.Bounds

        Dim bw As Double = If(gb.Width <= 0, 0.0001, gb.Width)
        Dim bh As Double = If(gb.Height <= 0, 0.0001, gb.Height)

        Dim sx As Double = width / bw
        Dim sy As Double = height / bh

        ' Build a single SVG matrix:
        ' 1) move geometry to origin (-gb.X, -gb.Y)
        ' 2) scale into wrapper (sx, sy)
        ' 3) translate to canvas position (originX, originY)
        Dim m As New Matrix()
        m.Translate(-gb.X, -gb.Y)
        m.Scale(sx, sy)
        m.Translate(originX, originY)

        ' Optional: apply RenderTransform ScaleTransform (mirror) about wrapper center
        Dim tfg As TransformGroup = TryCast(drawableElement.RenderTransform, TransformGroup)
        If tfg IsNot Nothing Then
            For Each t As Transform In tfg.Children
                Dim st = TryCast(t, ScaleTransform)
                If st IsNot Nothing Then
                    m.ScaleAt(st.ScaleX, st.ScaleY, originX + width / 2, originY + height / 2)
                End If
            Next
        End If

        ' Rotate wrapper around its center (uses wrapper size, not SVG bounds)
        Dim rot = TryCast(container.RenderTransform, RotateTransform)
        If rot IsNot Nothing Then
            m.RotateAt(rot.Angle, originX + width / 2, originY + height / 2)
        End If

        component.Transforms.Insert(0, New SvgMatrix(New List(Of Single) From {
        CSng(m.M11), CSng(m.M12),
        CSng(m.M21), CSng(m.M22),
        CSng(m.OffsetX), CSng(m.OffsetY)
    }))

        Return component
    End Function


End Class
