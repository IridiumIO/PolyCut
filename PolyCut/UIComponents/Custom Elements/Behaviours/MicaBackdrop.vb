Option Strict On
Option Infer On
Option Explicit On

Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Interop
Imports System.Windows.Media
Imports System.Windows.Media.Imaging
Imports System.Windows.Threading
Imports Microsoft.Win32


Public NotInheritable Class MicaBackdrop
    Inherits DependencyObject

    Private Sub New()
    End Sub

    ' ===== Shared wallpaper (decoded once for the whole process) =====


    Private Const DecodeWidth As Integer = 512
    Private Shared ReadOnly _micaSupported As Boolean = OperatingSystem.IsWindows()

    Private Enum WallpaperFit
        Fill = 0
        Fit = 1
        Stretch = 2
        Center = 3
        Tile = 4
        Span = 5
    End Enum

    Private Shared ReadOnly _wallpaperLock As New Object()
    Private Shared _wallpaper As BitmapSource
    Private Shared _wallpaperPath As String
    Private Shared _wallpaperFit As WallpaperFit = WallpaperFit.Fill

    Friend Shared ReadOnly Property Wallpaper As BitmapSource
        Get
            SyncLock _wallpaperLock
                If _wallpaper Is Nothing Then LoadWallpaper()
                Return _wallpaper
            End SyncLock
        End Get
    End Property



    Private Shared Sub LoadWallpaper()
        _wallpaper = Nothing
        _wallpaperPath = ReadWallpaperPath()
        _wallpaperFit = ReadWallpaperFit()

        If String.IsNullOrWhiteSpace(_wallpaperPath) Then Return

        Try
            Dim img As New BitmapImage()
            img.BeginInit()
            img.CacheOption = BitmapCacheOption.OnLoad
            img.CreateOptions = BitmapCreateOptions.IgnoreColorProfile
            img.UriSource = New Uri(_wallpaperPath, UriKind.Absolute)
            img.DecodePixelWidth = DecodeWidth
            img.EndInit()
            img.Freeze()
            _wallpaper = img
        Catch
            _wallpaper = Nothing
        End Try
    End Sub

    Private Shared Function ReadWallpaperPath() As String
        Try
            Dim buffer As New StringBuilder(260)
            If SystemParametersInfo(SPI_GETDESKWALLPAPER, CUInt(buffer.Capacity), buffer, 0UI) AndAlso buffer.Length > 0 Then
                Dim path = buffer.ToString()
                If IO.File.Exists(path) Then Return path
            End If
        Catch
        End Try

        Try
            Dim value = Registry.CurrentUser.OpenSubKey("Control Panel\Colors")?.GetValue("Background")
            Dim parts = If(TryCast(value, String), "").Split(" "c)
            If parts.Length = 3 Then
                Dim r, g, b As Integer
                If Integer.TryParse(parts(0), r) AndAlso Integer.TryParse(parts(1), g) AndAlso Integer.TryParse(parts(2), b) Then
                    Return "solid:" & r & "," & g & "," & b
                End If
            End If
        Catch
        End Try

        Return Nothing
    End Function

    Private Shared Function ReadWallpaperFit() As WallpaperFit
        Try
            Dim desktop = Registry.CurrentUser.OpenSubKey("Control Panel\Desktop")
            If desktop Is Nothing Then Return WallpaperFit.Fill

            Dim style = TryCast(desktop.GetValue("WallpaperStyle"), String)
            Dim tiled = TryCast(desktop.GetValue("TileWallpaper"), String)

            If tiled = "1" Then Return WallpaperFit.Tile

            Select Case style
                Case "10" : Return WallpaperFit.Fill
                Case "6" : Return WallpaperFit.Fit
                Case "2" : Return WallpaperFit.Stretch
                Case "22" : Return WallpaperFit.Span
                Case Else : Return WallpaperFit.Center
            End Select
        Catch
            Return WallpaperFit.Fill
        End Try
    End Function


    Private Const MicaWidth As Integer = 320
    Private Const MicaBlurPasses As Integer = 2
    Private Const SampleIntervalMs As Integer = 400

    Private Shared _mica As BitmapSource
    Private Shared _micaDarken As Double = 0.82
    Private Shared _micaDesaturation As Double = 0.85

    Friend Shared ReadOnly Property MicaImage As BitmapSource
        Get
            SyncLock _wallpaperLock
                If _mica Is Nothing Then BuildMica()
                Return _mica
            End SyncLock
        End Get
    End Property


    Private Shared Sub BuildMica()
        _mica = Nothing

        Dim image = Wallpaper
        If image Is Nothing Then Return

        Try
            Dim scale = Math.Min(1.0, MicaWidth / CDbl(image.PixelWidth))
            Dim reduced As BitmapSource = If(scale < 1.0, New TransformedBitmap(image, New ScaleTransform(scale, scale)), image)

            Dim converted As New FormatConvertedBitmap(reduced, PixelFormats.Bgr32, Nothing, 0)
            Dim width = converted.PixelWidth
            Dim height = converted.PixelHeight
            Dim stride = width * 4
            Dim buffer(stride * height - 1) As Byte
            converted.CopyPixels(buffer, stride, 0)

            buffer = BlurBuffer(buffer, width, height, Math.Max(2, MicaWidth \ 24), MicaBlurPasses)

            For i = 0 To buffer.Length - 4 Step 4
                Dim c = ApplyMicaLook(Color.FromRgb(buffer(i + 2), buffer(i + 1), buffer(i)), _micaDarken, _micaDesaturation)
                buffer(i) = c.B
                buffer(i + 1) = c.G
                buffer(i + 2) = c.R
            Next

            Dim result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, Nothing, buffer, stride)
            result.Freeze()
            _mica = result
        Catch
            _mica = Nothing
        End Try
    End Sub


    Private Shared Function BlurBuffer(source As Byte(), width As Integer, height As Integer, radius As Integer, passes As Integer) As Byte()
        If radius <= 0 Then Return source

        Dim stride = width * 4
        Dim a = CType(source.Clone(), Byte())
        Dim b(a.Length - 1) As Byte

        'stolen from somewhere on stack overflow
        For pass = 1 To passes
            For y = 0 To height - 1
                Dim row = y * stride
                For x = 0 To width - 1
                    Dim r = 0, g = 0, bl = 0, n = 0
                    For k = -radius To radius
                        Dim xx = x + k
                        If xx < 0 Then xx = 0
                        If xx > width - 1 Then xx = width - 1
                        Dim o = row + xx * 4
                        bl += a(o) : g += a(o + 1) : r += a(o + 2)
                        n += 1
                    Next
                    Dim d = row + x * 4
                    b(d) = CByte(bl \ n) : b(d + 1) = CByte(g \ n) : b(d + 2) = CByte(r \ n)
                Next
            Next

            For y = 0 To height - 1
                For x = 0 To width - 1
                    Dim r = 0, g = 0, bl = 0, n = 0
                    For k = -radius To radius
                        Dim yy = y + k
                        If yy < 0 Then yy = 0
                        If yy > height - 1 Then yy = height - 1
                        Dim o = (yy * width + x) * 4
                        bl += b(o) : g += b(o + 1) : r += b(o + 2)
                        n += 1
                    Next
                    Dim d = (y * width + x) * 4
                    a(d) = CByte(bl \ n) : a(d + 1) = CByte(g \ n) : a(d + 2) = CByte(r \ n)
                Next
            Next
        Next

        Return a
    End Function

    Private Shared Function ApplyMicaLook(c As Color, darken As Double, desaturation As Double) As Color
        Dim luminance = 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B
        Dim t = luminance / 255.0

        'Dark hues have little chroma to spare, so they keep most of it; bright hues give more up.
        Dim d = Math.Clamp(desaturation * (0.35 + 0.65 * t), 0.0, 1.0)

        Dim r = c.R + (luminance - c.R) * d
        Dim g = c.G + (luminance - c.G) * d
        Dim b = c.B + (luminance - c.B) * d


        Dim k = Math.Clamp((1.0 - Math.Clamp(darken, 0.0, 1.0)) * (1.6 - 1.2 * t), 0.0, 1.0)

        Return Color.FromRgb(CByte(Math.Clamp(r * k, 0.0, 255.0)),
                             CByte(Math.Clamp(g * k, 0.0, 255.0)),
                             CByte(Math.Clamp(b * k, 0.0, 255.0)))
    End Function


    ' ===== Attached properties =====

    Public Shared ReadOnly IsEnabledProperty As DependencyProperty = DependencyProperty.RegisterAttached("IsEnabled", GetType(Boolean), GetType(MicaBackdrop), New PropertyMetadata(False, AddressOf OnEnabledChanged))

    Public Shared Sub SetIsEnabled(fe As FrameworkElement, value As Boolean)
        fe.SetValue(IsEnabledProperty, value)
    End Sub

    Public Shared Function GetIsEnabled(fe As FrameworkElement) As Boolean
        Return CBool(fe.GetValue(IsEnabledProperty))
    End Function



    Public Shared ReadOnly TintColorProperty As DependencyProperty = DependencyProperty.RegisterAttached("TintColor", GetType(Color), GetType(MicaBackdrop), New PropertyMetadata(Colors.Transparent, AddressOf OnParamsChanged))

    Public Shared Sub SetTintColor(fe As FrameworkElement, value As Color)
        fe.SetValue(TintColorProperty, value)
    End Sub

    Public Shared Function GetTintColor(fe As FrameworkElement) As Color
        Return DirectCast(fe.GetValue(TintColorProperty), Color)
    End Function



    Public Shared ReadOnly SamplePaddingProperty As DependencyProperty = DependencyProperty.RegisterAttached("SamplePadding", GetType(Thickness), GetType(MicaBackdrop), New PropertyMetadata(New Thickness(0), AddressOf OnParamsChanged))

    Public Shared Sub SetSamplePadding(fe As FrameworkElement, value As Thickness)
        fe.SetValue(SamplePaddingProperty, value)
    End Sub

    Public Shared Function GetSamplePadding(fe As FrameworkElement) As Thickness
        Return DirectCast(fe.GetValue(SamplePaddingProperty), Thickness)
    End Function




    ' ===== Published results (read-only) =====



    Private Shared ReadOnly SurfaceBrushProperty As DependencyProperty = DependencyProperty.RegisterAttached("SurfaceBrush", GetType(Brush), GetType(MicaBackdrop), New PropertyMetadata(Nothing))

    Public Shared Function GetSurfaceBrush(fe As FrameworkElement) As Brush
        Return DirectCast(fe.GetValue(SurfaceBrushProperty), Brush)
    End Function


    Private Shared ReadOnly SurfaceImageProperty As DependencyProperty = DependencyProperty.RegisterAttached("SurfaceImage", GetType(ImageSource), GetType(MicaBackdrop), New PropertyMetadata(Nothing))

    Public Shared Function GetSurfaceImage(fe As FrameworkElement) As ImageSource
        Return DirectCast(fe.GetValue(SurfaceImageProperty), ImageSource)
    End Function





    ' ===== Per-element state =====

    Private NotInheritable Class State
        Public ReadOnly Element As FrameworkElement
        Public ReadOnly Timer As DispatcherTimer
        Public Window As Window
        Public LastRect As Int32Rect
        Public LastActive As Boolean

        Public Sub New(element As FrameworkElement)
            Me.Element = element
            Me.Timer = New DispatcherTimer(DispatcherPriority.Background, element.Dispatcher) With {.Interval = TimeSpan.FromMilliseconds(SampleIntervalMs)}
        End Sub
    End Class

    Private Shared ReadOnly _states As New Dictionary(Of FrameworkElement, State)()

    Private Shared Sub OnEnabledChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim fe = TryCast(d, FrameworkElement)
        If fe Is Nothing Then Return

        If CBool(e.NewValue) Then
            Start(fe)
        Else
            [Stop](fe)
        End If
    End Sub

    Private Shared Sub OnParamsChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim fe = TryCast(d, FrameworkElement)
        If fe Is Nothing Then Return

        Dim st As State = Nothing
        If Not _states.TryGetValue(fe, st) Then Return

        Sample(st)
    End Sub

    Private Shared Sub Start(fe As FrameworkElement)
        If _states.ContainsKey(fe) Then Return

        Dim st As New State(fe)
        _states(fe) = st

        AddHandler fe.Loaded, AddressOf OnElementLoaded
        AddHandler fe.Unloaded, AddressOf OnElementUnloaded
        AddHandler st.Timer.Tick, AddressOf OnTick

        If fe.IsLoaded Then Attach(st)
    End Sub

    Private Shared Sub [Stop](fe As FrameworkElement)
        Dim st As State = Nothing
        If Not _states.TryGetValue(fe, st) Then Return
        _states.Remove(fe)

        RemoveHandler fe.Loaded, AddressOf OnElementLoaded
        RemoveHandler fe.Unloaded, AddressOf OnElementUnloaded
        RemoveHandler st.Timer.Tick, AddressOf OnTick
        Detach(st)

        PublishFallback(fe)
    End Sub

    Private Shared Sub SampleSoon(st As State)
        st.Element.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, New Action(Sub() SampleDeferred(st)))
    End Sub

    Private Shared Sub SampleDeferred(st As State)
        If Not st.Element.IsLoaded Then Return
        If st.Window Is Nothing Then Attach(st)
        Sample(st)
    End Sub

    Private Shared Sub OnElementLoaded(sender As Object, e As RoutedEventArgs)
        Dim fe = TryCast(sender, FrameworkElement)
        If fe Is Nothing Then Return

        Dim st As State = Nothing
        If _states.TryGetValue(fe, st) Then Attach(st)
    End Sub

    Private Shared Sub OnElementUnloaded(sender As Object, e As RoutedEventArgs)
        Dim fe = TryCast(sender, FrameworkElement)
        If fe Is Nothing Then Return

        Dim st As State = Nothing
        If _states.TryGetValue(fe, st) Then Detach(st)
    End Sub

    Private Shared Sub Attach(st As State)
        If st.Window IsNot Nothing Then Detach(st)

        st.Window = Window.GetWindow(st.Element)
        If st.Window Is Nothing Then Return

        AddHandler st.Window.LocationChanged, AddressOf OnWindowChanged
        AddHandler st.Window.SizeChanged, AddressOf OnWindowChanged
        AddHandler st.Window.StateChanged, AddressOf OnWindowChanged
        AddHandler st.Window.Activated, AddressOf OnWindowActivationChanged
        AddHandler st.Window.Deactivated, AddressOf OnWindowActivationChanged
        AddHandler st.Window.DpiChanged, AddressOf OnWindowChanged
        AddHandler st.Element.SizeChanged, AddressOf OnWindowChanged

        st.Timer.Start()
        Sample(st)
        SampleSoon(st)
    End Sub

    Private Shared Sub Detach(st As State)
        st.Timer.Stop()

        If st.Window Is Nothing Then Return

        RemoveHandler st.Window.LocationChanged, AddressOf OnWindowChanged
        RemoveHandler st.Window.SizeChanged, AddressOf OnWindowChanged
        RemoveHandler st.Window.StateChanged, AddressOf OnWindowChanged
        RemoveHandler st.Window.Activated, AddressOf OnWindowActivationChanged
        RemoveHandler st.Window.Deactivated, AddressOf OnWindowActivationChanged
        RemoveHandler st.Window.DpiChanged, AddressOf OnWindowChanged

        st.Window = Nothing
    End Sub

    Private Shared Sub OnWindowChanged(sender As Object, e As EventArgs)
        InvalidateAll()
    End Sub

    Private Shared Sub OnWindowActivationChanged(sender As Object, e As EventArgs)
        InvalidateAll()
    End Sub

    Private Shared Sub OnTick(sender As Object, e As EventArgs)
        For Each st In _states.Values
            If st.Window Is Nothing OrElse Not st.Element.IsLoaded OrElse Not st.Element.IsVisible Then Continue For

            If st.LastActive <> st.Window.IsActive OrElse
               Not RectsMatch(st.LastRect, CurrentElementRect(st.Element)) Then Sample(st)
        Next
    End Sub

    Private Shared Sub InvalidateAll()
        For Each st In _states.Values
            Sample(st)
        Next
    End Sub



    ' ===== Sampling =====

    Private Shared Sub Sample(st As State)
        Dim fe = st.Element
        Dim w = st.Window
        If w Is Nothing Then Return

        Dim rect = CurrentElementRect(fe)
        st.LastActive = w.IsActive

        If rect.Width <= 0 OrElse rect.Height <= 0 Then
            st.LastRect = rect
            Return
        End If

        rect = ExpandRect(rect, GetSamplePadding(fe), VisualTreeHelper.GetDpi(fe))
        st.LastRect = rect

        'Linux(not tested yet) or unfocused window: publish the plain app background instead of a region.
        If Not _micaSupported OrElse Not w.IsActive Then
            PublishFallback(fe)
            Return
        End If

        Dim region = GetWallpaperRegion(rect)
        If region Is Nothing Then Return

        Dim average As Color
        Dim tinted = BuildRegion(region, GetTintColor(fe), average)
        If tinted Is Nothing Then Return

        Dim brush As New SolidColorBrush(average)
        brush.Freeze()

        PublishSurface(fe, tinted, average)
    End Sub


    Private Shared Sub PublishSurface(fe As FrameworkElement, region As ImageSource, average As Color)
        Dim tinted As New SolidColorBrush(average)
        tinted.Freeze()

        Dim surface As New ImageBrush(region) With {
            .Stretch = Stretch.Fill,
            .AlignmentX = AlignmentX.Left,
            .AlignmentY = AlignmentY.Top,
            .ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
            .Viewport = New Rect(0, 0, 1, 1),
            .TileMode = TileMode.None
        }
        surface.Freeze()

        fe.SetValue(SurfaceImageProperty, region)
        fe.SetValue(SurfaceBrushProperty, surface)
    End Sub


    Private Shared Sub PublishFallback(fe As FrameworkElement)
        Dim colour = ResolveFallbackColor(fe)

        Dim flat As New SolidColorBrush(colour)
        flat.Freeze()

        Dim swatch = SwatchImage(colour)
        Dim surface As New ImageBrush(swatch) With {
            .Stretch = Stretch.Fill,
            .ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
            .Viewport = New Rect(0, 0, 1, 1),
            .TileMode = TileMode.None
        }
        surface.Freeze()

        fe.SetValue(SurfaceImageProperty, swatch)
        fe.SetValue(SurfaceBrushProperty, surface)
    End Sub


    Private Shared Function ResolveFallbackColor(fe As FrameworkElement) As Color

        'Dim themed = TryCast(fe.TryFindResource("ApplicationBackgroundBrush"), SolidColorBrush)
        'If themed IsNot Nothing Then Return themed.Color

        Return Color.FromRgb(&H32, &H32, &H32)
    End Function

    Private Shared ReadOnly _swatches As New Dictionary(Of Color, BitmapSource)

    Private Shared Function SwatchImage(colour As Color) As BitmapSource
        SyncLock _swatches
            Dim cached As BitmapSource = Nothing
            If _swatches.TryGetValue(colour, cached) Then Return cached

            Dim pixels(15) As Byte
            For i = 0 To 3
                pixels(i * 4) = colour.B
                pixels(i * 4 + 1) = colour.G
                pixels(i * 4 + 2) = colour.R
                pixels(i * 4 + 3) = colour.A
            Next

            Dim image = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, Nothing, pixels, 8)
            image.Freeze()
            _swatches(colour) = image
            Return image
        End SyncLock
    End Function


    Private Shared Function RectsMatch(a As Int32Rect, b As Int32Rect) As Boolean
        Return a.X = b.X AndAlso a.Y = b.Y AndAlso a.Width = b.Width AndAlso a.Height = b.Height
    End Function


    Private Shared Function ExpandRect(rect As Int32Rect, padding As Thickness, dpi As DpiScale) As Int32Rect
        Dim left = CInt(Math.Round(padding.Left * dpi.DpiScaleX))
        Dim top = CInt(Math.Round(padding.Top * dpi.DpiScaleY))
        Dim right = CInt(Math.Round(padding.Right * dpi.DpiScaleX))
        Dim bottom = CInt(Math.Round(padding.Bottom * dpi.DpiScaleY))

        Dim x = rect.X - left
        Dim y = rect.Y - top
        Dim w = rect.Width + left + right
        Dim h = rect.Height + top + bottom

        If w <= 0 OrElse h <= 0 Then Return rect
        Return New Int32Rect(x, y, w, h)
    End Function

    Private Shared Function CurrentElementRect(fe As FrameworkElement) As Int32Rect
        If PresentationSource.FromVisual(fe) Is Nothing Then Return New Int32Rect(0, 0, 0, 0)
        Try
            Dim topLeft = fe.PointToScreen(New Point(0, 0))
            Dim dpi = VisualTreeHelper.GetDpi(fe)

            Dim width = Math.Max(1, CInt(Math.Round(fe.ActualWidth * dpi.DpiScaleX)))
            Dim height = Math.Max(1, CInt(Math.Round(fe.ActualHeight * dpi.DpiScaleY)))

            Return New Int32Rect(CInt(Math.Round(topLeft.X)), CInt(Math.Round(topLeft.Y)), width, height)
        Catch
            Return New Int32Rect(0, 0, 0, 0)
        End Try
    End Function


    Private Shared Function GetWallpaperRegion(windowRect As Int32Rect) As ImageSource
        Dim image = MicaImage
        If image Is Nothing OrElse image.PixelWidth <= 0 OrElse image.PixelHeight <= 0 Then Return Nothing

        Dim monitor = MonitorBounds(windowRect)


        Dim layout = _wallpaperFit
        Dim scaleX As Double
        Dim scaleY As Double

        Select Case layout
            Case WallpaperFit.Stretch
                scaleX = monitor.Width / image.PixelWidth
                scaleY = monitor.Height / image.PixelHeight
            Case WallpaperFit.Fit
                Dim s = Math.Min(monitor.Width / image.PixelWidth, monitor.Height / image.PixelHeight)
                scaleX = s : scaleY = s
            Case WallpaperFit.Center
                scaleX = 1.0 : scaleY = 1.0
            Case Else ' Fill, and Tile/Span approximated as Fill
                Dim s = Math.Max(monitor.Width / image.PixelWidth, monitor.Height / image.PixelHeight)
                scaleX = s : scaleY = s
        End Select

        Dim drawnW = image.PixelWidth * scaleX
        Dim drawnH = image.PixelHeight * scaleY
        Dim originX = monitor.X + (monitor.Width - drawnW) / 2.0
        Dim originY = monitor.Y + (monitor.Height - drawnH) / 2.0


        Dim x = (windowRect.X - originX) / scaleX
        Dim y = (windowRect.Y - originY) / scaleY
        Dim wpx = windowRect.Width / scaleX
        Dim hpx = windowRect.Height / scaleY

        If wpx < 1 OrElse hpx < 1 Then Return Nothing

        Return CropMica(image, x, y, wpx, hpx)
    End Function


    Private Shared Function CropMica(image As BitmapSource, x As Double, y As Double, w As Double, h As Double) As ImageSource
        Try
            Dim ix = Math.Max(0, CInt(Math.Floor(x)))
            Dim iy = Math.Max(0, CInt(Math.Floor(y)))
            Dim iw = Math.Min(image.PixelWidth - ix, Math.Max(1, CInt(Math.Ceiling(w))))
            Dim ih = Math.Min(image.PixelHeight - iy, Math.Max(1, CInt(Math.Ceiling(h))))

            If iw <= 0 OrElse ih <= 0 Then Return Nothing

            Dim crop As New CroppedBitmap(image, New Int32Rect(ix, iy, iw, ih))
            crop.Freeze()
            Return crop
        Catch
            Return Nothing
        End Try
    End Function


    Private Shared Function BuildRegion(source As ImageSource, tint As Color, ByRef average As Color) As ImageSource
        Try
            Dim bitmap = TryCast(source, BitmapSource)
            If bitmap Is Nothing Then Return Nothing

            Dim converted As New FormatConvertedBitmap(bitmap, PixelFormats.Bgr32, Nothing, 0)
            Dim width = converted.PixelWidth
            Dim height = converted.PixelHeight
            Dim stride = width * 4
            Dim buffer(stride * height - 1) As Byte
            converted.CopyPixels(buffer, stride, 0)

            Dim a As Integer = CInt(tint.A)

            Dim tr = CInt(tint.R)
            Dim tg = CInt(tint.G)
            Dim tb = CInt(tint.B)

            Dim rSum As Long = 0
            Dim gSum As Long = 0
            Dim bSum As Long = 0
            Dim count As Long = 0

            For i = 0 To buffer.Length - 4 Step 4
                Dim b = CInt(buffer(i))
                Dim g = CInt(buffer(i + 1))
                Dim r = CInt(buffer(i + 2))

                If a > 0 Then
                    r = (r * (255 - a) + tr * a) \ 255
                    g = (g * (255 - a) + tg * a) \ 255
                    b = (b * (255 - a) + tb * a) \ 255
                    buffer(i) = CByte(b)
                    buffer(i + 1) = CByte(g)
                    buffer(i + 2) = CByte(r)
                End If

                rSum += r
                gSum += g
                bSum += b
                count += 1
            Next

            If count = 0 Then Return Nothing
            average = Color.FromRgb(CByte(rSum \ count), CByte(gSum \ count), CByte(bSum \ count))

            If a = 0 Then Return source

            Dim result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, Nothing, buffer, stride)
            result.Freeze()
            Return result
        Catch
            Return Nothing
        End Try
    End Function

    Private Shared Function AverageColor(region As ImageSource) As Color?
        Try
            Dim source = TryCast(region, BitmapSource)
            If source Is Nothing Then Return Nothing

            Dim stride = source.PixelWidth * 4
            Dim buffer(stride * source.PixelHeight - 1) As Byte
            source.CopyPixels(buffer, stride, 0)

            Dim r As Long = 0
            Dim g As Long = 0
            Dim b As Long = 0
            Dim count As Long = 0

            Dim i As Integer = 0
            While i + 3 < buffer.Length
                b += buffer(i)
                g += buffer(i + 1)
                r += buffer(i + 2)
                count += 1
                i += 4
            End While

            If count = 0 Then Return Nothing

            Return Color.FromRgb(CByte(r \ count), CByte(g \ count), CByte(b \ count))
        Catch
            Return Nothing
        End Try
    End Function



    ' ===== Monitor bounds =====

    Private Shared Function MonitorBounds(windowRect As Int32Rect) As Int32Rect
        Dim previous = SetThreadDpiAwarenessContext(New IntPtr(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
        Try
            Dim pt As New NativePoint With {.X = windowRect.X + windowRect.Width \ 2, .Y = windowRect.Y + windowRect.Height \ 2}
            Dim monitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST)
            If monitor <> IntPtr.Zero Then
                Dim info As New MONITORINFO()
                info.cbSize = Marshal.SizeOf(GetType(MONITORINFO))
                If GetMonitorInfo(monitor, info) Then
                    If previous <> IntPtr.Zero Then SetThreadDpiAwarenessContext(previous)
                    Return New Int32Rect(info.rcMonitor.Left,
                                         info.rcMonitor.Top,
                                         info.rcMonitor.Right - info.rcMonitor.Left,
                                         info.rcMonitor.Bottom - info.rcMonitor.Top)
                End If
            End If
        Catch
        End Try

        ' Fall back to the primary monitor's virtual-screen bounds.
        If previous <> IntPtr.Zero Then SetThreadDpiAwarenessContext(previous)
        Return New Int32Rect(0, 0, CInt(SystemParameters.VirtualScreenWidth), CInt(SystemParameters.VirtualScreenHeight))
    End Function

    <StructLayout(LayoutKind.Sequential)>
    Private Structure NativePoint
        Public X As Integer
        Public Y As Integer
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure NativeRect
        Public Left As Integer
        Public Top As Integer
        Public Right As Integer
        Public Bottom As Integer
    End Structure

    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Auto)>
    Private Structure MONITORINFO
        Public cbSize As Integer
        Public rcMonitor As NativeRect
        Public rcWork As NativeRect
        Public dwFlags As Integer
    End Structure

    Private Const SPI_GETDESKWALLPAPER As UInteger = &H73
    Private Const MONITOR_DEFAULTTONEAREST As UInteger = 2
    Private Const DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 As Integer = -4

    <DllImport("user32.dll", CharSet:=CharSet.Unicode, SetLastError:=True)>
    Private Shared Function SystemParametersInfo(uAction As UInteger, uParam As UInteger, lpvParam As StringBuilder, fWinIni As UInteger) As Boolean
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function MonitorFromPoint(pt As NativePoint, dwFlags As UInteger) As IntPtr
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Auto, SetLastError:=True)>
    Private Shared Function GetMonitorInfo(hMonitor As IntPtr, ByRef lpmi As MONITORINFO) As Boolean
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function SetThreadDpiAwarenessContext(context As IntPtr) As IntPtr
    End Function

End Class
