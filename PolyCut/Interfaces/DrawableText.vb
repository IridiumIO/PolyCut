

Imports PolyCut.Shared

Imports Svg
Imports Svg.Pathing
Imports Svg.Transforms

Public Class DrawableText : Inherits BaseDrawable : Implements IDrawable


    Public Overloads ReadOnly Property VisualName As String Implements IDrawable.VisualName
    Public Sub New(element As TextBox)
        DrawableElement = element
        VisualName = "Text"
        Name = VisualName
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
            1.0
        )


        Dim tabWidth As Double = New FormattedText(
            vbTab, ' 4 spaces
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            New Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch),
            tb.FontSize,
            Brushes.Black,
            1.0
        ).Width

        Debug.WriteLine($"Tab width: {tabWidth}")

        Dim baselineOffset = formattedText.Baseline

        ' Convert fill (foreground) color
        Dim fillServer As SvgColourServer = Nothing
        Try
            fillServer = SvgHelpers.BrushToSvgColourServer(Me.Fill)
        Catch
            fillServer = New SvgColourServer(System.Drawing.Color.Black)
        End Try

        Dim svgText As New Svg.SvgText With {
            .X = New SvgUnitCollection From {0},
            .Y = New SvgUnitCollection From {CSng(baselineOffset)},
            .Text = tb.Text,
            .FontFamily = tb.FontFamily.Source,
            .FontSize = tb.FontSize,
            .FontWeight = SvgFontWeight.Normal,
            .Fill = If(fillServer, SvgPaintServer.None),
            .TextAnchor = SvgTextAnchor.Start,
            .FontStyle = SvgFontStyle.Normal,
            .LengthAdjust = SvgTextLengthAdjust.Spacing,
            .Stroke = SvgPaintServer.None
        }

        ' Only set stroke if thickness > 0 and stroke is not Nothing
        If Me.StrokeThickness > 0.001 AndAlso Me.Stroke IsNot Nothing Then
            Try
                Dim strokeServer = SvgHelpers.BrushToSvgColourServer(Me.Stroke)
                If strokeServer IsNot Nothing Then
                    svgText.Stroke = strokeServer
                    svgText.StrokeWidth = CSng(Me.StrokeThickness)
                End If
            Catch
            End Try
        End If

        svgText.Text = Nothing

        Dim substrings As String() = tb.Text.Split(vbTab)
        Dim currentX As Double = 0

        For i As Integer = 0 To substrings.Length - 1
            Dim substring As String = substrings(i)

            Dim substringWidth As Double = tabWidth

            If Not String.IsNullOrEmpty(substring) Then
                substringWidth = New FormattedText(
                    substring,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    New Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch),
                    tb.FontSize,
                    Brushes.Black,
                    1.0
                ).Width
            End If

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


    Public Overloads Function GetTransformedSVGElement() As SvgVisualElement Implements IDrawable.GetTransformedSVGElement

        Dim component As SvgVisualElement = DrawingToSVG().DeepCopy

        Return BakeTransforms(component, DrawableElement, -3, -1, True)

    End Function


    Private Shared Function BakeTransforms(SVGelement As SvgVisualElement, drawableElement As FrameworkElement, Optional LCorrection As Double = 0, Optional TCorrection As Double = 0, Optional IgnoreDrawableScale As Boolean = False) As SvgVisualElement
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

        'Scale
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
        Return component
    End Function


End Class

