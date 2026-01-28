Imports System.Runtime.InteropServices
Imports System.Windows.Threading


Public NotInheritable Class Eyedropper
    Implements IDisposable

    Private ReadOnly _owner As Window
    Private _tcs As TaskCompletionSource(Of Color?)
    Private _isActive As Boolean
    Private _oldCursor As Cursor
    Private _timer As DispatcherTimer

    Private _preview As ColorPreviewWindow

    Public Sub New(Optional owner As Window = Nothing)
        _owner = If(owner, Application.Current?.MainWindow)
    End Sub


    Public Function BeginAsync() As Task(Of Color?)
        If _isActive Then
            Return If(_tcs?.Task, Task.FromResult(Of Color?)(Nothing))
        End If

        _tcs = New TaskCompletionSource(Of Color?)(TaskCreationOptions.RunContinuationsAsynchronously)
        _isActive = True

        _oldCursor = Mouse.OverrideCursor
        Mouse.OverrideCursor = Cursors.Cross

        Hook()
        InstallMouseHook()

        If _preview Is Nothing Then
            _preview = New ColorPreviewWindow()
        End If
        _preview.Show()

        If _timer Is Nothing Then
            _timer = New DispatcherTimer(DispatcherPriority.Input) With {.Interval = TimeSpan.FromMilliseconds(33)}
            AddHandler _timer.Tick, AddressOf Timer_Tick
        End If
        _timer.Start()

        Return _tcs.Task
    End Function

    Private Sub Hook()
        If _owner IsNot Nothing Then
            AddHandler _owner.PreviewKeyDown, AddressOf Owner_PreviewKeyDown
            AddHandler _owner.Deactivated, AddressOf Owner_Deactivated
            AddHandler _owner.Closed, AddressOf Owner_Closed
        Else
            AddHandler InputManager.Current.PreProcessInput, AddressOf InputManager_PreProcessInput
        End If
    End Sub

    Private Sub Unhook()
        If _owner IsNot Nothing Then
            RemoveHandler _owner.PreviewKeyDown, AddressOf Owner_PreviewKeyDown
            RemoveHandler _owner.Deactivated, AddressOf Owner_Deactivated
            RemoveHandler _owner.Closed, AddressOf Owner_Closed
        Else
            RemoveHandler InputManager.Current.PreProcessInput, AddressOf InputManager_PreProcessInput
        End If
    End Sub

    Private Sub Complete(result As Color?)
        If Not _isActive Then Return

        _isActive = False
        Mouse.OverrideCursor = _oldCursor

        If _timer IsNot Nothing Then _timer.Stop()
        Unhook()
        RemoveMouseHook()

        If _preview IsNot Nothing Then
            _preview.Hide()
        End If

        _tcs.TrySetResult(result)
    End Sub

    Private Sub Cancel()
        Complete(Nothing)
    End Sub

    Private Sub Owner_Deactivated(sender As Object, e As EventArgs)
        ' If the app loses focus, cancel (common eyedropper behavior)
        Cancel()
    End Sub

    Private Sub Owner_Closed(sender As Object, e As EventArgs)
        Cancel()
    End Sub

    Private Sub Owner_PreviewKeyDown(sender As Object, e As KeyEventArgs)
        If Not _isActive Then Return
        If e.Key = Key.Escape Then
            e.Handled = True
            Cancel()
        End If
    End Sub


    Private Sub InputManager_PreProcessInput(sender As Object, e As PreProcessInputEventArgs)
        If Not _isActive Then Return
        Dim args = TryCast(e.StagingItem.Input, KeyEventArgs)
        If args IsNot Nothing AndAlso args.Key = Key.Escape Then
            args.Handled = True
            Cancel()
        End If
    End Sub

    Private Sub Timer_Tick(sender As Object, e As EventArgs)
        If Not _isActive Then Return

        Dim p As POINT
        If Not GetCursorPos(p) Then Return

        Dim c As Color
        If TryGetScreenPixelColor(c) Then
            If _preview IsNot Nothing AndAlso _preview.IsVisible Then
                _preview.UpdateAtCursor(p.X, p.Y, c)
            End If
        End If
    End Sub


    Public Sub Dispose() Implements IDisposable.Dispose
        If _isActive Then Cancel()

        If _preview IsNot Nothing Then
            _preview.Close()
            _preview = Nothing
        End If

        If _timer IsNot Nothing Then
            RemoveHandler _timer.Tick, AddressOf Timer_Tick
            _timer.Stop()
            _timer = Nothing
            RemoveMouseHook()
        End If
    End Sub


    ' ===== Win32 pixel sampling =====

    <StructLayout(LayoutKind.Sequential)>
    Private Structure POINT
        Public X As Integer
        Public Y As Integer
    End Structure

    <DllImport("user32.dll")>
    Private Shared Function GetCursorPos(ByRef lpPoint As POINT) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetDC(hWnd As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function ReleaseDC(hWnd As IntPtr, hDC As IntPtr) As Integer
    End Function

    <DllImport("gdi32.dll")>
    Private Shared Function GetPixel(hdc As IntPtr, x As Integer, y As Integer) As UInteger
    End Function

    Private Shared Function TryGetScreenPixelColor(ByRef color As Color) As Boolean
        color = Colors.Transparent

        Dim p As POINT
        If Not GetCursorPos(p) Then Return False

        Dim hdc As IntPtr = GetDC(IntPtr.Zero) ' entire screen
        If hdc = IntPtr.Zero Then Return False

        Try
            Dim pixel As UInteger = GetPixel(hdc, p.X, p.Y)
            ' GetPixel returns 0x00BBGGRR
            Dim r As Byte = CByte(pixel And &HFFUI)
            Dim g As Byte = CByte((pixel >> 8) And &HFFUI)
            Dim b As Byte = CByte((pixel >> 16) And &HFFUI)

            color = Color.FromArgb(&HFF, r, g, b)
            Return True
        Finally
            ReleaseDC(IntPtr.Zero, hdc)
        End Try
    End Function


    ' ---- Global mouse hook (captures clicks outside app) ----
    Private _mouseHook As IntPtr = IntPtr.Zero
    Private _mouseProc As LowLevelMouseProc

    Private Delegate Function LowLevelMouseProc(nCode As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr

    Private Const WH_MOUSE_LL As Integer = 14
    Private Const WM_LBUTTONDOWN As Integer = &H201
    Private Const WM_RBUTTONDOWN As Integer = &H204

    Private Sub InstallMouseHook()
        If _mouseHook <> IntPtr.Zero Then Return

        _mouseProc = AddressOf MouseHookCallback
        Dim hMod As IntPtr = GetModuleHandle(Nothing)
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, hMod, 0UI)
    End Sub

    Private Sub RemoveMouseHook()
        If _mouseHook = IntPtr.Zero Then Return
        UnhookWindowsHookEx(_mouseHook)
        _mouseHook = IntPtr.Zero
        _mouseProc = Nothing
    End Sub

    Private Function MouseHookCallback(nCode As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
        If nCode >= 0 AndAlso _isActive Then
            Dim msg As Integer = wParam.ToInt32()

            If msg = WM_LBUTTONDOWN Then
                Application.Current.Dispatcher.BeginInvoke(
                Sub()
                    If Not _isActive Then Return
                    Dim c As Color
                    If TryGetScreenPixelColor(c) Then
                        Complete(c)
                    Else
                        Cancel()
                    End If
                End Sub,
                DispatcherPriority.Send)

                Return CType(1, IntPtr) ' swallow click 

            ElseIf msg = WM_RBUTTONDOWN Then
                Application.Current.Dispatcher.BeginInvoke(
                Sub() If _isActive Then Cancel(),
                DispatcherPriority.Send)

                Return CType(1, IntPtr) ' swallow right click 
            End If
        End If

        Return CallNextHookEx(_mouseHook, nCode, wParam, lParam)
    End Function



    <StructLayout(LayoutKind.Sequential)>
    Private Structure MSLLHOOKSTRUCT
        Public pt As POINT
        Public mouseData As UInteger
        Public flags As UInteger
        Public time As UInteger
        Public dwExtraInfo As IntPtr
    End Structure

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function SetWindowsHookEx(idHook As Integer, lpfn As LowLevelMouseProc, hMod As IntPtr, dwThreadId As UInteger) As IntPtr
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function UnhookWindowsHookEx(hhk As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function CallNextHookEx(hhk As IntPtr, nCode As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    <DllImport("kernel32.dll", CharSet:=CharSet.Auto, SetLastError:=True)>
    Private Shared Function GetModuleHandle(lpModuleName As String) As IntPtr
    End Function



End Class
