
Imports System.Globalization
Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes
Imports Svg

Public Class TextElement : Implements IPathBasedElement

    Public Property Lines As List(Of Line) Implements IPathBasedElement.Lines
    Public Property Geo As PathGeometry Implements IPathBasedElement.Geo
    Public Property Config As ProcessorConfiguration Implements IPathBasedElement.Config

    Public Sub CompileElement(element As SvgVisualElement, cfg As ProcessorConfiguration) Implements IPathBasedElement.CompileElement
        Dim text = DirectCast(element, SvgText)
        Config = cfg
        Dim tp = text.Path(Nothing).PathData.Points.ToList
        Dim tt = text.Path(Nothing).PathData.Types.ToList


        For Each child In element.Children

            Dim tBase = DirectCast(child, SvgTextBase)
            If tBase IsNot Nothing Then
                Dim tBasePath As System.Drawing.Drawing2D.PathData = tBase.Path(Nothing).PathData


                For i = 0 To tBasePath.Points.Length - 1

                    Dim point = tBasePath.Points(i)
                    Dim type As Byte = tBasePath.Types(i)

                    tp.Add(point)
                    tt.Add(type)

                Next


            End If

        Next

        Dim geoPath As New System.Drawing.Drawing2D.GraphicsPath(tp.ToArray, tt.ToArray)
        geoPath.Flatten(New System.Drawing.Drawing2D.Matrix, Config.Tolerance)

        Dim subpaths As New List(Of List(Of Line))

        Dim currentSubpath As New List(Of Line)

        For i As Integer = 0 To geoPath.PathPoints.Length - 1

            Dim point = geoPath.PathPoints(i)
            Dim type As System.Drawing.Drawing2D.PathPointType = geoPath.PathTypes(i)

            If type = 0 Then
                currentSubpath = New List(Of Line)
            End If

            If type.HasFlag(System.Drawing.Drawing2D.PathPointType.Line) Then
                Dim line As New Line With {
                            .X1 = geoPath.PathPoints(i - 1).X,
                            .Y1 = geoPath.PathPoints(i - 1).Y,
                            .X2 = point.X,
                            .Y2 = point.Y
                        }
                currentSubpath.Add(line)
            End If

            If type.HasFlag(System.Drawing.Drawing2D.PathPointType.CloseSubpath) Then
                Dim line As New Line With {
                            .X1 = point.X,
                            .Y1 = point.Y,
                            .X2 = currentSubpath.First.X1,
                            .Y2 = currentSubpath.First.Y1
                        }
                currentSubpath.Add(line)
                subpaths.Add(currentSubpath)
            End If

        Next


        Dim generatedLines As New List(Of Line)

        For Each subpath In subpaths
            generatedLines.AddRange(subpath)
        Next
        Lines = generatedLines

        Lines = TransformLines(Lines, element.Transforms.GetMatrix)

        'Dim hatch1 = Fill.FillLines(Lines, 2, 0)
        'Dim hatch2 = Fill.FillLines(Lines, 2, 90)

        'Lines.AddRange(hatch1)
        'Lines.AddRange(hatch2)
        Lines = OffsetProcessor.ProcessOffsets(Lines, Config.Offset, Config.Overcut)

    End Sub

End Class
