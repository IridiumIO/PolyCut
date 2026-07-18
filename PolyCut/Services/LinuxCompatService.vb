
Imports System.Runtime.InteropServices

Public Class LinuxCompatService

End Class

Public Module WineDetection

    <DllImport("kernel32.dll", CharSet:=CharSet.Ansi, SetLastError:=True)>
    Private Function GetModuleHandle(lpModuleName As String) As IntPtr
    End Function

    <DllImport("kernel32.dll", CharSet:=CharSet.Ansi, SetLastError:=True)>
    Private Function GetProcAddress(hModule As IntPtr, lpProcName As String) As IntPtr
    End Function

    Public ReadOnly Property IsRunningUnderWine As Boolean
        Get
            Dim ntdll = GetModuleHandle("ntdll.dll")
            If ntdll = IntPtr.Zero Then Return False

            Return GetProcAddress(ntdll, "wine_get_version") <> IntPtr.Zero
        End Get
    End Property


End Module