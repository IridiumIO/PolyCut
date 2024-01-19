Imports System.Collections.ObjectModel
Imports System.IO
Imports System.Reflection
Imports System.Text.Json
Imports CommunityToolkit.Mvvm.ComponentModel
Imports SharpVectors.Renderers

Public Class SettingsHandler : Inherits ObservableObject

    Public Shared Property DataFolder As IO.DirectoryInfo = New IO.DirectoryInfo(IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IridiumIO", "PolyCut"))
    Public Shared Property PrintersFolder As IO.DirectoryInfo = New IO.DirectoryInfo(IO.Path.Combine(DataFolder.FullName, "Printers"))
    Public Shared Property CuttingMatsFolder As IO.DirectoryInfo = New IO.DirectoryInfo(IO.Path.Combine(DataFolder.FullName, "Cutting Mats"))

    Public Shared Property SettingsJSONFile As IO.FileInfo = New IO.FileInfo(IO.Path.Combine(DataFolder.FullName, "settings.json"))


    Shared Async Sub InitialiseSettings()

        If Not DataFolder.Exists Then DataFolder.Create()
        If Not PrintersFolder.Exists Then PrintersFolder.Create()
        If Not CuttingMatsFolder.Exists Then CuttingMatsFolder.Create()
        If Not SettingsJSONFile.Exists Then Await SettingsJSONFile.Create().DisposeAsync()


    End Sub

    Shared Async Function GetPrinters() As Task(Of ObservableCollection(Of Printer))

        Dim allPrinters As New ObservableCollection(Of Printer)

        Dim files = PrintersFolder.GetFiles("*.json")

        If files.Length = 0 Then
            Dim p = New Printer
            allPrinters.Add(p)
            WritePrinter(p)
        Else
            For Each file In files
                Dim p = JsonSerializer.Deserialize(Of Printer)(IO.File.ReadAllText(file.FullName), New JsonSerializerOptions With {.IncludeFields = True})
                allPrinters.Add(p)
            Next
        End If

        Return allPrinters

    End Function
    Shared Async Sub WritePrinter(printer As Printer)
        Dim filename = printer.Name & ".json"
        Dim output = JsonSerializer.Serialize(printer, New JsonSerializerOptions With {.IncludeFields = True, .IgnoreReadOnlyProperties = True, .WriteIndented = True})
        Await IO.File.WriteAllTextAsync(IO.Path.Combine(PrintersFolder.FullName, filename), output)

    End Sub

    Shared Async Function GetCuttingMats() As Task(Of ObservableCollection(Of CuttingMat))
        Dim allCuttingMats As New ObservableCollection(Of CuttingMat)
        Dim files = CuttingMatsFolder.GetFiles("*.json")

        If files.Length = 0 Then
            WriteDefaultCuttingMat()
            Dim p = New CuttingMat
            allCuttingMats.Add(p)
            WriteCuttingMat(p)
        Else
            For Each file In files
                Dim p = JsonSerializer.Deserialize(Of CuttingMat)(IO.File.ReadAllText(file.FullName), New JsonSerializerOptions With {.IncludeFields = True})
                allCuttingMats.Add(p)
            Next
        End If

        Return allCuttingMats

    End Function

    Shared Async Sub WriteCuttingMat(cuttingmat As CuttingMat)
        Dim filename = cuttingmat.WinSafeName & ".json"
        Dim output = JsonSerializer.Serialize(cuttingmat, New JsonSerializerOptions With {.IncludeFields = True, .IgnoreReadOnlyProperties = True, .WriteIndented = True})
        Await IO.File.WriteAllTextAsync(IO.Path.Combine(CuttingMatsFolder.FullName, filename), output)
    End Sub

    Private Shared Sub WriteDefaultCuttingMat()

        Dim outputPath As String = IO.Path.Combine(CuttingMatsFolder.FullName, "CuttingMat.svg")
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

End Class
