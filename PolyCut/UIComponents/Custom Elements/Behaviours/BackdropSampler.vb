Option Strict On
Option Infer On
Option Explicit On

Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Windows.Threading

Public NotInheritable Class BackdropSampler
    Inherits DependencyObject

    Private Sub New()
    End Sub

    Public Enum SampleCorner
        TopLeft = 0
        TopRight = 1
        BottomLeft = 2
        BottomRight = 3
    End Enum

    Private NotInheritable Class State
        Public ReadOnly Ctrl As Control
        Public ReadOnly Brush As SolidColorBrush
        Public Original As Brush
        Public Timer As Timer
        Public Busy As Integer

        Public Sub New(ctrl As Control)
            Me.Ctrl = ctrl
            Me.Original = Nothing
            Me.Brush = New SolidColorBrush(Colors.Transparent) ' mutable
            ctrl.Background = Me.Brush
        End Sub
    End Class

    Private Shared ReadOnly _states As New Dictionary(Of Control, State)()

    ' ===== Attached properties =====

    Public Shared ReadOnly IsEnabledProperty As DependencyProperty = DependencyProperty.RegisterAttached("IsEnabled", GetType(Boolean), GetType(BackdropSampler), New PropertyMetadata(False, AddressOf OnEnabledChanged))

    Public Shared Sub SetIsEnabled(fe As FrameworkElement, value As Boolean)
        fe.SetValue(IsEnabledProperty, value)
    End Sub

    Public Shared Function GetIsEnabled(fe As FrameworkElement) As Boolean
        Return CBool(fe.GetValue(IsEnabledProperty))
    End Function

    Public Shared ReadOnly IntervalMsProperty As DependencyProperty = DependencyProperty.RegisterAttached("IntervalMs", GetType(Integer), GetType(BackdropSampler), New PropertyMetadata(60, AddressOf OnParamsChanged))

    Public Shared Sub SetIntervalMs(fe As FrameworkElement, value As Integer)
        fe.SetValue(IntervalMsProperty, value)
    End Sub

    Public Shared Function GetIntervalMs(fe As FrameworkElement) As Integer
        Return Math.Max(50, CInt(fe.GetValue(IntervalMsProperty)))
    End Function

    Public Shared ReadOnly SampleXProperty As DependencyProperty = DependencyProperty.RegisterAttached("SampleX", GetType(Integer), GetType(BackdropSampler), New PropertyMetadata(10, AddressOf OnParamsChanged))

    Public Shared Sub SetSampleX(fe As FrameworkElement, value As Integer)
        fe.SetValue(SampleXProperty, value)
    End Sub

    Public Shared Function GetSampleX(fe As FrameworkElement) As Integer
        Return Math.Max(0, CInt(fe.GetValue(SampleXProperty)))
    End Function

    Public Shared ReadOnly SampleYProperty As DependencyProperty = DependencyProperty.RegisterAttached("SampleY", GetType(Integer), GetType(BackdropSampler), New PropertyMetadata(10, AddressOf OnParamsChanged))

    Public Shared Sub SetSampleY(fe As FrameworkElement, value As Integer)
        fe.SetValue(SampleYProperty, value)
    End Sub

    Public Shared Function GetSampleY(fe As FrameworkElement) As Integer
        Return Math.Max(0, CInt(fe.GetValue(SampleYProperty)))
    End Function

    Public Shared ReadOnly CornerProperty As DependencyProperty = DependencyProperty.RegisterAttached("Corner", GetType(SampleCorner), GetType(BackdropSampler), New PropertyMetadata(SampleCorner.TopLeft, AddressOf OnParamsChanged))

    Public Shared Sub SetCorner(fe As FrameworkElement, value As SampleCorner)
        fe.SetValue(CornerProperty, value)
    End Sub

    Public Shared Function GetCorner(fe As FrameworkElement) As SampleCorner
        Return DirectCast(fe.GetValue(CornerProperty), SampleCorner)
    End Function

    Public Shared ReadOnly TintColorProperty As DependencyProperty = DependencyProperty.RegisterAttached("TintColor", GetType(Color), GetType(BackdropSampler), New PropertyMetadata(Colors.Transparent, AddressOf OnParamsChanged))

    Public Shared Sub SetTintColor(fe As FrameworkElement, value As Color)
        fe.SetValue(TintColorProperty, value)
    End Sub

    Public Shared Function GetTintColor(fe As FrameworkElement) As Color
        Return DirectCast(fe.GetValue(TintColorProperty), Color)
    End Function

    ' ===== Lifecycle =====

    Private Shared Sub OnEnabledChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim ctrl = TryCast(d, Control)
        If ctrl Is Nothing Then Return

        If CBool(e.NewValue) Then
            Start(ctrl)
        Else
            [Stop](ctrl)
        End If
    End Sub

    Private Shared Sub OnParamsChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim ctrl = TryCast(d, Control)
        If ctrl Is Nothing Then Return

        Dim st As State = Nothing
        If Not _states.TryGetValue(ctrl, st) Then Return

        Dim ms As Integer = GetIntervalMs(ctrl)
        st.Timer.Change(0, ms)
        ThreadPool.UnsafeQueueUserWorkItem(AddressOf Tick, st)
    End Sub

    Private Shared Sub Start(ctrl As Control)
        If _states.ContainsKey(ctrl) Then Return

        Dim st As New State(ctrl)
        _states(ctrl) = st

        Dim ms As Integer = GetIntervalMs(ctrl)
        st.Timer = New Timer(AddressOf Tick, st, 0, ms)
    End Sub

    Private Shared Sub [Stop](ctrl As Control)
        Dim st As State = Nothing
        If Not _states.TryGetValue(ctrl, st) Then Return
        _states.Remove(ctrl)

        st.Timer.Dispose()
        ctrl.Background = st.Original
    End Sub

    ' ===== Tick (async capture) =====

    Private Shared Sub Tick(stateObj As Object)
        Dim st = DirectCast(stateObj, State)

        If Interlocked.Exchange(st.Busy, 1) = 1 Then Return

        Try
            Dim ctrl = st.Ctrl
            Dim fe As FrameworkElement = ctrl

            Dim sx As Integer = 0
            Dim sy As Integer = 0
            Dim tint As Color = Colors.Transparent
            Dim doSample As Boolean = False

            ' UI thread: decide + compute coords
            Try
                fe.Dispatcher.Invoke(
                Sub()
                    Dim w = Window.GetWindow(fe)
                    If w Is Nothing Then Return

                    If Not w.IsActive Then
                        If st.Original IsNot Nothing Then
                            ctrl.Background = st.Original
                        End If
                        Return
                    End If

                    ' Lazily capture "original" right before we overwrite it
                    If Not Object.ReferenceEquals(ctrl.Background, st.Brush) Then
                        st.Original = ctrl.Background
                    End If

                    ctrl.Background = st.Brush

                    Dim origin = w.PointToScreen(New Point(0, 0))

                    Dim insetX = GetSampleX(fe)
                    Dim insetY = GetSampleY(fe)
                    Dim corner = GetCorner(fe)
                    tint = GetTintColor(fe)

                    Dim clientW = Math.Max(1, CInt(w.ActualWidth))
                    Dim clientH = Math.Max(1, CInt(w.ActualHeight))

                    Dim ox = CInt(origin.X)
                    Dim oy = CInt(origin.Y)

                    Select Case corner
                        Case SampleCorner.TopLeft
                            sx = ox + insetX : sy = oy + insetY
                        Case SampleCorner.TopRight
                            sx = ox + (clientW - 1 - insetX) : sy = oy + insetY
                        Case SampleCorner.BottomLeft
                            sx = ox + insetX : sy = oy + (clientH - 1 - insetY)
                        Case Else
                            sx = ox + (clientW - 1 - insetX) : sy = oy + (clientH - 1 - insetY)
                    End Select

                    doSample = True
                End Sub,
                DispatcherPriority.Render)

            Catch ex As Exception : Return
            End Try

            If Not doSample Then Return

            ' Background thread: capture
            Dim px = CapturePixelNative(sx, sy)
            If px Is Nothing Then Return

            Dim c = px.Value
            Dim r As Integer = c.R
            Dim g As Integer = c.G
            Dim b As Integer = c.B

            If tint.A <> 0 Then
                Dim a As Integer = tint.A
                r = ((r * (255 - a)) + (tint.R * a)) \ 255
                g = ((g * (255 - a)) + (tint.G * a)) \ 255
                b = ((b * (255 - a)) + (tint.B * a)) \ 255
            End If

            Dim newColor As Color = Color.FromArgb(255, CByte(r), CByte(g), CByte(b))

            ' UI thread: apply
            fe.Dispatcher.BeginInvoke(
                Sub()
                    st.Brush.Color = newColor
                End Sub,
                DispatcherPriority.Render)

        Finally
            st.Busy = 0
        End Try
    End Sub

    ' ===== Native capture =====

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function GetDC(hWnd As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function ReleaseDC(hWnd As IntPtr, hdc As IntPtr) As Integer
    End Function

    <DllImport("gdi32.dll", SetLastError:=True)>
    Private Shared Function GetPixel(hdc As IntPtr, nXPos As Integer, nYPos As Integer) As Integer
    End Function

    Private Shared Function CapturePixelNative(screenX As Integer, screenY As Integer) As (R As Byte, G As Byte, B As Byte)?
        If screenX < 0 OrElse screenY < 0 Then Return Nothing

        Dim hdc As IntPtr = IntPtr.Zero
        Try
            hdc = GetDC(IntPtr.Zero)
            If hdc = IntPtr.Zero Then Return Nothing
            Dim pixel = GetPixel(hdc, screenX, screenY)
            If pixel = &HFFFFFFFF Then
                ' GetPixel failure
                Return Nothing
            End If
            Dim r As Byte = CByte(pixel And &HFF)
            Dim g As Byte = CByte((pixel >> 8) And &HFF)
            Dim b As Byte = CByte((pixel >> 16) And &HFF)
            Return (r, g, b)
        Catch
            Return Nothing
        Finally
            If hdc <> IntPtr.Zero Then
                ReleaseDC(IntPtr.Zero, hdc)
            End If
        End Try
    End Function

End Class
