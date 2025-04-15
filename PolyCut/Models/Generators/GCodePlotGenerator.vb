
'Generator that uses a modified version of GCodePlot by @arpruss
Imports System.IO
Imports System.Reflection

Imports PolyCut.Core


Public Class GCodePlotGenerator : Implements IGenerator
    Private Property Configuration As ProcessorConfiguration Implements IGenerator.Configuration
    Private Property Printer As Printer Implements IGenerator.Printer
    Private Property GCodes As New List(Of GCode) Implements IGenerator.GCodes

    Private ReadOnly SVGFile As String

    Public Async Function GenerateGcodeAsync() As Task(Of (StatusCode As Integer, Message As String)) Implements IGenerator.GenerateGcodeAsync


        Dim args = BuildGCPArgs()

        Dim tempFilePath As String = Path.GetRandomFileName()
        IO.File.WriteAllText(tempFilePath, SVGFile)

        args = args & " """ & tempFilePath & """"

        Dim ret As (String, String) = Await RunEmbeddedExecutable("gcodeplot.exe", args)
        Dim output = ret.Item1
        Dim eroutput = ret.Item2

        File.Delete(tempFilePath)


        If output?.Length = 0 OrElse output Is Nothing Then Return (1, eroutput)


        For Each line In output.Split(Environment.NewLine)
            GCodes.Add(GCode.Parse(line))
        Next

        If GCodes.Count = 0 Then Return (1, "No GCodes generated")
        ProcessGcodes()

        Return (0, Nothing)

    End Function

    Private Sub ProcessGcodes()

        GCodes.ForEach(Sub(gc) gc.Comment = Nothing)

        Dim cumulativeTime As Double = 0

        For i = 1 To GCodes.Count - 1
            cumulativeTime += CalculateDuration(GCodes(i))
        Next




        Dim InitialMeta As New List(Of GCode) From {
            GCode.CommentLine($"  Created using PolyCut v {Configuration.SoftwareVersion}"),
            GCode.CommentLine($"  "),
            GCode.CommentLine($"  Estimated Time: {PolyCut.Core.GCodeGenerator.SecondsToReadable(cumulativeTime),20}"),
            GCode.CommentLine($"  Total Length:   {PolyCut.Core.GCodeGenerator.MillimetresToReadable(totalLineLength),20}"),
            GCode.CommentLine($"  Generator:                 GCodePlot"),
            GCode.Blank(),
            GCode.CommentLine($"######################################"),
            GCode.Blank(),
            GCode.Parse("G0 E0"),
            GCode.Parse("G21"),
            GCode.Parse("G28")}

        Dim EndMeta As New List(Of GCode) From {
            GCode.Blank(),
            GCode.CommentLine($"######################################"),
            GCode.CommentLine($" Klipper MetaData"),
            GCode.Blank(),
            GCode.CommentLine($" OrcaSlicer PolyCut {Configuration.SoftwareVersion} on_"),
            GCode.CommentLine($" estimated printing time = {CInt(cumulativeTime)}s"),
            GCode.CommentLine($" filament used [mm] = {totalLineLength:F1}"),
            GCode.Blank(),
            GCode.CommentLine($"######################################")
            }

        GCodes.InsertRange(0, InitialMeta)
        GCodes.AddRange(EndMeta)

    End Sub

    Dim currentX As Double = 0
    Dim currentY As Double = 0
    Dim currentZ As Double = 0

    Private totalLineLength As Double = 0

    Private Function CalculateDuration(gc As GCode) As Double

        If gc.Mode <> "G" Then Return 0
        If gc.Code <> 0 AndAlso gc.Code <> 1 Then Return 0


        Dim x? As Double = If(gc.X IsNot Nothing, gc.X - currentX, 0)
        Dim y? As Double = If(gc.Y IsNot Nothing, gc.Y - currentY, 0)
        Dim z? As Double = If(gc.Z IsNot Nothing, gc.Z - currentZ, 0)

        currentX = If(gc.X IsNot Nothing, gc.X, currentX)
        currentY = If(gc.Y IsNot Nothing, gc.Y, currentY)
        currentZ = If(gc.Z IsNot Nothing, gc.Z, currentZ)

        If x = 0 AndAlso y = 0 AndAlso z = 0 Then
            Return 0
        End If

        Dim dist = Math.Sqrt(x.Value ^ 2 + y.Value ^ 2 + z.Value ^ 2)
        Dim speed = gc.F.Value / 60

        If gc.Code = 1 Then
            totalLineLength += dist
        End If

        Return dist / speed


    End Function

    Public Function GetGCode() As List(Of GCode) Implements IGenerator.GetGCode
        Return GCodes
    End Function

    Shared Async Function RunEmbeddedExecutable(executableName As String, args As String) As Task(Of (String, String))
        Dim executingAssembly As Assembly = Assembly.GetExecutingAssembly()

        Dim executablePath As String = Path.Combine(SettingsHandler.DataFolder.FullName, executableName)

        If Not File.Exists(executablePath) Then
            Using stream As Stream = executingAssembly.GetManifestResourceStream(executingAssembly.GetName().Name & "." & executableName)
                If stream IsNot Nothing Then
                    Dim exeBytes(CInt(stream.Length) - 1) As Byte
                    stream.Read(exeBytes, 0, exeBytes.Length)

                    Using tempFileStream As FileStream = File.Create(executablePath)
                        tempFileStream.Write(exeBytes, 0, exeBytes.Length)
                    End Using
                End If
            End Using
        End If


        ' Run the extracted executable
        Dim process As New Process()
        process.StartInfo.FileName = executablePath
        process.StartInfo.Arguments = args
        process.StartInfo.RedirectStandardOutput = True
        process.StartInfo.RedirectStandardError = True
        process.StartInfo.UseShellExecute = False
        process.StartInfo.CreateNoWindow = True
        process.Start()
        Dim output As String = process.StandardOutput.ReadToEnd()
        Dim outputER As String = process.StandardError.ReadToEnd()

        ' Optionally, wait for the process to exit
        Await process.WaitForExitAsync()


        Return (output, outputER)


    End Function




    Private ReadOnly Mappings As New Dictionary(Of String, String)() From {
    {NameOf(Configuration.SelectedToolMode), "tool-mode"},
    {NameOf(Configuration.TravelSpeed), "pen-up-speed"},
    {NameOf(Configuration.WorkSpeed), "pen-down-speed"},
    {NameOf(Configuration.ZSpeed), "z-speed"},
    {NameOf(Configuration.WorkZ), "work-z"},
    {NameOf(Configuration.TravelZ), "lift-delta-z"},
    {NameOf(Configuration.SafeZ), "safe-delta-z"},
    {NameOf(Configuration.Tolerance), "tolerance"},
    {NameOf(Configuration.ExtractOneColour), "boolean-extract-color"},
    {NameOf(Configuration.ExtractionColor), "extract-color"},
    {NameOf(Configuration.CuttingConfig.ToolRadius), "tool-offset"},
    {NameOf(Configuration.CuttingConfig.Overcut), "overcut"},
    {NameOf(Configuration.InsideOutCuttingOrder), "boolean-sort"},
    {NameOf(Configuration.DrawingConfig.DrawingDirection), "direction"},
    {NameOf(Configuration.OptimisationTimeout), "optimization-time"},
    {NameOf(Configuration.DrawingConfig.CrossHatch), "boolean-shading-crosshatch"},
    {NameOf(Configuration.DrawingConfig.ShadingThreshold), "shading-threshold"},
    {NameOf(Configuration.DrawingConfig.MaxStrokeWidth), "shading-lightest"},
    {NameOf(Configuration.DrawingConfig.MinStrokeWidth), "shading-darkest"},
    {NameOf(Configuration.DrawingConfig.ShadingAngle), "shading-angle"},
    {NameOf(Configuration.Area), "area"}
    }



    Public Function BuildGCPArgs() As String

        Dim properties = Configuration.GetType().GetProperties(Reflection.BindingFlags.Public Or Reflection.BindingFlags.Instance)
        Dim args = BuildArgsForObject(properties, Configuration)

        properties = Configuration.CuttingConfig.GetType().GetProperties(Reflection.BindingFlags.Public Or Reflection.BindingFlags.Instance)
        args.AddRange(BuildArgsForObject(properties, Configuration.CuttingConfig))

        properties = Configuration.DrawingConfig.GetType().GetProperties(Reflection.BindingFlags.Public Or Reflection.BindingFlags.Instance)
        args.AddRange(BuildArgsForObject(properties, Configuration.DrawingConfig))


        args.Add("--align-x=none --align-y=none --ignore-locked=false --ignore-hidden=false")

        Debug.WriteLine(String.Join(" ", args))

        Return String.Join(" ", args)

    End Function


    Private Function BuildArgsForObject(properties As PropertyInfo(), o As Object) As List(Of String)

        Dim args As New List(Of String)

        For Each prop In properties

            Dim val = prop.GetValue(o)

            If val Is Nothing Then
                Continue For
            End If

            'Force decimal separator to use a point instead of a comma
            If TypeOf val Is Double Then
                val = val.ToString().Replace(",", ".")
            End If

            If prop.Name = NameOf(Configuration.SelectedToolMode) Then
                If val = 0 Then
                    args.Add($"--{Mappings(prop.Name)}=cut")
                Else
                    args.Add($"--{Mappings(prop.Name)}=draw")
                End If
                Continue For

            End If

            If Not Mappings.ContainsKey(prop.Name) Then
                Continue For
            End If

            args.Add($"--{Mappings(prop.Name)}={val.ToString.ToLower}")
        Next

        Return args

    End Function


    Public Sub New(ByRef cfg As ProcessorConfiguration, printer As Printer, svg As String)
        Configuration = cfg
        Me.Printer = printer
        Me.SVGFile = svg

    End Sub

End Class

