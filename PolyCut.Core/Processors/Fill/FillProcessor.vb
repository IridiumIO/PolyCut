Imports System.Numerics
Imports System.Windows
Imports System.Windows.Shapes

Imports MeasurePerformance.IL.Weaver

Public Class FillProcessor : Implements IProcessor

    ' -------------------------
    ' Constants / configuration
    ' -------------------------

    Protected Friend Const DefaultScalingFactor As Double = 100_000 ' 1mm -> 100m in scaled units

    ' Intersection tolerances
    Protected Friend Const IntersectionTolerance As Double = 0.000000001
    Protected Friend Const MergeTolerance As Double = 1.0

    ' Optimiser tuning (converts angular difference into a distance-like penalty - units: squared distance. Higher values = try harder to continue directioin)
    Protected Friend Const DirectionPreferenceWeight As Double = 5000.0



    ' -------------------------
    ' Entry point
    ' -------------------------
    Public Function Process(lines As List(Of GeoLine), cfg As ProcessorConfiguration) As List(Of GeoLine) Implements IProcessor.Process

        ' Respect per-element SVG fill presence when deciding to generate fills.
        Dim fillTag As Object = Nothing
        If lines IsNot Nothing AndAlso lines.Count > 0 Then fillTag = lines(0).Tag

        If Not ShouldGenerateFill(fillTag) Then Return lines
        If Not IsShapeClosed(lines) OrElse cfg.DrawingConfig.FillType = FillType.None Then Return lines

        Dim spacingNullable As Double? = ComputeSpacingFromTag(fillTag, cfg)
        If Not spacingNullable.HasValue Then Return lines

        Dim spacing As Double = spacingNullable.Value
        Dim spacingScaled As Double = spacing * DefaultScalingFactor

        Dim outlineGeo As List(Of GeoLine) = ToScaledGeoLines(lines, DefaultScalingFactor)

        Dim processedGeo As List(Of List(Of GeoLine)) = GenerateFill(outlineGeo, spacingScaled, cfg.DrawingConfig.FillType, cfg.DrawingConfig.ShadingAngle, cfg)

        Dim newGeo As List(Of GeoLine) = Nothing



        If cfg.OptimisedToolPath Then
            newGeo = FillOptimiser.Optimise(processedGeo, outlineGeo, cfg.DrawingConfig.AllowDrawingOverOutlines, cfg)
        Else
            newGeo = processedGeo.SelectMany(Function(x) x).ToList()
        End If

        If cfg.DrawingConfig.KeepOutlines Then
            If cfg.DrawingConfig.OutlinesBeforeFill Then
                newGeo.InsertRange(0, outlineGeo)
            Else
                newGeo.AddRange(outlineGeo)
            End If
        End If

        Return ToUnscaledLines(newGeo, DefaultScalingFactor)
    End Function


    <MeasurePerformance>
    Private Shared Function GenerateFill(outline As List(Of GeoLine), spacingScaled As Double, fillType As FillType, angle As Double, cfg As ProcessorConfiguration) As List(Of List(Of GeoLine))
        Dim result As New List(Of List(Of GeoLine))

        Select Case fillType
            Case FillType.Spiral
                result.AddRange(SpiralFillGenerator.Generate(outline, spacingScaled, angle, cfg))
            Case FillType.Radial
                result.AddRange(RadialFillGenerator.Generate(outline, spacingScaled, angle))

            Case FillType.CrossHatch
                result.AddRange(HatchFillGenerator.GenerateHatch(outline, spacingScaled, angle))
                result.AddRange(HatchFillGenerator.GenerateHatch(outline, spacingScaled, angle + 90))

            Case FillType.TriangularHatch
                result.AddRange(HatchFillGenerator.GenerateTriangularHatch(outline, spacingScaled, angle))

            Case FillType.DiamondCrossHatch
                result.AddRange(HatchFillGenerator.GenerateDiamondCrossHatch(outline, spacingScaled, 0))

            Case Else 'regular hatch
                result.AddRange(HatchFillGenerator.GenerateHatch(outline, spacingScaled, angle))
        End Select

        Return result
    End Function


    Public Shared Function IsShapeClosed(lines As List(Of GeoLine)) As Boolean
        For i = 0 To lines.Count - 1
            For j = i To lines.Count - 1
                If i = j Then Continue For
                If lines(i).StartPoint.X = lines(j).EndPoint.X AndAlso lines(i).StartPoint.Y = lines(j).EndPoint.Y Then Return True
            Next
        Next
        Return False
    End Function



    ' -------------------------
    ' Tag policy 
    ' -------------------------
    Private Function ShouldGenerateFill(fillTag As Object) As Boolean
        If fillTag Is Nothing Then Return False
        If TypeOf fillTag Is Boolean Then Return CType(fillTag, Boolean)
        Return True
    End Function

    Private Function ComputeSpacingFromTag(fillTag As Object, cfg As ProcessorConfiguration) As Double?
        ' Map tag (Color #RRGGBB) -> spacing value between MinStrokeWidth and MaxStrokeWidth.
        Dim minW = cfg.DrawingConfig.MinStrokeWidth
        Dim maxW = cfg.DrawingConfig.MaxStrokeWidth
        If maxW < minW Then
            Dim tmp = minW : minW = maxW : maxW = tmp
        End If

        Dim threshold As Double = Math.Clamp(cfg.DrawingConfig.ShadingThreshold, 0, 1)

        ' Defaults
        Dim spacing As Double = minW


        If TypeOf fillTag Is String Then
            Dim s = CType(fillTag, String)
            If s.StartsWith("#"c) AndAlso s.Length = 7 Then
                Try
                    Dim r = Convert.ToInt32(s.Substring(1, 2), 16)
                    Dim g = Convert.ToInt32(s.Substring(3, 2), 16)
                    Dim b = Convert.ToInt32(s.Substring(5, 2), 16)
                    Dim brightness = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0
                    brightness = Math.Round(brightness, 3)
                    If brightness < threshold Then Return Nothing

                    ' Map brightness to spacing: brighter => wider spacing (lighter)
                    spacing = minW + brightness * (maxW - minW)
                    Return spacing
                Catch
                    Return (minW + maxW) / 2
                End Try
            Else
                ' non-hex paint (gradient/pattern) -> fallback to mid
                Dim brightness = 0.5
                If brightness < threshold Then Return Nothing
                Return (minW + maxW) / 2
            End If
        ElseIf TypeOf fillTag Is Boolean AndAlso CType(fillTag, Boolean) = True Then
            ' Unknown colour but filled: use dense (min)
            Return minW
        End If

        Return Nothing
    End Function



    ' -------------------------
    ' Conversions
    ' -------------------------
    Private Shared Function ToScaledGeoLines(lines As List(Of GeoLine), scale As Double) As List(Of GeoLine)
        Dim result As New List(Of GeoLine)(If(lines?.Count, 0))
        If lines Is Nothing Then Return result

        For Each ln In lines
            result.Add(New GeoLine(ln.X1 * scale, ln.Y1 * scale, ln.X2 * scale, ln.Y2 * scale))
        Next

        Return result
    End Function

    Private Shared Function ToUnscaledLines(lines As List(Of GeoLine), scale As Double) As List(Of GeoLine)
        Dim result As New List(Of GeoLine)(If(lines?.Count, 0))
        If lines Is Nothing Then Return result

        For Each ln In lines
            result.Add(New GeoLine(ln.X1 / scale, ln.Y1 / scale, ln.X2 / scale, ln.Y2 / scale))
        Next

        Return result
    End Function





End Class
