
'Generator that uses a modified version of GCodePlot by @arpruss
Imports System.IO
Imports System.Reflection

Imports PolyCut.Core


Public Class GCodePlotGenerator : Implements IGenerator
    Private Property Configuration As ProcessorConfiguration Implements IGenerator.Configuration
    Private Property Printer As Printer Implements IGenerator.Printer
    Private Property GCodes As New List(Of GCode) Implements IGenerator.GCodes

    Private SVGFile As String

    Public Async Function GenerateGcodeAsync() As Task(Of (StatusCode As Integer, Message As String)) Implements IGenerator.GenerateGcodeAsync


        Dim args = BuildGCPArgs()

        Dim tempFilePath As String = Path.GetTempFileName()
        IO.File.WriteAllText(tempFilePath, SVGFile)

        args = args & " """ & tempFilePath & """"

        Dim ret As (String, String) = Await RunEmbeddedExecutable("gcodeplot.exe", args)
        Dim output = ret.Item1
        Dim eroutput = ret.Item2

        File.Delete(tempFilePath)


        If output?.Length = 0 Then Return (1, eroutput)


        For Each line In output.Split(Environment.NewLine)
            GCodes.Add(GCode.Parse(line))
        Next

        If GCodes.Count = 0 Then Return (1, "No GCodes generated")

        Return (0, Nothing)


    End Function

    Public Function GetGCode() As List(Of GCode) Implements IGenerator.GetGCode
        Return GCodes
    End Function



    Async Function RunEmbeddedExecutable(executableName As String, args As String) As Task(Of (String, String))
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
    {NameOf(Configuration.CuttingConfig.ToolDiameter), "tool-offset"},
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

            args.Add($"--{Mappings(prop.Name)}={val?.ToString.ToLower}")
        Next

        Return args

    End Function


    Public Sub New(cfg As ProcessorConfiguration, printer As Printer, svg As String)
        Configuration = cfg
        Me.Printer = printer
        Me.SVGFile = svg

        'GCodePlot uses a tool diameter, not a tool radius
        Configuration.CuttingConfig.ToolDiameter /= 2

    End Sub

End Class

