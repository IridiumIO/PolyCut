Imports System.IO
Imports System.Text.Json

Imports PolyCut.Shared
Imports PolyCut.Shared.Project

Public Class ProjectSerializationService

        Private ReadOnly _jsonOptions As New JsonSerializerOptions With {
            .WriteIndented = True,
            .PropertyNameCaseInsensitive = True
        }


    Public Function SaveProject(filePath As String, drawables As IEnumerable(Of IDrawable), groups As IEnumerable(Of DrawableGroup)) As Boolean
        Try
            Dim projectData = CreateProjectData(drawables, groups)
            Dim json = JsonSerializer.Serialize(projectData, _jsonOptions)

            Using stream As New FileStream(filePath, FileMode.Create)
                Using gzip As New IO.Compression.GZipStream(stream, IO.Compression.CompressionMode.Compress)
                    Using writer As New StreamWriter(gzip)
                        writer.Write(json)
                    End Using
                End Using
            End Using

            ' File.WriteAllText(filePath, json)
            Return True
        Catch ex As Exception
            Debug.WriteLine($"Failed to save project: {ex.Message}")
            Return False
        End Try
    End Function


    Public Function LoadProject(filePath As String) As ProjectData
        Try
            If Not File.Exists(filePath) Then Return Nothing

            Dim json As String

            Using stream As New FileStream(filePath, FileMode.Open)
                Using gzip As New IO.Compression.GZipStream(stream, IO.Compression.CompressionMode.Decompress)
                    Using reader As New StreamReader(gzip)
                        json = reader.ReadToEnd()
                    End Using
                End Using
            End Using

            'Dim json = File.ReadAllText(filePath)

            Return JsonSerializer.Deserialize(Of ProjectData)(json, _jsonOptions)
        Catch ex As Exception
            Debug.WriteLine($"Failed to load project: {ex.Message}")
            Return Nothing
        End Try
    End Function


    Private Function CreateProjectData(drawables As IEnumerable(Of IDrawable), groups As IEnumerable(Of DrawableGroup)) As ProjectData
        Dim projectData As New ProjectData()
        Dim drawableIdMap As New Dictionary(Of IDrawable, Guid)
        Dim groupIdMap As New Dictionary(Of DrawableGroup, Guid)

        ' Assign IDs to all groups first
        For Each group In groups
            groupIdMap(group) = Guid.NewGuid()
        Next

        ' Assign IDs to all drawables
        For Each drawable In drawables
            If TypeOf drawable Is DrawableGroup Then Continue For ' Skip groups in drawable collection..need to do this at spme point
            drawableIdMap(drawable) = Guid.NewGuid()
        Next

        ' Serialize groups with hierarchy
        For Each group In groups
            Dim groupData As New GroupData With {
                .Id = groupIdMap(group),
                .Name = group.Name
            }

            ' Set parent group ID if exists
            Dim parentGroup = TryCast(group.ParentGroup, DrawableGroup)
            If parentGroup IsNot Nothing AndAlso groupIdMap.ContainsKey(parentGroup) Then
                groupData.ParentGroupId = groupIdMap(parentGroup)
            End If

            ' Add child drawable IDs
            For Each child In group.GroupChildren
                If drawableIdMap.ContainsKey(child) Then
                    groupData.ChildIds.Add(drawableIdMap(child))
                End If
            Next

            projectData.Groups.Add(groupData)
        Next

        ' Serialize drawables
        For Each drawable In drawables
            If TypeOf drawable Is DrawableGroup Then Continue For

            Dim drawableData = SerializeDrawable(drawable, drawableIdMap(drawable))
            If drawableData IsNot Nothing Then
                ' Set parent group ID
                Dim parentGroup = TryCast(drawable.ParentGroup, DrawableGroup)
                If parentGroup IsNot Nothing AndAlso groupIdMap.ContainsKey(parentGroup) Then
                    drawableData.ParentGroupId = groupIdMap(parentGroup)
                End If

                projectData.Drawables.Add(drawableData)
            End If
        Next

        Return projectData
    End Function

    Private Function SerializeDrawable(drawable As IDrawable, id As Guid) As DrawableData
        If drawable?.DrawableElement Is Nothing Then Return Nothing

        Dim element = drawable.DrawableElement
        Dim wrapper = TryCast(element.Parent, ContentControl)
        If wrapper Is Nothing Then Return Nothing

        Dim data As New DrawableData With {
            .Id = id,
            .Name = drawable.Name,
            .Left = Canvas.GetLeft(wrapper),
            .Top = Canvas.GetTop(wrapper),
            .Width = wrapper.ActualWidth,
            .Height = wrapper.ActualHeight,
            .IsHidden = drawable.IsHidden,
            .ZIndex = Panel.GetZIndex(wrapper)
        }

        If Double.IsNaN(data.Left) Then data.Left = 0
        If Double.IsNaN(data.Top) Then data.Top = 0

        ' Rotation
        Dim rotateTransform = TryCast(wrapper.RenderTransform, RotateTransform)
        If rotateTransform IsNot Nothing Then
            data.RotationAngle = rotateTransform.Angle
        End If

        ' Scale (from element's render transform)
        Dim elementTransformGroup = TryCast(element.RenderTransform, TransformGroup)
        If elementTransformGroup IsNot Nothing Then
            Dim scale = elementTransformGroup.Children.OfType(Of ScaleTransform)().FirstOrDefault()
            If scale IsNot Nothing Then
                data.ScaleX = scale.ScaleX
                data.ScaleY = scale.ScaleY
            End If
        End If

        ' Visual properties
        data.StrokeColor = SerializeBrush(drawable.Stroke)
        data.StrokeThickness = drawable.StrokeThickness
        data.FillColor = SerializeBrush(drawable.Fill)

        ' Type-specific serialization
        If TypeOf element Is Shapes.Path Then
            data.Type = "Path"
            Dim path = CType(element, Shapes.Path)
            data.PathData = path.Data?.ToString()

        ElseIf TypeOf element Is Rectangle Then
            data.Type = "Rectangle"

        ElseIf TypeOf element Is Ellipse Then
            data.Type = "Ellipse"

        ElseIf TypeOf element Is Line Then
            data.Type = "Line"
            Dim line = CType(element, Line)
            data.LineX1 = line.X1
            data.LineY1 = line.Y1
            data.LineX2 = line.X2
            data.LineY2 = line.Y2

        ElseIf TypeOf element Is TextBox Then
            data.Type = "Text"
            Dim textBox = CType(element, TextBox)
            data.TextContent = textBox.Text
            data.FontFamily = textBox.FontFamily.Source
            data.FontSize = textBox.FontSize

        Else
            Return Nothing ' Unknown type
        End If

        Return data
    End Function


    Public Function DeserializeDrawable(data As DrawableData) As FrameworkElement
        If data Is Nothing Then Return Nothing

        Dim element As FrameworkElement = Nothing

        Select Case data.Type
            Case "Path"
                Dim path As New System.Windows.Shapes.Path With {
                    .Stretch = Stretch.Fill,
                    .Data = If(Not String.IsNullOrEmpty(data.PathData),
                                Geometry.Parse(data.PathData),
                                Nothing)
                }
                element = path

            Case "Rectangle"
                element = New Rectangle()

            Case "Ellipse"
                element = New Ellipse()

            Case "Line"
                element = New Line With {
                    .X1 = data.LineX1,
                    .Y1 = data.LineY1,
                    .X2 = data.LineX2,
                    .Y2 = data.LineY2
                }

            Case "Text"
                element = New TextBox With {
                    .Width = Double.NaN,
                    .Height = Double.NaN,
                    .Background = Brushes.Transparent,
                    .BorderBrush = Brushes.Transparent,
                    .Foreground = Brushes.Black,
                    .BorderThickness = New Thickness(1),
                    .Style = Nothing,
                    .Text = data.TextContent,
                    .AcceptsReturn = False,
                    .AcceptsTab = True,
                    .FontSize = If(data.FontSize > 0, data.FontSize, 14),
                    .FontFamily = New FontFamily(If(String.IsNullOrEmpty(data.FontFamily), "Calibri", data.FontFamily)),
                    .FontWeight = FontWeights.Regular,
                    .Padding = New Thickness(0)
                }

            Case Else
                Return Nothing
        End Select

        If element Is Nothing Then Return Nothing

        ' Apply visual properties
        If TypeOf element Is Shape Then
            Dim shape = CType(element, Shape)
            shape.Stroke = DeserializeBrush(data.StrokeColor)
            shape.StrokeThickness = data.StrokeThickness
            shape.Fill = DeserializeBrush(data.FillColor)
        ElseIf TypeOf element Is TextBox Then
            Dim textBox = CType(element, TextBox)
            textBox.Foreground = DeserializeBrush(data.FillColor)
        End If

        ' Apply scale transform to element (use 0.01 threshold because of floating point bullshit)
        If Math.Abs(data.ScaleX - 1.0) > 0.01 OrElse Math.Abs(data.ScaleY - 1.0) > 0.01 Then
            Dim transformGroup As New TransformGroup()
            transformGroup.Children.Add(New ScaleTransform(data.ScaleX, data.ScaleY))
            element.RenderTransform = transformGroup
            element.RenderTransformOrigin = New Point(0.5, 0.5)
        End If

        Return element
    End Function

    Public Shared Function SerializeBrush(brush As Brush) As String
        If brush Is Nothing Then Return Nothing

        If TypeOf brush Is SolidColorBrush Then
            Dim solidBrush = CType(brush, SolidColorBrush)
            Return solidBrush.Color.ToString()
        End If

        Return Nothing
    End Function


    Public Shared Function DeserializeBrush(colorString As String) As Brush
        If String.IsNullOrEmpty(colorString) Then Return Brushes.Black

        Try
            Dim converter As New BrushConverter()
            Return CType(converter.ConvertFromString(colorString), Brush)
        Catch
            Return Brushes.Black
        End Try
    End Function

End Class
