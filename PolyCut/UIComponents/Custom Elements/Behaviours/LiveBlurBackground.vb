Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media
Imports System.Windows.Media.Effects
Imports System.Windows.Shapes


Public NotInheritable Class LiveBlurBackground

    Public Shared ReadOnly EnableBlurProperty As DependencyProperty = DependencyProperty.RegisterAttached("EnableBlur", GetType(Boolean), GetType(LiveBlurBackground), New PropertyMetadata(False, AddressOf OnEnableBlurChanged))

    Public Shared ReadOnly SourceProperty As DependencyProperty = DependencyProperty.RegisterAttached("Source", GetType(Visual), GetType(LiveBlurBackground), New PropertyMetadata(Nothing, AddressOf OnAppearanceChanged))

    Private Const BlurRadiusDefault As Double = 40.0
    Private Const BlurOpacityDefault As Double = 0.1
    Private Shared ReadOnly ShadeColorDefault As Color = Color.FromArgb(&H5, 255, 255, 255)
    Private Shared ReadOnly MicaTintDefault As Color = Color.FromArgb(&H23, 255, 255, 255)








    Private Shared ReadOnly StateProperty As DependencyProperty = DependencyProperty.RegisterAttached("State", GetType(BlurState), GetType(LiveBlurBackground), New PropertyMetadata(Nothing))





    Public Shared Function GetEnableBlur(element As DependencyObject) As Boolean
        Return CBool(element.GetValue(EnableBlurProperty))
    End Function

    Public Shared Sub SetEnableBlur(element As DependencyObject, value As Boolean)
        element.SetValue(EnableBlurProperty, value)
    End Sub

    Public Shared Function GetSource(element As DependencyObject) As Visual
        Return TryCast(element.GetValue(SourceProperty), Visual)
    End Function

    Public Shared Sub SetSource(element As DependencyObject, value As Visual)
        element.SetValue(SourceProperty, value)
    End Sub











    Private Shared Sub OnEnableBlurChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim element = TryCast(d, FrameworkElement)
        If element Is Nothing Then Return
        If Not (TypeOf element Is Border OrElse TypeOf element Is ContentControl) Then Return

        Dim state = TryCast(element.GetValue(StateProperty), BlurState)

        If CBool(e.NewValue) Then
            If state Is Nothing Then
                state = New BlurState(element)
                element.SetValue(StateProperty, state)
            End If

            'The surface owns its own Mica sampling, so no element needs to declare MicaBackdrop itself.
            MicaBackdrop.SetIsEnabled(element, True)
            If element.ReadLocalValue(MicaBackdrop.TintColorProperty) Is DependencyProperty.UnsetValue Then
                MicaBackdrop.SetTintColor(element, MicaTintDefault)
            End If

            state.Attach()
        ElseIf state IsNot Nothing Then
            state.Detach()
            element.ClearValue(StateProperty)
            MicaBackdrop.SetIsEnabled(element, False)
        End If
    End Sub


    Private Shared Sub OnAppearanceChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim element = TryCast(d, FrameworkElement)
        If element Is Nothing Then Return

        Dim state = TryCast(element.GetValue(StateProperty), BlurState)
        state?.Update()
    End Sub

    Private NotInheritable Class BlurState

        Private ReadOnly _element As FrameworkElement
        Private ReadOnly _border As Border
        Private ReadOnly _content As ContentControl

        Private _host As Grid
        Private _contentHost As Grid
        Private _blurRectangle As Rectangle
        Private _baseRectangle As Rectangle
        Private _shadeRectangle As Rectangle
        Private _shadeBrush As SolidColorBrush
        Private _brush As VisualBrush
        Private _imageBrush As ImageBrush
        Private _effect As BlurEffect
        Private _originalChild As UIElement

        Private _attached As Boolean
        Private _installed As Boolean
        Private _trackingLayout As Boolean

        Public Sub New(element As FrameworkElement)
            _element = element
            _border = TryCast(element, Border)
            _content = If(_border Is Nothing, TryCast(element, ContentControl), Nothing)
        End Sub

        ''' <summary>A Border owns Child; a ContentControl owns Content.</summary>
        Private Function GetChild() As UIElement
            If _border IsNot Nothing Then Return _border.Child
            If _content IsNot Nothing Then Return TryCast(_content.Content, UIElement)
            Return Nothing
        End Function

        Private Sub SetChild(value As UIElement)
            If _border IsNot Nothing Then
                _border.Child = value
            ElseIf _content IsNot Nothing Then
                _content.Content = value
            End If
        End Sub

        Private ReadOnly Property InnerPadding As Thickness
            Get
                If _border IsNot Nothing Then Return _border.Padding
                If _content IsNot Nothing Then Return _content.Padding
                Return New Thickness(0)
            End Get
        End Property

        Private _cornerRadius As Double = -1

        Private ReadOnly Property InnerCornerRadius As Double
            Get
                If _border IsNot Nothing Then Return _border.CornerRadius.TopLeft

                If _cornerRadius < 0 Then
                    Dim template = FindRoundedBorder(_element)
                    _cornerRadius = If(template Is Nothing, 0, template.CornerRadius.TopLeft)
                End If

                Return _cornerRadius
            End Get
        End Property

        Private Shared Function FindRoundedBorder(root As DependencyObject) As Border
            Dim count = VisualTreeHelper.GetChildrenCount(root)
            For i = 0 To count - 1
                Dim child = VisualTreeHelper.GetChild(root, i)

                Dim border = TryCast(child, Border)
                If border IsNot Nothing AndAlso border.CornerRadius.TopLeft > 0 Then Return border

                Dim nested = FindRoundedBorder(child)
                If nested IsNot Nothing Then Return nested
            Next
            Return Nothing
        End Function

        Public Sub Attach()
            If _attached Then
                Update()
                Return
            End If

            _attached = True

            AddHandler _element.Loaded, AddressOf OnLoaded
            AddHandler _element.Unloaded, AddressOf OnUnloaded

            If _element.IsLoaded Then Install()
        End Sub

        Public Sub Detach()
            If Not _attached Then Return

            _attached = False

            RemoveHandler _element.Loaded, AddressOf OnLoaded
            RemoveHandler _element.Unloaded, AddressOf OnUnloaded

            StopTrackingLayout()

            If Not _installed Then Return

            _brush.Visual = Nothing
            _blurRectangle.Fill = Nothing
            _shadeRectangle.Fill = Nothing
            _imageBrush.ImageSource = Nothing
            _blurRectangle.Effect = Nothing

            If ReferenceEquals(GetChild(), _host) Then
                SetChild(Nothing)

                If _originalChild IsNot Nothing Then
                    _contentHost.Children.Remove(_originalChild)
                    SetChild(_originalChild)
                End If
            End If

            _host.Children.Clear()

            _host = Nothing
            _contentHost = Nothing
            _blurRectangle = Nothing
            _baseRectangle = Nothing
            _shadeRectangle = Nothing
            _shadeBrush = Nothing
            _brush = Nothing
            _imageBrush = Nothing
            _effect = Nothing
            _originalChild = Nothing

            _installed = False
        End Sub

        Public Sub Update()
            If Not _installed Then Return

            UpdatePaddingLayout()
            UpdateAppearance()
            UpdateClip()
            UpdateMapping()
        End Sub

        Private Sub OnLoaded(sender As Object, e As RoutedEventArgs)
            Install()

            Dim hostWindow = Window.GetWindow(_element)
            If hostWindow IsNot Nothing Then
                AddHandler hostWindow.Activated, AddressOf OnWindowFocusChanged
                AddHandler hostWindow.Deactivated, AddressOf OnWindowFocusChanged
            End If
        End Sub

        Private Sub OnWindowFocusChanged(sender As Object, e As EventArgs)
            Update()
        End Sub

        Private Sub OnUnloaded(sender As Object, e As RoutedEventArgs)
            StopTrackingLayout()
        End Sub

        Private Sub Install()
            If _installed Then
                StartTrackingLayout()
                Update()
                Return
            End If

            _originalChild = GetChild()
            SetChild(Nothing)

            _brush = New VisualBrush With {
                .Stretch = Stretch.Fill,
                .AlignmentX = AlignmentX.Left,
                .AlignmentY = AlignmentY.Top,
                .ViewboxUnits = BrushMappingMode.Absolute,
                .ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                .Viewport = New Rect(0, 0, 1, 1),
                .TileMode = TileMode.None
            }

            _imageBrush = New ImageBrush With {
                .Stretch = Stretch.Fill,
                .AlignmentX = AlignmentX.Left,
                .AlignmentY = AlignmentY.Top,
                .ViewboxUnits = BrushMappingMode.Absolute,
                .ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                .Viewport = New Rect(0, 0, 1, 1),
                .TileMode = TileMode.None
            }

            _effect = New BlurEffect With {
                .KernelType = KernelType.Gaussian,
                .RenderingBias = RenderingBias.Performance
            }

            _blurRectangle = New Rectangle With {
                .Fill = _brush,
                .Effect = _effect,
                .IsHitTestVisible = False
            }

            _baseRectangle = New Rectangle With {
                .Fill = _imageBrush,
                .IsHitTestVisible = False
            }

            _shadeBrush = New SolidColorBrush(ShadeColorDefault)
            _shadeRectangle = New Rectangle With {
                .Fill = _shadeBrush,
                .IsHitTestVisible = False
            }

            _host = New Grid()
            _contentHost = New Grid()

            _host.Children.Add(_baseRectangle)
            _host.Children.Add(_blurRectangle)
            _host.Children.Add(_shadeRectangle)
            _host.Children.Add(_contentHost)

            If _originalChild IsNot Nothing Then
                _contentHost.Children.Add(_originalChild)
            End If

            SetChild(_host)
            _installed = True

            Update()
            StartTrackingLayout()
        End Sub

        Private Sub StartTrackingLayout()
            If _trackingLayout Then Return

            AddHandler _element.LayoutUpdated, AddressOf OnLayoutUpdated
            _trackingLayout = True
        End Sub

        Private Sub StopTrackingLayout()
            If Not _trackingLayout Then Return

            RemoveHandler _element.LayoutUpdated, AddressOf OnLayoutUpdated
            _trackingLayout = False
        End Sub

        Private Sub OnLayoutUpdated(sender As Object, e As EventArgs)
            If Not _installed Then Return

            UpdatePaddingLayout()
            UpdateClip()
            UpdateMapping()
        End Sub

        Private Sub UpdatePaddingLayout()
            If _host Is Nothing OrElse _contentHost Is Nothing Then Return

            Dim padding = InnerPadding
            Dim negativePadding = New Thickness(-padding.Left, -padding.Top, -padding.Right, -padding.Bottom)

            If _host.Margin <> negativePadding Then _host.Margin = negativePadding
            If _contentHost.Margin <> padding Then _contentHost.Margin = padding
        End Sub

        Private Sub UpdateAppearance()
            If _effect Is Nothing OrElse _brush Is Nothing Then Return

            Dim radius = Math.Max(0, BlurRadiusDefault)
            Dim blurOpacity = Math.Clamp(BlurOpacityDefault, 0, 1)

            If Not AreClose(_effect.Radius, radius) Then _effect.Radius = radius

            If Not AreClose(_brush.Opacity, blurOpacity) Then _brush.Opacity = blurOpacity

            'Provide buffer around the visible region so the blur does not produce transparent/ darkened edges.
            Dim overscan = Math.Ceiling(radius * 1.5)
            Dim margin = New Thickness(-overscan)

            If _blurRectangle.Margin <> margin Then _blurRectangle.Margin = margin
            If _baseRectangle IsNot Nothing AndAlso _baseRectangle.Margin <> margin Then _baseRectangle.Margin = margin
            If _shadeBrush IsNot Nothing AndAlso _shadeBrush.Color <> ShadeColorDefault Then _shadeBrush.Color = ShadeColorDefault
            If _shadeRectangle IsNot Nothing AndAlso _shadeRectangle.Margin <> margin Then _shadeRectangle.Margin = margin
        End Sub

        Private Sub UpdateClip()
            If _host Is Nothing OrElse _host.ActualWidth <= 0 OrElse _host.ActualHeight <= 0 Then Return

            Dim radius = Math.Max(0, InnerCornerRadius)
            Dim bounds = New Rect(0, 0, _host.ActualWidth, _host.ActualHeight)
            Dim clip = TryCast(_host.Clip, RectangleGeometry)

            If clip Is Nothing Then
                _host.Clip = New RectangleGeometry(bounds, radius, radius)
            Else
                If Not RectsEqual(clip.Rect, bounds) Then clip.Rect = bounds
                If Not AreClose(clip.RadiusX, radius) Then clip.RadiusX = radius
                If Not AreClose(clip.RadiusY, radius) Then clip.RadiusY = radius
            End If
        End Sub

        Private Shared Function FindBackdropSource(element As FrameworkElement) As Visual
            Dim child As DependencyObject = element

            While child IsNot Nothing
                Dim parent = VisualTreeHelper.GetParent(child)
                If parent Is Nothing Then Return Nothing

                Dim panel = TryCast(parent, Panel)
                If panel IsNot Nothing Then
                    Dim self = TryCast(child, UIElement)
                    Dim selfIndex = If(self Is Nothing, -1, panel.Children.IndexOf(self))
                    Dim selfZ = If(self Is Nothing, 0, Panel.GetZIndex(self))

                    Dim best As Visual = Nothing
                    Dim bestArea As Double = 0

                    For i = 0 To panel.Children.Count - 1
                        If i = selfIndex Then Continue For

                        Dim sibling = panel.Children(i)
                        If sibling Is Nothing OrElse ReferenceEquals(sibling, element) Then Continue For

                        'Behind means lower z-order, or equal z-order and earlier in the panel.
                        Dim z = Panel.GetZIndex(sibling)
                        If z > selfZ OrElse (z = selfZ AndAlso i > selfIndex) Then Continue For

                        Dim fe = TryCast(sibling, FrameworkElement)
                        If fe Is Nothing OrElse fe.ActualWidth <= 0 OrElse fe.ActualHeight <= 0 Then Continue For

                        Dim area = fe.ActualWidth * fe.ActualHeight
                        If area > bestArea Then
                            bestArea = area
                            best = fe
                        End If
                    Next

                    If best IsNot Nothing Then Return best
                End If

                child = parent
            End While

            Return Nothing
        End Function


        Private Sub UpdateMapping()
            If _brush Is Nothing OrElse _imageBrush Is Nothing Then Return
            Dim surfaceBrush = MicaBackdrop.GetSurfaceBrush(_element)

            'An unfocused window shows the plain card background rather than a backdrop, but it must still be solid.
            Dim hostWindow = Window.GetWindow(_element)
            If hostWindow IsNot Nothing AndAlso Not hostWindow.IsActive Then
                _brush.Visual = Nothing
                _blurRectangle.Fill = Nothing
                _shadeRectangle.Fill = Nothing
                If _baseRectangle IsNot Nothing AndAlso surfaceBrush IsNot Nothing Then _baseRectangle.Fill = surfaceBrush
                Return
            End If


            Dim backgroundImage = MicaBackdrop.GetSurfaceImage(_element)
            If backgroundImage IsNot Nothing Then
                If Not ReferenceEquals(_imageBrush.ImageSource, backgroundImage) Then _imageBrush.ImageSource = backgroundImage

                Dim viewbox = Rect.Empty
                If viewbox.IsEmpty Then
                    Dim bitmap = TryCast(backgroundImage, BitmapSource)
                    If bitmap IsNot Nothing Then
                        _imageBrush.ViewboxUnits = BrushMappingMode.Absolute
                        viewbox = New Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight)
                    Else
                        _imageBrush.ViewboxUnits = BrushMappingMode.RelativeToBoundingBox
                        viewbox = New Rect(0, 0, 1, 1)
                    End If
                Else
                    _imageBrush.ViewboxUnits = BrushMappingMode.Absolute
                End If

                If Not RectsEqual(_imageBrush.Viewbox, viewbox) Then _imageBrush.Viewbox = viewbox
                If _baseRectangle IsNot Nothing AndAlso Not ReferenceEquals(_baseRectangle.Fill, _imageBrush) Then _baseRectangle.Fill = _imageBrush
                If _shadeRectangle IsNot Nothing AndAlso Not ReferenceEquals(_shadeRectangle.Fill, _shadeBrush) Then _shadeRectangle.Fill = _shadeBrush
            End If

            'No backdrop image (linux, or Mica unavailable): paint a solid colour so the card is never transparent.
            If backgroundImage Is Nothing AndAlso _baseRectangle IsNot Nothing Then
                If surfaceBrush IsNot Nothing AndAlso Not ReferenceEquals(_baseRectangle.Fill, surfaceBrush) Then _baseRectangle.Fill = surfaceBrush
            End If

            'The live visual is blurred on top of that base.
            If Not ReferenceEquals(_blurRectangle.Fill, _brush) Then _blurRectangle.Fill = _brush

            Dim source = GetSource(_element)
            If source Is Nothing Then source = FindBackdropSource(_element)

            If source Is Nothing OrElse
               _host Is Nothing OrElse
               _host.ActualWidth <= 0 OrElse
               _host.ActualHeight <= 0 Then

                _brush.Visual = Nothing
                Return
            End If

            'The source MUST NOT be an ancestor of the element or we get a circular dependency and fkn overflow
            If source.IsAncestorOf(_element) Then
                _brush.Visual = Nothing
                Return
            End If

            If Not ReferenceEquals(_brush.Visual, source) Then _brush.Visual = source

            Try
                Dim overscan = Math.Ceiling(Math.Max(0, BlurRadiusDefault) * 1.5)

                Dim localCaptureBounds = New Rect(-overscan, -overscan, _host.ActualWidth + overscan * 2, _host.ActualHeight + overscan * 2)

                Dim transform = _host.TransformToVisual(source)
                Dim sourceBounds = transform.TransformBounds(localCaptureBounds)

                If sourceBounds.Width <= 0 OrElse sourceBounds.Height <= 0 Then Return

                If Not RectsEqual(_brush.Viewbox, sourceBounds) Then _brush.Viewbox = sourceBounds

            Catch ex As InvalidOperationException

            End Try
        End Sub

        Private Shared Function AreClose(a As Double, b As Double) As Boolean
            Return Math.Abs(a - b) < 0.01
        End Function

        Private Shared Function RectsEqual(a As Rect, b As Rect) As Boolean
            Return AreClose(a.X, b.X) AndAlso
                   AreClose(a.Y, b.Y) AndAlso
                   AreClose(a.Width, b.Width) AndAlso
                   AreClose(a.Height, b.Height)
        End Function

    End Class

End Class
