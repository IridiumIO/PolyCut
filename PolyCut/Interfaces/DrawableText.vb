

Imports System.ComponentModel
Imports System.Windows.Threading

Imports PolyCut.Shared

Imports Svg

Public Class DrawableText : Inherits BaseDrawable : Implements IDrawable


    Public Overloads ReadOnly Property VisualName As String Implements IDrawable.VisualName
    Public Sub New(element As TextBox)
        DrawableElement = element
        element.Cursor = Cursors.Arrow
        VisualName = "Text"
        Name = VisualName
        InitializeStrokeRendering()
    End Sub

    ' Visual stroke rendering members
    Private _strokeGeometryDrawing As GeometryDrawing
    Private _drawingBrush As DrawingBrush
    Private _strokePen As Pen
    Private _attachedTextBox As TextBox

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
        Dim watchProps() As DependencyProperty = {
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
            End If
        Next

        Dim fontSizeDesc = DependencyPropertyDescriptor.FromProperty(TextBox.FontSizeProperty, GetType(TextBox))
        If fontSizeDesc IsNot Nothing Then
            fontSizeDesc.AddValueChanged(tb, AddressOf OnFontPropertyChanged)
        End If
        Dim fontFamilyDesc = DependencyPropertyDescriptor.FromProperty(TextBox.FontFamilyProperty, GetType(TextBox))
        If fontFamilyDesc IsNot Nothing Then
            fontFamilyDesc.AddValueChanged(tb, AddressOf OnFontPropertyChanged)
        End If

        Dim editingDesc = DependencyPropertyDescriptor.FromProperty(TextEditHelper.IsEditingProperty, GetType(TextBox))
        If editingDesc IsNot Nothing Then
            editingDesc.AddValueChanged(tb, AddressOf OnIsEditingChanged)
        End If

        ' Initial update (defer to allow control to be measured/rendered)
        tb.Dispatcher.BeginInvoke(New Action(Sub() UpdateTextGeometry()), DispatcherPriority.Loaded)
    End Sub

    Private Sub OnIsEditingChanged(sender As Object, e As EventArgs)
        If Not TextEditHelper.GetIsEditing(_attachedTextBox) Then
            UpdateTextGeometry()
        End If
    End Sub

    Private Sub OnTextBoxVisualChanged(sender As Object, e As EventArgs)
        UpdateTextGeometry()
    End Sub

    Private Sub OnFontPropertyChanged(sender As Object, e As EventArgs)
        RefreshVisualBox()
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

        If Not TextEditHelper.GetIsEditing(tb) Then
            tb.Background = _drawingBrush
        End If
    End Sub

    Public Sub RefreshVisualBox()
        Dim tb = _attachedTextBox
        Dim wrapper As ContentControl = CType(tb.Parent, ContentControl)
        tb.UpdateLayout()

        If TextEditHelper.GetIsEditing(tb) Then
            wrapper.Width = Double.NaN
            wrapper.Height = Double.NaN
        Else
            tb.Focus()
            wrapper.Width = tb.ActualWidth
            wrapper.Height = tb.ActualHeight
            wrapper.FocusVisualStyle = Nothing
            wrapper.Focus()
        End If
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
            fillServer = ColorAndBrushHelpers.BrushToSvgColourServer(Me.Fill)
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
                Dim strokeServer = ColorAndBrushHelpers.BrushToSvgColourServer(Me.Stroke)
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

        ' Text is not stretched to the wrapper!!
        Return SvgExportHelper.BakeToRoot(component, DrawableElement, stretchAsWrapper:=False)

    End Function


End Class

