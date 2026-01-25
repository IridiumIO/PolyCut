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
            fillServer = SvgHelpers.BrushToSvgColourServer(Me.Fill)
        Catch
        End Try
        
        ' Only set stroke if thickness > 0 and stroke is not Nothing
        If Me.StrokeThickness > 0.001 AndAlso Me.Stroke IsNot Nothing Then
            Try
                strokeServer = SvgHelpers.BrushToSvgColourServer(Me.Stroke)
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

        Dim container As ContentControl = CType(drawableElement.Parent, ContentControl)

        Dim matrix As New Matrix()

        Dim originX As Double = Canvas.GetLeft(container)
        Dim originY As Double = Canvas.GetTop(container)
        Dim width As Double = container.ActualWidth
        Dim height As Double = container.ActualHeight

        ' Scale

        Dim matrixTransform As MatrixTransform = CType(drawableElement, Path).GeometryTransform
        Dim matrix2 As Matrix = matrixTransform.Value

        ' Extract the scale components
        Dim scaleX As Double = matrix2.M11
        Dim scaleY As Double = matrix2.M22
        Debug.WriteLine($"ScaleX: {scaleX}, ScaleY: {scaleY}")

        matrix = CType(drawableElement, Path).GeometryTransform.Value

        ' Translate
        matrix.Translate(originX - LCorrection, originY - TCorrection)

        Dim tfg As TransformGroup = TryCast(drawableElement.RenderTransform, TransformGroup)
        If tfg IsNot Nothing Then
            For Each transform As Transform In tfg.Children
                If TypeOf transform Is ScaleTransform Then
                    Dim st = CType(transform, ScaleTransform)
                    matrix.ScaleAt(st.ScaleX, st.ScaleY, originX + width / 2, originY + height / 2)
                End If
            Next
        End If

        ' Rotate if present
        If TypeOf container.RenderTransform Is RotateTransform Then
            Dim rt = CType(container.RenderTransform, RotateTransform)
            matrix.RotateAt(rt.Angle, originX + width / 2, originY + height / 2)
        End If

        ' Apply transform
        Dim values As New List(Of Single) From {
            CSng(matrix.M11), CSng(matrix.M12),
            CSng(matrix.M21), CSng(matrix.M22),
            CSng(matrix.OffsetX), CSng(matrix.OffsetY)
        }
        component.Transforms.Insert(0, New SvgMatrix(values))

        Debug.WriteLine($"Bounds.X: {component.Bounds.X}, Y: {component.Bounds.Y}, Width: {component.Bounds.Width}, Height: {component.Bounds.Height}")

        Return component
    End Function


End Class
