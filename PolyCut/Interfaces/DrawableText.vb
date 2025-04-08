

Imports Svg
Imports Svg.Pathing
Imports Svg.Transforms

Public Class DrawableText : Inherits BaseDrawable : Implements IDrawable


    Public ReadOnly Property VisualName As String Implements IDrawable.VisualName
    Public Sub New(element As TextBox)
        DrawableElement = element
        VisualName = "Text"
    End Sub


    Public Overloads Function DrawingToSVG() As SvgVisualElement Implements IDrawable.DrawingToSVG
        Dim tb As TextBox = CType(DrawableElement, TextBox)
        Dim formattedText As New FormattedText(
            tb.Text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            New Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch),
            tb.FontSize,
            Brushes.Black,
            New NumberSubstitution(),
            1.0
        )


        Dim tabWidth As Double = New FormattedText(
            vbTab, ' 4 spaces
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            New Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch),
            tb.FontSize,
            Brushes.Black
        ).Width

        Debug.WriteLine($"Tab width: {tabWidth}")

        Dim baselineOffset = formattedText.Baseline

        Dim svgText As New Svg.SvgText With {
        .X = New SvgUnitCollection From {0},
        .Y = New SvgUnitCollection From {CSng(baselineOffset)},
        .Text = tb.Text,
        .FontFamily = tb.FontFamily.Source,
        .FontSize = tb.FontSize,
        .FontWeight = SvgFontWeight.Normal,
        .Fill = New Svg.SvgColourServer(System.Drawing.Color.Black),
        .TextAnchor = SvgTextAnchor.Start,
        .FontStyle = SvgFontStyle.Normal,
        .LengthAdjust = SvgTextLengthAdjust.Spacing
    }

        svgText.Text = Nothing

        Dim substrings As String() = tb.Text.Split(vbTab)
        Dim currentX As Double = 0

        For i As Integer = 0 To substrings.Length - 1
            Dim substring As String = substrings(i)

            ' Measure the width of the substring
            Dim substringWidth As Double = New FormattedText(
                substring,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                New Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch),
                tb.FontSize,
                Brushes.Black,
                New NumberSubstitution(),
                1.0
            ).Width

            ' Add the substring as a tspan
            Dim tspan As New Svg.SvgTextSpan With {
                .Text = substring,
                .X = New SvgUnitCollection From {CSng(currentX)},
                .Y = New SvgUnitCollection From {CSng(baselineOffset)}
            }
            svgText.Children.Add(tspan)

            ' Update the current X position
            currentX += substringWidth

            ' If this is not the last substring, move to the next tab stop
            If i < substrings.Length - 1 Then
                currentX = Math.Ceiling(currentX / tabWidth) * tabWidth
            End If
        Next


        svgText.CustomAttributes("dominant-baseline") = "alphabetic"
        svgText.CustomAttributes("xml:space") = "preserve"

        Return svgText
    End Function


    Private Function ConvertPathGeometryToSvgPathSegmentList(geometry As Geometry) As SvgPathSegmentList
        Dim segmentList As New SvgPathSegmentList()

        If TypeOf geometry Is PathGeometry Then
            Dim pathGeometry As PathGeometry = CType(geometry, PathGeometry)
            For Each figure As PathFigure In pathGeometry.Figures
                ' Move to the start point of the figure
                segmentList.Add(New SvgMoveToSegment(New System.Drawing.PointF(CSng(figure.StartPoint.X), CSng(figure.StartPoint.Y))))
                For Each segment As PathSegment In figure.Segments
                    If TypeOf segment Is LineSegment Then
                        Dim lineSegment As LineSegment = CType(segment, LineSegment)
                        segmentList.Add(New SvgLineSegment(New System.Drawing.PointF(CSng(figure.StartPoint.X), CSng(figure.StartPoint.Y)), New System.Drawing.PointF(CSng(lineSegment.Point.X), CSng(lineSegment.Point.Y))))
                    ElseIf TypeOf segment Is BezierSegment Then
                        Dim bezierSegment As BezierSegment = CType(segment, BezierSegment)
                        segmentList.Add(New SvgCubicCurveSegment(New System.Drawing.PointF(CSng(figure.StartPoint.X), CSng(figure.StartPoint.Y)), New System.Drawing.PointF(CSng(bezierSegment.Point1.X), CSng(bezierSegment.Point1.Y)), New System.Drawing.PointF(CSng(bezierSegment.Point2.X), CSng(bezierSegment.Point2.Y)), New System.Drawing.PointF(CSng(bezierSegment.Point3.X), CSng(bezierSegment.Point3.Y))))
                    ElseIf TypeOf segment Is QuadraticBezierSegment Then
                        Dim quadraticBezierSegment As QuadraticBezierSegment = CType(segment, QuadraticBezierSegment)
                        segmentList.Add(New SvgQuadraticCurveSegment(New System.Drawing.PointF(CSng(figure.StartPoint.X), CSng(figure.StartPoint.Y)), New System.Drawing.PointF(CSng(quadraticBezierSegment.Point1.X), CSng(quadraticBezierSegment.Point1.Y)), New System.Drawing.PointF(CSng(quadraticBezierSegment.Point2.X), CSng(quadraticBezierSegment.Point2.Y))))
                    ElseIf TypeOf segment Is ArcSegment Then
                        Dim arcSegment As ArcSegment = CType(segment, ArcSegment)
                        segmentList.Add(New SvgArcSegment(New System.Drawing.PointF(CSng(figure.StartPoint.X), CSng(figure.StartPoint.Y)), CSng(arcSegment.Size.Width), CSng(arcSegment.Size.Height), CSng(arcSegment.RotationAngle), If(arcSegment.IsLargeArc, SvgArcSize.Large, SvgArcSize.Small), If(arcSegment.SweepDirection = SweepDirection.Clockwise, SvgArcSweep.Positive, SvgArcSweep.Negative), New System.Drawing.PointF(CSng(arcSegment.Point.X), CSng(arcSegment.Point.Y))))
                    ElseIf TypeOf segment Is PolyLineSegment Then
                        Dim polyLineSegment As PolyLineSegment = CType(segment, PolyLineSegment)
                        For Each point As Windows.Point In polyLineSegment.Points
                            segmentList.Add(New SvgLineSegment(New System.Drawing.PointF(CSng(figure.StartPoint.X), CSng(figure.StartPoint.Y)), New System.Drawing.PointF(CSng(point.X), CSng(point.Y))))
                        Next
                    ElseIf TypeOf segment Is PolyBezierSegment Then
                        Dim polyBezierSegment As PolyBezierSegment = CType(segment, PolyBezierSegment)
                        For i As Integer = 0 To polyBezierSegment.Points.Count - 1 Step 3
                            segmentList.Add(New SvgCubicCurveSegment(New System.Drawing.PointF(CSng(figure.StartPoint.X), CSng(figure.StartPoint.Y)), New System.Drawing.PointF(CSng(polyBezierSegment.Points(i).X), CSng(polyBezierSegment.Points(i).Y)), New System.Drawing.PointF(CSng(polyBezierSegment.Points(i + 1).X), CSng(polyBezierSegment.Points(i + 1).Y)), New System.Drawing.PointF(CSng(polyBezierSegment.Points(i + 2).X), CSng(polyBezierSegment.Points(i + 2).Y))))
                        Next
                    ElseIf TypeOf segment Is PolyQuadraticBezierSegment Then
                        Dim polyQuadraticBezierSegment As PolyQuadraticBezierSegment = CType(segment, PolyQuadraticBezierSegment)
                        For i As Integer = 0 To polyQuadraticBezierSegment.Points.Count - 1 Step 2
                            segmentList.Add(New SvgQuadraticCurveSegment(New System.Drawing.PointF(CSng(figure.StartPoint.X), CSng(figure.StartPoint.Y)), New System.Drawing.PointF(CSng(polyQuadraticBezierSegment.Points(i).X), CSng(polyQuadraticBezierSegment.Points(i).Y)), New System.Drawing.PointF(CSng(polyQuadraticBezierSegment.Points(i + 1).X), CSng(polyQuadraticBezierSegment.Points(i + 1).Y))))
                        Next
                    End If
                Next

                ' Close the figure if it is closed
                If figure.IsClosed Then
                    segmentList.Add(New SvgClosePathSegment())
                End If
            Next
        ElseIf TypeOf geometry Is GeometryGroup Then
            Dim geometryGroup As GeometryGroup = CType(geometry, GeometryGroup)
            For Each childGeometry As Geometry In geometryGroup.Children
                segmentList.AddRange(ConvertPathGeometryToSvgPathSegmentList(childGeometry))
            Next
        End If

        Return segmentList
    End Function

    Public Overloads Function GetTransformedSVGElement() As SvgVisualElement Implements IDrawable.GetTransformedSVGElement

        Dim component As SvgVisualElement = DrawingToSVG().DeepCopy

        Return BakeTransforms(component, DrawableElement, -3, -1, True)

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
        If Not IgnoreDrawableScale Then
            Dim scaleX As Double = width / drawableElement.ActualWidth
            Dim scaleY As Double = height / drawableElement.ActualHeight
            matrix.Scale(scaleX, scaleY)
        End If


        ' Translate
        matrix.Translate(originX - LCorrection, originY - TCorrection)
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
        Return component
    End Function


End Class

