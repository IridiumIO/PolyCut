

Imports System.ComponentModel
Imports System.Windows.Threading

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
        InitializeStrokeRendering()
    End Sub

    ' Visual stroke rendering members
    Private _strokeGeometryDrawing As GeometryDrawing
    Private _drawingBrush As DrawingBrush
    Private _strokePen As Pen
    Private _attachedTextBox As TextBox

    Private _fontPropertyDescriptors As List(Of DependencyPropertyDescriptor)

    Private Sub InitializeStrokeRendering()
        Dim tb = TryCast(DrawableElement, TextBox)
        If tb Is Nothing Then Return

        _attachedTextBox = tb

        _strokePen = New Pen(If(Me.Stroke, Brushes.Black), Me.StrokeThickness)
        If _strokePen.IsFrozen Then _strokePen = _strokePen.Clone()

        _strokeGeometryDrawing = New GeometryDrawing With {
            .Brush = Nothing,
            .Pen = _strokePen,
            .Geometry = Geometry.Empty
        }

        Dim drawingGroup As New DrawingGroup()
        drawingGroup.Children.Add(_strokeGeometryDrawing)

        _drawingBrush = New DrawingBrush With {
            .Drawing = drawingGroup,
            .Stretch = Stretch.None,
            .AlignmentX = AlignmentX.Left,
            .AlignmentY = AlignmentY.Top,
            .TileMode = TileMode.None,
            .ViewboxUnits = BrushMappingMode.Absolute,
            .ViewportUnits = BrushMappingMode.Absolute
        }

        tb.Background = _drawingBrush

        ' Hook updates
        AddHandler tb.TextChanged, AddressOf OnTextBoxVisualChanged
        AddHandler tb.LayoutUpdated, AddressOf OnTextBoxVisualChanged
        AddHandler tb.SizeChanged, AddressOf OnTextBoxVisualChanged
        AddHandler Me.PropertyChanged, AddressOf OnDrawablePropertyChanged

        ' Watch for font / layout-related property changes so geometry updates automatically
        _fontPropertyDescriptors = New List(Of DependencyPropertyDescriptor)()
        Dim watchProps() As DependencyProperty = {
            TextBox.FontSizeProperty,
            TextBox.FontFamilyProperty,
            TextBox.FontStyleProperty,
            TextBox.FontWeightProperty,
            TextBox.FontStretchProperty,
            TextBox.TextAlignmentProperty,
            TextBox.TextWrappingProperty
        }

        For Each dp In watchProps
            Dim desc = DependencyPropertyDescriptor.FromProperty(dp, GetType(TextBox))
            If desc IsNot Nothing Then
                desc.AddValueChanged(tb, AddressOf OnTextBoxVisualChanged)
                _fontPropertyDescriptors.Add(desc)
            End If
        Next

        ' Initial update (defer to allow control to be measured/rendered)
        tb.Dispatcher.BeginInvoke(New Action(Sub() UpdateTextGeometry()), DispatcherPriority.Loaded)
    End Sub

    Private Sub OnTextBoxVisualChanged(sender As Object, e As EventArgs)
        UpdateTextGeometry()
    End Sub



    Private Sub OnDrawablePropertyChanged(sender As Object, e As System.ComponentModel.PropertyChangedEventArgs)
        If e.PropertyName = NameOf(Stroke) OrElse e.PropertyName = NameOf(StrokeThickness) Then
            ' Update pen brush/thickness on UI thread
            If _attachedTextBox IsNot Nothing Then
                _attachedTextBox.Dispatcher.BeginInvoke(New Action(Sub()
                                                                       _strokePen.Brush = If(Me.Stroke, Brushes.Black)
                                                                       _strokePen.Thickness = Me.StrokeThickness
                                                                       _strokeGeometryDrawing.Pen = _strokePen
                                                                       _drawingBrush.Drawing = _drawingBrush.Drawing ' touch to refresh binding
                                                                   End Sub), DispatcherPriority.Render)
            Else
                _strokePen.Brush = If(Me.Stroke, Brushes.Black)
                _strokePen.Thickness = Me.StrokeThickness
            End If
        End If
    End Sub

    Public Sub UpdateTextGeometry()
        Dim tb = _attachedTextBox

        If tb Is Nothing OrElse tb.ActualWidth <= 0 OrElse tb.ActualHeight <= 0 Then Return

        Dim textToDraw As String = If(String.IsNullOrEmpty(tb.Text), " ", tb.Text)

        Dim r As Rect = tb.GetRectFromCharacterIndex(0, False)
        If r.IsEmpty OrElse Double.IsNaN(r.X) OrElse Double.IsNaN(r.Y) Then Return

        Dim ft As New FormattedText(
        textToDraw,
        Globalization.CultureInfo.CurrentCulture,
        tb.FlowDirection,
        New Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch),
        tb.FontSize,
        Brushes.Black,
        VisualTreeHelper.GetDpi(tb).PixelsPerDip
        ) With {
                .Trimming = TextTrimming.None,
                .TextAlignment = tb.TextAlignment
        }


        Dim origin As New Point(r.X, r.Y)
        Dim geom As Geometry = ft.BuildGeometry(origin)

        _strokeGeometryDrawing.Geometry = geom

        _drawingBrush.Viewbox = New Rect(0, 0, Math.Max(1, tb.ActualWidth), Math.Max(1, tb.ActualHeight))
        _drawingBrush.Viewport = _drawingBrush.Viewbox

        If _drawingBrush.IsFrozen Then _drawingBrush = _drawingBrush.Clone()

        tb.Background = _drawingBrush
    End Sub



    Public Overloads Function DrawingToSVG() As SvgVisualElement Implements IDrawable.DrawingToSVG
        Dim tb As TextBox = CType(DrawableElement, TextBox)

        ' Ensure layout is ready; otherwise rects are often empty/invalid
        If tb.ActualWidth <= 0 OrElse tb.ActualHeight <= 0 Then Return Nothing

        Dim dpi = VisualTreeHelper.GetDpi(tb)
        Dim ppd = dpi.PixelsPerDip

        Dim textValue As String = If(tb.Text, "")
        Dim formattedText As New FormattedText(
        If(textValue.Length = 0, " ", textValue),
        Globalization.CultureInfo.CurrentCulture,
        tb.FlowDirection,
        New Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch),
        tb.FontSize,
        Brushes.Black,
        ppd
    )

        ' Anchor to the real rendered origin (includes padding/border/scroll)
        Dim r As Rect = tb.GetRectFromCharacterIndex(0, False)
        If r.IsEmpty OrElse Double.IsNaN(r.X) OrElse Double.IsNaN(r.Y) Then Return Nothing

        Dim baselineOffset As Double = formattedText.Baseline

        ' SVG text position should be at baseline
        Dim svgStartX As Double = r.X
        Dim svgBaselineY As Double = r.Y + baselineOffset

        ' Convert fill (foreground) color
        Dim fillServer As SvgColourServer = Nothing
        Try
            fillServer = SvgHelpers.BrushToSvgColourServer(Me.Fill)
        Catch
        End Try

        Dim svgText As New Svg.SvgText With {
        .X = New SvgUnitCollection From {CSng(svgStartX)},
        .Y = New SvgUnitCollection From {CSng(svgBaselineY)},
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


        Dim tabWidth As Double = New FormattedText(
            vbTab,
            Globalization.CultureInfo.CurrentCulture,
            tb.FlowDirection,
            New Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch),
            tb.FontSize,
            Brushes.Black,
            ppd
        ).Width

        svgText.Text = Nothing

        Dim substrings As String() = textValue.Split(vbTab)
        Dim currentX As Double = svgStartX

        For i As Integer = 0 To substrings.Length - 1
            Dim substring As String = substrings(i)

            Dim substringWidth As Double = tabWidth
            If substring.Length > 0 Then
                substringWidth = New FormattedText(
                substring,
                Globalization.CultureInfo.CurrentCulture,
                tb.FlowDirection,
                New Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch),
                tb.FontSize,
                Brushes.Black,
                ppd
            ).Width
            End If

            Dim tspan As New Svg.SvgTextSpan With {
            .Text = substring,
            .X = New SvgUnitCollection From {CSng(currentX)},
            .Y = New SvgUnitCollection From {CSng(svgBaselineY)}
        }
            svgText.Children.Add(tspan)

            currentX += substringWidth

            If i < substrings.Length - 1 Then
                currentX = Math.Ceiling((currentX - svgStartX) / tabWidth) * tabWidth + svgStartX
            End If
        Next

        svgText.CustomAttributes("dominant-baseline") = "alphabetic"
        svgText.CustomAttributes("xml:space") = "preserve"

        Return svgText
    End Function



    Public Overloads Function GetTransformedSVGElement() As SvgVisualElement Implements IDrawable.GetTransformedSVGElement

        Dim component As SvgVisualElement = DrawingToSVG().DeepCopy

        Return BakeTransforms(component, DrawableElement, 0, 0, True)

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

