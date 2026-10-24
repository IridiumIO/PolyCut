Imports PolyCut.Shared
Imports IxMilia.Converters
Imports IxMilia.Dxf
Imports IxMilia.Dxf.Entities
Imports System.Globalization

Public Class DXFImportService : Implements IVectorImportService

    Private Const OutlineWidthMm As Double = 0.25

    Public Function ParseFromFile(path As String) As IEnumerable(Of IDrawable) Implements IVectorImportService.ParseFromFile
        Throw New NotSupportedException("Convert the DXF asynchronously before importing its SVG document.")
    End Function

    Public Function ParseFromDocument(svgDoc As Svg.SvgDocument, Optional sourceName As String = Nothing) As IEnumerable(Of IDrawable) Implements IVectorImportService.ParseFromDocument
        ' Size DXF wrappers from the toolpath itself. Restore the visual pen only after the ordinary importer has constructed all shapes and groups.
        Dim copy As Svg.SvgDocument
        Using buffer As New IO.MemoryStream()
            svgDoc.Write(buffer)
            buffer.Position = 0
            copy = Svg.SvgDocument.Open(Of Svg.SvgDocument)(buffer)
        End Using
        For Each element In copy.Descendants().OfType(Of Svg.SvgVisualElement)()
            element.StrokeWidth = New Svg.SvgUnit(0)
        Next
        Dim drawables = New SVGImportService().ParseFromDocument(copy, sourceName).ToArray()
        For Each drawable In drawables
            RestoreOutline(drawable, OutlineWidthMm)
        Next
        Return drawables
    End Function

    Private Shared Sub RestoreOutline(drawable As IDrawable, width As Double)
        Dim group = TryCast(drawable, NestedDrawableGroup)
        If group IsNot Nothing Then
            For Each child In group.GroupChildren
                RestoreOutline(child, width)
            Next
        Else
            ' Keep a zero-extent DXF centerline free of minimum layout padding.
            drawable.DrawableElement.MinWidth = 0
            drawable.DrawableElement.MinHeight = 0
            Dim path = TryCast(drawable.DrawableElement, Shapes.Path)
            If path IsNot Nothing AndAlso (path.Data.Bounds.Width < 0.000001 OrElse path.Data.Bounds.Height < 0.000001) Then
                path.Stretch = Stretch.None
                ' An explicit zero Width/Height creates an empty WPF layout clip even with ClipToBounds=False.
                ' Force the path to be measured at its natural size so it can be drawn at its native coordinates because I can't math. 
                path.Width = Double.NaN
                path.Height = Double.NaN
                path.MinWidth = 0.001
                path.MinHeight = 0.001
                path.HorizontalAlignment = HorizontalAlignment.Left
                path.VerticalAlignment = VerticalAlignment.Top
            End If
            drawable.StrokeThickness = width
        End If
    End Sub

    Public Async Function ConvertDXFToSVG(filepath As String) As Task(Of Svg.SvgDocument)

        Try
            Dim dxf = DxfFile.Load(filepath)
            ' Fix stupid negative Z arcs from Fusion sketches
            For Each circle In dxf.Entities.OfType(Of DxfCircle)()
                If circle.Normal.X = 0 AndAlso circle.Normal.Y = 0 AndAlso circle.Normal.Z < 0 Then
                    circle.Center = New DxfPoint(-circle.Center.X, circle.Center.Y, -circle.Center.Z)
                    Dim arc = TryCast(circle, DxfArc)
                    If arc IsNot Nothing Then
                        Dim oldStart = arc.StartAngle
                        arc.StartAngle = NormaliseAngle(180 - arc.EndAngle)
                        arc.EndAngle = NormaliseAngle(180 - oldStart)
                    End If
                    circle.Normal = DxfVector.ZAxis
                End If
            Next
            Dim bb = dxf.GetBoundingBox()
            If bb.Size.X <= 0 OrElse bb.Size.Y <= 0 Then bb = DxfBoundingBox.FromMinMax(dxf.Header.MinimumDrawingExtents, dxf.Header.MaximumDrawingExtents)

            Dim dxfRect = New ConverterDxfRect(bb)

            Dim svgRect = New ConverterSvgRect(bb.MaximumPoint.X - bb.MinimumPoint.X, bb.MaximumPoint.Y - bb.MinimumPoint.Y)

            Dim options = New DxfToSvgConverterOptions(dxfRect, svgRect)

            Dim svgxml = Await New DxfToSvgConverter().Convert(dxf, options)

            Dim mmPerUnit = MillimetresPerUnit(dxf.Header.DefaultDrawingUnits)
            svgxml.SetAttributeValue("width", (bb.Size.X * mmPerUnit).ToString("R", CultureInfo.InvariantCulture) & "mm")
            svgxml.SetAttributeValue("height", (bb.Size.Y * mmPerUnit).ToString("R", CultureInfo.InvariantCulture) & "mm")

            For Each element In svgxml.DescendantsAndSelf()
                If element.Attribute("stroke") IsNot Nothing Then element.SetAttributeValue("stroke", "#000000")
                If element.Attribute("fill") IsNot Nothing Then element.SetAttributeValue("fill", "#000000")
                If element.Attribute("stroke-width") IsNot Nothing Then
                    element.SetAttributeValue("stroke-width", (OutlineWidthMm / mmPerUnit).ToString("R", CultureInfo.InvariantCulture))
                End If
                element.Attribute("vector-effect")?.Remove()
                ' Keep straight DXF segments as paths, too. Still buggy as hell on the canvas but better than lines getting broken as well
                If element.Name.LocalName = "line" Then
                    Dim data = String.Format(CultureInfo.InvariantCulture, "M {0} {1} L {2} {3}",
                                             element.Attribute("x1").Value, element.Attribute("y1").Value,
                                             element.Attribute("x2").Value, element.Attribute("y2").Value)
                    element.Name = element.Name.Namespace + "path"
                    element.SetAttributeValue("d", data)
                    For Each attributeName In {"x1", "y1", "x2", "y2"}
                        element.Attribute(attributeName).Remove()
                    Next
                    element.SetAttributeValue("fill", "none")
                End If
            Next

            Using reader = svgxml.CreateReader()
                Return Svg.SvgDocument.Open(Of Svg.SvgDocument)(reader)
            End Using

        Catch ex As Exception

            Application.GetService(Of SnackbarService)().GenerateError("DXF Import Error", ex.Message, 3)

        End Try


    End Function

    Private Shared Function NormaliseAngle(angle As Double) As Double
        Return ((angle Mod 360) + 360) Mod 360
    End Function

    Private Shared Function MillimetresPerUnit(units As DxfUnits) As Double
        ' why tf does Dxf support parsecs and lightyears.
        ' (I hope I get a bug report that someone crashes PolyCut by trying to import something with lightyear dimensions)
        Dim factors = {1.0, 25.4, 304.8, 1609344.0, 1.0, 10.0, 1000.0, 1000000.0, '<normal
                       0.0000254, 0.0254, 914.4, 0.0000001, 0.000001, 0.001, '<still okay
                       100.0, 10000.0, 100000.0, 1000000000000.0, '<weird but at least its metric 
                       149597870700000.0, 9.4607304725808E+18, 3.0856775814913671E+19, '<fucked
                       1200000.0 / 3937, 100000.0 / 3937, 3600000.0 / 3937, 6336000000.0 / 3937} '<you lot are actually fucking stupid
        Dim index = CInt(units)
        If index < 0 OrElse index >= factors.Length Then Throw New NotSupportedException("Unsupported DXF units: " & units.ToString())
        Return factors(index)
    End Function


End Class
