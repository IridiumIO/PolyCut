Imports System.Windows

Public Class GCodeLeadInPostProcessor : Implements IGCodePostProcessor


    Public Function Process(gcode As GCodeData, cfg As ProcessorConfiguration) As GCodeData _
    Implements IGCodePostProcessor.Process

        If gcode?.GCodes Is Nothing OrElse gcode.GCodes.Count = 0 Then Return gcode

        Dim result As New List(Of GCode)

        Dim lastTravelZ As Double = cfg.TravelZ
        Dim bladeDown As Boolean = False
        Dim contourStart As Point? = Nothing
        Dim contourPoints As New List(Of Point)
        Dim bladeDownInsertIndex As Integer = -1
        Dim lastXY As Point? = Nothing

        For i = 0 To gcode.GCodes.Count - 1
            Dim g = gcode.GCodes(i)

            ' Track last XY at all times
            If g.X.HasValue AndAlso g.Y.HasValue Then
                lastXY = New Point(g.X.Value, g.Y.Value)
            End If

            ' Handle Z transitions
            If g.Z.HasValue Then
                If Math.Abs(g.Z.Value - cfg.WorkZ) < 0.000001 Then
                    ' Knife down → start contour
                    bladeDown = True
                    contourPoints.Clear()
                    contourStart = lastXY
                    bladeDownInsertIndex = result.Count
                Else
                    ' Knife up → end contour
                    If bladeDown Then
                        InsertLeadInIfClosed(
                    result,
                    bladeDownInsertIndex,
                    contourStart,
                    contourPoints,
                    lastTravelZ,
                    cfg
                )
                    End If

                    bladeDown = False
                    contourStart = Nothing
                    lastTravelZ = g.Z.Value
                End If
            End If

            ' Collect cutting moves
            If bladeDown AndAlso g.Mode = "G" AndAlso g.Code = 1 _
       AndAlso g.X.HasValue AndAlso g.Y.HasValue Then

                contourPoints.Add(New Point(g.X.Value, g.Y.Value))
            End If

            result.Add(CloneGCode(g))
        Next


        gcode.GCodes = result
        Return gcode
    End Function

    Private Sub InsertLeadInIfClosed(
    result As List(Of GCode),
    insertIndex As Integer,
    startPt As Point?,
    contour As List(Of Point),
    travelZ As Double,
    cfg As ProcessorConfiguration)

        If Not startPt.HasValue Then Return
        If contour Is Nothing OrElse contour.Count < 2 Then Return

        Dim p0 As Point = startPt.Value

        ' Find first non-zero cut direction
        Dim dir As Vector = Nothing
        Dim found As Boolean = False

        For Each p In contour
            dir = p - p0
            If dir.Length > 0.000001 Then
                found = True
                Exit For
            End If
        Next

        If Not found Then Return

        ' Normalize direction
        Dim len As Double = dir.Length
        Dim nx As Double = dir.X / len
        Dim ny As Double = dir.Y / len

        ' --- Tool-based lead-in calculation ---
        Dim bladeOffset As Double = cfg.CuttingConfig.ToolRadius
        Const SafetyMarginMm As Double = 0.3

        Dim leadInDistance As Double = bladeOffset + SafetyMarginMm

        If leadInDistance <= 0.0001 Then Return

        ' Compute approach point
        Dim approach As New Point(
        p0.X - nx * leadInDistance,
        p0.Y - ny * leadInDistance
    )

        result.Insert(
        insertIndex,
        Core.GCode.G0(
            approach,
            travelZ,
            F:=cfg.TravelSpeed * 60
        )
    )
    End Sub




    Private Function CloneGCode(src As GCode) As GCode
        If src Is Nothing Then Return Nothing
        Dim clone As New GCode With {
            .Mode = src.Mode,
            .Code = src.Code,
            .X = src.X,
            .Y = src.Y,
            .Z = src.Z,
            .E = src.E,
            .F = src.F,
            .Comment = src.Comment,
            .BlankLine = src.BlankLine
        }
        Return clone
    End Function
End Class