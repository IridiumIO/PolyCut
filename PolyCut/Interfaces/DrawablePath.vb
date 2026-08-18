Imports PolyCut.Shared

Imports Svg

Public Class DrawablePath : Inherits BaseDrawable : Implements IDrawable


    Public Overloads ReadOnly Property VisualName As String Implements IDrawable.VisualName
    Public Sub New(element As Path)
        DrawableElement = element
        VisualName = "Path"
        Name = VisualName
    End Sub

    Public Overrides Function DrawingToSVG() As SvgVisualElement Implements IDrawable.DrawingToSVG
        Dim ln = CType(DrawableElement, Path)

        Dim paths As Pathing.SvgPathSegmentList = SvgPathBuilder.Parse(ln.Data.ToString())

        Dim fillServer As SvgColourServer = CreateSvgFillServer()
        Dim strokeServer As SvgColourServer = CreateSvgStrokeServer()
        Dim strokeW As Single = If(strokeServer IsNot Nothing, CSng(Me.StrokeThickness), 0.001F)

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


End Class
