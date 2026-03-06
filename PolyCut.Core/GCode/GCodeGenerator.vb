Imports System.Windows.Shapes

Public Class GCodeData
    Public Property GCodes As New List(Of GCode)
    Public Property EstimatedTime As Double = 0
    Public Property TotalLength As Double = 0
End Class

Public Class GCodeGenerator

    Public Shared Function Generate(lines As List(Of GeoLine), cfg As ProcessorConfiguration) As GCodeData

        'Standard Coordinates set 0,0 to the Top Left. 3D Printers use Bottom Left
        Dim workLines = ApplyOffset(RedefineOrigin(lines, cfg), cfg.ToolOffsetX, cfg.ToolOffsetY)

        Dim zTravel As Double = cfg.TravelZ
        Dim zSafe As Double = cfg.SafeZ

        Dim workSpeed As Double = cfg.WorkSpeed * 60
        Dim travelSpeed As Double = cfg.TravelSpeed * 60
        Dim zSpeed As Double = cfg.ZSpeed * 60

        Dim GCD As New GCodeData With {
            .TotalLength = workLines.Sum(Function(l) l.Length) * Math.Max(1, cfg.Passes)
        }

        If workLines Is Nothing OrElse workLines.Count = 0 Then
            Return GCD
        End If

        GCD.EstimatedTime += GetTimeForLine(workLines(0), travelSpeed)
        GCD.GCodes.Add(GCode.G0(workLines(0).StartPoint.ToPoint, zTravel, travelSpeed))

        For passIndex As Integer = 0 To cfg.Passes - 1

            Dim zWork As Double = cfg.WorkZ + passIndex * cfg.PassHeightDelta
            Dim isNewLine As Boolean = True

            If cfg.Passes > 1 Then
                GCD.GCodes.Add(GCode.CommentLine($"Pass {passIndex + 1} of {cfg.Passes} at height {zWork}"))
            End If

            For i As Integer = 0 To workLines.Count - 1

                'Pen Down
                If isNewLine Then
                    GCD.GCodes.Add(GCode.GZ(zWork))
                    GCD.EstimatedTime += GetTimeForZ(zTravel - zWork, zSpeed)
                End If

                'Draw Line
                GCD.GCodes.Add(GCode.G1(workLines(i).EndPoint.ToPoint, F:=workSpeed))
                GCD.EstimatedTime += GetTimeForLine(workLines(i), workSpeed)

                Dim l2 As GeoLine = workLines((i + 1) Mod workLines.Count)

                'Continue Drawing if next line is continuous
                If workLines(i).IsContinuousWith(l2) Then
                    isNewLine = False
                Else
                    'Pen Up
                    GCD.GCodes.Add(GCode.GZ(zTravel))
                    GCD.EstimatedTime += GetTimeForZ(zTravel - zWork, zSpeed)

                    'Travel to next line if not at the end
                    If i <> workLines.Count - 1 Then
                        GCD.GCodes.Add(GCode.G0(workLines(i + 1).StartPoint.ToPoint, F:=travelSpeed))
                        GCD.EstimatedTime += GetTimeForLine(workLines(i).EndPoint.LineTo(workLines(i + 1).StartPoint), travelSpeed)
                        isNewLine = True
                    Else
                        'Pen Up
                        GCD.GCodes.Add(GCode.GZ(zSafe, zSpeed))
                        GCD.EstimatedTime += GetTimeForZ(zSafe - zWork, zSpeed)
                    End If
                End If

            Next

            If passIndex <> cfg.Passes - 1 Then
                ' Move to travel Z from safe
                GCD.GCodes.Add(GCode.GZ(zTravel))
                GCD.EstimatedTime += GetTimeForZ(zSafe - zTravel, zSpeed)

                ' Travel XY back to the first line start
                Dim lastEnd = workLines(workLines.Count - 1).EndPoint
                GCD.GCodes.Add(GCode.G0(workLines(0).StartPoint.ToPoint, F:=travelSpeed))
                GCD.EstimatedTime += GetTimeForLine(lastEnd.LineTo(workLines(0).StartPoint), travelSpeed)
            End If

        Next

        GCD.GCodes.Add(GCode.GZ(zSafe, zSpeed))

        Return GCD

    End Function

    Private Shared Function RedefineOrigin(lines As List(Of GeoLine), cfg As ProcessorConfiguration) As List(Of GeoLine)
        Dim h As Single = cfg.WorkAreaHeight
        Return lines.Select(Function(ln) New GeoLine(ln.X1, h - ln.Y1, ln.X2, h - ln.Y2)).ToList()
    End Function

    Private Shared Function ApplyOffset(lines As List(Of GeoLine), offsetX As Double, offsetY As Double) As List(Of GeoLine)
        Dim ox As Single = offsetX
        Dim oy As Single = offsetY
        Return lines.Select(Function(ln) New GeoLine(ln.X1 + ox, ln.Y1 + oy, ln.X2 + ox, ln.Y2 + oy)).ToList()
    End Function



    Public Shared Function GenerateWithMetadata(lines As List(Of GeoLine), cfg As ProcessorConfiguration) As GCodeData

        Dim GCodeData = Generate(lines, cfg)

        Dim InitialMeta As New List(Of GCode) From {
            GCode.CommentLine($"  Created using PolyCut v {cfg.SoftwareVersion}"),
            GCode.CommentLine($"  "),
            GCode.CommentLine($"  Estimated Time: {SecondsToReadable(GCodeData.EstimatedTime),20}"),
            GCode.CommentLine($"  Total Length:   {MillimetresToReadable(GCodeData.TotalLength),20}"),
            GCode.CommentLine($"  Generator:              PolyCut.Core"),
            GCode.Blank(),
            GCode.CommentLine($"######################################"),
            GCode.CommentLine($"Custom Start GCode"),
            GCode.Blank(),
            GCode.Blank(),
            GCode.CommentLine($"######################################")}

        Dim EndMeta As New List(Of GCode) From {
            GCode.Blank(),
            GCode.CommentLine($"######################################"),
            GCode.CommentLine($"Custom End GCode"),
            GCode.Blank(),
            GCode.Blank(),
            GCode.CommentLine($"######################################"),
            GCode.CommentLine($" Klipper MetaData"),
            GCode.Blank(),
            GCode.CommentLine($" OrcaSlicer PolyCut {cfg.SoftwareVersion} on_"),
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

    Private Shared Function GetTimeForLine(line As GeoLine, speed As Double) As Double
        Return line.Length / speed * 60
    End Function

    Private Shared Function GetTimeForZ(zDist As Double, speed As Double) As Double
        Return Math.Abs(zDist) / speed * 60
    End Function

End Class

