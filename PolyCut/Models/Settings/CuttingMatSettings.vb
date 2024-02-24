Imports System.IO
Imports System.Reflection
Imports System.Text.Json

Imports PolyCut.Core

Public Class CuttingMatSettings : Inherits SettingsBase : Implements ISettingsService

    Public Overloads Async Function InitialiseSettings(Of Configuration As {ISaveable, New})(appName As String, Subfolder As String) As Task Implements ISettingsService.InitialiseSettings

        Await MyBase.InitialiseSettings(Of Configuration)(appName, Subfolder)

        Dim cuttingmatSVGs = SettingsFolder.GetFiles("*.svg")

        If cuttingmatSVGs.Count = 0 Then
            WriteDefaultCuttingMat()
            WriteDarkDefaultCuttingMat()
            Return
        End If

        If Not cuttingmatSVGs.Contains(New FileInfo(IO.Path.Combine(SettingsFolder.FullName, "CuttingMat.svg"))) Then
            WriteDefaultCuttingMat()
        ElseIf Not cuttingmatSVGs.Contains(New FileInfo(IO.Path.Combine(SettingsFolder.FullName, "CuttingMat.Dark.svg"))) Then
            WriteDarkDefaultCuttingMat()
        End If

    End Function



    Private Sub WriteDefaultCuttingMat()

        Dim outputPath As String = IO.Path.Combine(SettingsFolder.FullName, "CuttingMat.svg")
        Dim asx = Assembly.GetExecutingAssembly()
        Using stream As Stream = asx.GetManifestResourceStream("PolyCut.CuttingMat.svg")
            If stream IsNot Nothing Then
                ' Read the content of the embedded resource
                Using reader As New StreamReader(stream)
                    Dim content As String = reader.ReadToEnd()

                    ' Write the content to the specified file on disk
                    File.WriteAllText(outputPath, content)
                End Using
            Else
                Console.WriteLine("Embedded resource not found.")
            End If
        End Using

    End Sub

    Private Sub WriteDarkDefaultCuttingMat()

        Dim outputPath As String = IO.Path.Combine(SettingsFolder.FullName, "CuttingMat.Dark.svg")
        Dim asx = Assembly.GetExecutingAssembly()
        Using stream As Stream = asx.GetManifestResourceStream("PolyCut.CuttingMat.Dark.svg")
            If stream IsNot Nothing Then
                ' Read the content of the embedded resource
                Using reader As New StreamReader(stream)
                    Dim content As String = reader.ReadToEnd()

                    ' Write the content to the specified file on disk
                    File.WriteAllText(outputPath, content)
                End Using
            Else
                Console.WriteLine("Embedded resource not found.")
            End If
        End Using
    End Sub

End Class
