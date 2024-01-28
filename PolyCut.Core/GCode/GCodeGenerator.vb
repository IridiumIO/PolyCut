Imports System.Windows.Shapes

Public Class GCodeData
    Public Property GCodes As New List(Of GCode)
    Public Property EstimatedTime As Double = 0
    Public Property TotalLength As Double = 0
End Class

Public Class GCodeGenerator

    Public Shared Function Generate(lines As List(Of Line), cfg As ProcessorConfiguration) As GCodeData

        'Standard Coordinates set 0,0 to the Top Left. 3D Printers use Bottom Left
        RedefineOrigin(lines, cfg)



        Dim zWork As Double = cfg.WorkZ
        Dim zTravel As Double = cfg.TravelZ
        Dim zSafe As Double = cfg.SafeZ

        Dim workSpeed As Double = cfg.WorkSpeed * 60
        Dim travelSpeed As Double = cfg.TravelSpeed * 60
        Dim zSpeed As Double = cfg.ZSpeed * 60


        Dim GCD As New GCodeData With {
            .TotalLength = lines.Sum(Function(l) l.Length)
        }


        GCD.EstimatedTime += GetTimeForLine(lines(0), travelSpeed)
        GCD.GCodes.Add(GCode.G0(lines(0).StartPoint, zTravel, travelSpeed))

        Dim isNewLine As Boolean = True

        For i As Integer = 0 To lines.Count - 1

            'Pen Down
            If isNewLine Then
                GCD.GCodes.Add(GCode.GZ(zWork))
                GCD.EstimatedTime += GetTimeForZ(zTravel - zWork, zSpeed)
            End If

            'Draw Line
            GCD.GCodes.Add(GCode.G1(lines(i).EndPoint, F:=workSpeed))
            GCD.EstimatedTime += GetTimeForLine(lines(i), workSpeed)

            Dim l2 As Line = lines((i + 1) Mod lines.Count)


            'Continue Drawing if next line is continuous
            If lines(i).IsContinuousWith(l2) Then
                isNewLine = False

            Else
                'Pen Up
                GCD.GCodes.Add(GCode.GZ(zTravel))
                GCD.EstimatedTime += GetTimeForZ(zTravel - zWork, zSpeed)


                'Travel to next line if not at the end
                If i <> lines.Count - 1 Then
                    GCD.GCodes.Add(GCode.G0(lines(i + 1).StartPoint, F:=travelSpeed))
                    GCD.EstimatedTime += GetTimeForLine(lines(i).EndPoint.LineTo(lines(i + 1).StartPoint), travelSpeed)
                    isNewLine = True
                Else
                    'Pen Up
                    GCD.GCodes.Add(GCode.GZ(zSafe, zSpeed))
                    GCD.EstimatedTime += GetTimeForZ(zSafe - zWork, zSpeed)
                End If


            End If

        Next

        GCD.GCodes.Add(GCode.GZ(zSafe, zSpeed))

        Return GCD

    End Function

    Private Shared Sub RedefineOrigin(ByRef lines As List(Of Line), cfg As ProcessorConfiguration)
        For Each line In lines
            line.Y1 = cfg.WorkAreaHeight - line.Y1
            line.Y2 = cfg.WorkAreaHeight - line.Y2
        Next
    End Sub

    Public Shared Function GenerateWithMetadata(lines As List(Of Line), cfg As ProcessorConfiguration) As GCodeData

        Dim GCodeData = Generate(lines, cfg)

        Dim InitialMeta As New List(Of GCode) From {
            GCode.CommentLine($"  Created using PolyCut v {ProcessorConfiguration.Version}"),
            GCode.CommentLine($"  "),
            GCode.CommentLine($"  Estimated Time: {SecondsToReadable(GCodeData.EstimatedTime),20}"),
            GCode.CommentLine($"  Total Length:   {MillimetresToReadable(GCodeData.TotalLength),20}"),
            GCode.CommentLine($"  Generator:              PolyCut.Core"),
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
            GCode.CommentLine($" OrcaSlicer PolyCut {ProcessorConfiguration.Version} on_"),
            GCode.CommentLine($" estimated printing time = {CInt(GCodeData.EstimatedTime)}s"),
            GCode.CommentLine($" filament used [mm] = {GCodeData.TotalLength:F1}"),
            GCode.Blank(),
            GCode.CommentLine($"######################################")
            }

        GCodeData.GCodes.InsertRange(0, InitialMeta)
        GCodeData.GCodes.AddRange(EndMeta)

        Return GCodeData

    End Function

    'TODO: Break these out into global so they can be used in other places
    Public Shared Function SecondsToReadable(seconds As Double) As String
        Dim ts As TimeSpan = TimeSpan.FromSeconds(seconds)
        If ts.TotalMinutes < 1 Then Return $"{ts.Seconds}s"
        If ts.TotalHours < 1 Then Return $"{ts.Minutes}m {ts.Seconds}s"

        Return $"{ts.Hours}h {ts.Minutes}m {ts.Seconds}s"

    End Function

    Public Shared Function MillimetresToReadable(distance As Double) As String

        If distance < 100 Then Return $"{CInt(distance)} mm"
        If distance < 1000 Then Return $"{distance / 10:F2} cm"
        Return $"{distance / 1000:F2} m"

    End Function

    Private Shared Function GetTimeForLine(line As Line, speed As Double) As Double
        Return line.Length / speed * 60
    End Function

    Private Shared Function GetTimeForZ(zDist As Double, speed As Double) As Double
        Return Math.Abs(zDist) / speed * 60
    End Function

End Class

