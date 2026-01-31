Imports System.IO
Imports System.Text.Json

Imports PolyCut.Shared

Public Class ProjectSerializationService

    Private ReadOnly _jsonOptions As New JsonSerializerOptions With {
        .WriteIndented = True,
        .PropertyNameCaseInsensitive = True
    }

    ' --------------------
    ' SAVE / LOAD ProjectData
    ' --------------------

    Public Function SaveProject(filePath As String, drawables As IEnumerable(Of IDrawable), groups As IEnumerable(Of IDrawable)) As Boolean
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

            'File.WriteAllText(filePath, json)

            Return True
        Catch ex As Exception
            Application.GetService(Of SnackbarService)?.Show($"Failed to save project", ex.Message, WPF.Ui.Controls.ControlAppearance.Danger, Nothing, TimeSpan.FromSeconds(5))
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

            'json = File.ReadAllText(filePath)

            Return JsonSerializer.Deserialize(Of ProjectData)(json, _jsonOptions)
        Catch ex As Exception
            Application.GetService(Of SnackbarService)?.Show($"Failed to load project", ex.Message, WPF.Ui.Controls.ControlAppearance.Danger, Nothing, TimeSpan.FromSeconds(5))
            Return Nothing
        End Try
    End Function

    ' --------------------
    ' Build runtime objects
    ' --------------------
    Public Function BuildRuntimeModel(projectData As ProjectData, designerItemStyle As Style) As RuntimeProjectModel

        If projectData Is Nothing Then Return New RuntimeProjectModel With {
            .Drawables = New List(Of IDrawable)(),
            .Groups = New List(Of IDrawable)()
        }

        Dim drawableById As New Dictionary(Of Guid, IDrawable)
        Dim groupById As New Dictionary(Of Guid, IDrawable)

        ' 1) Create leaf drawables
        For Each dd In projectData.Drawables
            Dim fe = DrawableCodec.DeserializeDrawable(dd)
            If fe Is Nothing Then Continue For

            ' IMPORTANT: seed size for group-bound calculations BEFORE anything is measured
            If dd.Width > 0 AndAlso Not Double.IsNaN(dd.Width) Then fe.Width = dd.Width
            If dd.Height > 0 AndAlso Not Double.IsNaN(dd.Height) Then fe.Height = dd.Height

            Canvas.SetLeft(fe, dd.Left)
            Canvas.SetTop(fe, dd.Top)

            Dim d As IDrawable = BaseDrawable.DrawableFactory(fe)
            d.Name = dd.Name
            d.IsHidden = dd.IsHidden

            drawableById(dd.Id) = d
        Next

        ' 2) Create groups
        For Each gd In projectData.Groups
            Dim g As IDrawable
            If String.Equals(gd.GroupType, "NestedDrawableGroup", StringComparison.OrdinalIgnoreCase) Then
                Dim ng As New NestedDrawableGroup(gd.Name)
                ng.SetNativeSize(gd.NativeWidth, gd.NativeHeight)

                Dim fe = TryCast(ng.DrawableElement, FrameworkElement)
                If fe IsNot Nothing Then
                    fe.Width = gd.Width
                    fe.Height = gd.Height
                    Canvas.SetLeft(fe, gd.Left)
                    Canvas.SetTop(fe, gd.Top)
                End If

                g = ng
            Else
                g = New DrawableGroup(gd.Name)
            End If

            g.Name = gd.Name
            groupById(gd.Id) = g
        Next

        ' 3) Wire group membership
        For Each gd In projectData.Groups
            Dim g = groupById(gd.Id)

            For Each cid In gd.ChildIds
                If drawableById.ContainsKey(cid) Then
                    AddChildToGroup(g, drawableById(cid))
                ElseIf groupById.ContainsKey(cid) Then
                    AddChildToGroup(g, groupById(cid))
                End If
            Next
        Next

        ' 4) Wire parent groups
        For Each gd In projectData.Groups
            If Not gd.ParentGroupId.HasValue Then Continue For
            If groupById.ContainsKey(gd.Id) AndAlso groupById.ContainsKey(gd.ParentGroupId.Value) Then
                groupById(gd.Id).ParentGroup = groupById(gd.ParentGroupId.Value)
            End If
        Next

        ' 5) Rebuild nested visuals (deepest-first)
        Dim nestedGroups = groupById.Values.OfType(Of NestedDrawableGroup)().
                            OrderByDescending(Function(g) GetNestedDepth(g)).
                            ToList()

        For Each ng In nestedGroups
            ng.RebuildGroupVisualFromChildren(designerItemStyle)
        Next

        Return New RuntimeProjectModel With {
            .Drawables = drawableById.Values.ToList(),
            .Groups = groupById.Values.ToList(),
            .DrawableById = drawableById,
            .GroupById = groupById
        }
    End Function

    Private Shared Function GetNestedDepth(g As NestedDrawableGroup) As Integer
        Dim depth As Integer = 0
        Dim p = TryCast(g.ParentGroup, NestedDrawableGroup)
        While p IsNot Nothing
            depth += 1
            p = TryCast(p.ParentGroup, NestedDrawableGroup)
        End While
        Return depth
    End Function

    Private Shared Sub AddChildToGroup(group As IDrawable, child As IDrawable)
        If group Is Nothing OrElse child Is Nothing Then Return

        If TypeOf group Is NestedDrawableGroup Then
            DirectCast(group, NestedDrawableGroup).AddChild(child)
        ElseIf TypeOf group Is DrawableGroup Then
            DirectCast(group, DrawableGroup).AddChild(child)
        End If
        child.ParentGroup = group
    End Sub


    ' --------------------
    ' Serialize
    ' --------------------

    Public Function CreateProjectDataForClipboard(selected As IEnumerable(Of IDrawable), drawingGroup As DrawableGroup) As ProjectData

        Dim allGroups As HashSet(Of IDrawable) = Nothing
        Dim allLeaves As HashSet(Of IDrawable) = Nothing

        ProjectGraph.WalkGraph(selected, allGroups, allLeaves)

        Dim g = If(allGroups, New HashSet(Of IDrawable)())
        Dim l = If(allLeaves, New HashSet(Of IDrawable)())

        For Each d In l
            AddAncestors(d, g, drawingGroup)
        Next
        For Each grp In g.ToList()
            AddAncestors(grp, g, drawingGroup)
        Next

        Return CreateProjectDataFromSets(g.ToList(), l.ToList())
    End Function

    Public Function CreateProjectData(drawables As IEnumerable(Of IDrawable),
                                  groups As IEnumerable(Of IDrawable)) As ProjectData

        Dim allGroups As HashSet(Of IDrawable) = Nothing
        Dim allLeaves As HashSet(Of IDrawable) = Nothing

        ProjectGraph.WalkGraph(drawables, allGroups, allLeaves)
        ProjectGraph.WalkGraph(groups, allGroups, allLeaves)

        Dim groupList = If(allGroups, New HashSet(Of IDrawable)()).ToList()
        Dim leafList = If(allLeaves, New HashSet(Of IDrawable)()).ToList()

        Return CreateProjectDataFromSets(groupList, leafList)
    End Function


    Private Shared Function CreateProjectDataFromSets(groupList As List(Of IDrawable),
                                                  leafList As List(Of IDrawable)) As ProjectData

        Dim projectData As New ProjectData()

        If groupList Is Nothing OrElse groupList.Count = 0 Then
            ' still serialize drawables if any
            groupList = New List(Of IDrawable)
        End If
        If leafList Is Nothing OrElse leafList.Count = 0 Then
            leafList = New List(Of IDrawable)
        End If

        Dim groupIdMap As New Dictionary(Of IDrawable, Guid)(groupList.Count)
        For Each g In groupList
            groupIdMap(g) = Guid.NewGuid()
        Next

        Dim drawableIdMap As New Dictionary(Of IDrawable, Guid)(leafList.Count)
        For Each d In leafList
            drawableIdMap(d) = Guid.NewGuid()
        Next

        ' --- Groups ---
        For Each g In groupList
            Dim gd As New GroupData With {
            .Id = groupIdMap(g),
            .Name = g.Name,
            .GroupType = If(TypeOf g Is NestedDrawableGroup, "NestedDrawableGroup", "DrawableGroup"),
            .IsHidden = g.IsHidden
        }

            Dim parent = TryCast(g.ParentGroup, IDrawable)
            If parent IsNot Nothing Then
                Dim pid As Guid = Nothing
                If groupIdMap.TryGetValue(parent, pid) Then
                    gd.ParentGroupId = pid
                End If
            End If

            DrawableCodec.SerializeGroupWrapperState(g, gd)

            Dim ng = TryCast(g, NestedDrawableGroup)
            If ng IsNot Nothing Then
                Dim native = ng.GetNativeSize()
                gd.NativeWidth = native.Item1
                gd.NativeHeight = native.Item2
            End If

            For Each ch In ProjectGraph.GetGroupChildren(g)
                If ch Is Nothing Then Continue For

                If ch.IsAnyGroup() Then
                    Dim cid As Guid = Nothing
                    If groupIdMap.TryGetValue(ch, cid) Then gd.ChildIds.Add(cid)
                Else
                    Dim cid As Guid = Nothing
                    If drawableIdMap.TryGetValue(ch, cid) Then gd.ChildIds.Add(cid)
                End If
            Next

            projectData.Groups.Add(gd)
        Next

        ' --- Drawables ---
        For Each d In leafList
            Dim dd = DrawableCodec.SerializeDrawable(d, drawableIdMap(d))
            If dd Is Nothing Then Continue For

            Dim parent = TryCast(d.ParentGroup, IDrawable)
            If parent IsNot Nothing Then
                Dim pid As Guid = Nothing
                If groupIdMap.TryGetValue(parent, pid) Then
                    dd.ParentGroupId = pid
                End If
            End If

            projectData.Drawables.Add(dd)
        Next

        Return projectData
    End Function




    Private Shared Sub AddAncestors(item As IDrawable,
                               groups As HashSet(Of IDrawable),
                               drawingGroup As DrawableGroup)

        Dim p As IDrawable = item?.ParentGroup
        While p IsNot Nothing
            ' Never include Drawing Group in clipboard data; pasted loose items attach to current DrawingGroup.
            If drawingGroup IsNot Nothing AndAlso Object.ReferenceEquals(p, drawingGroup) Then Exit While

            ' Only groups matter as ancestors
            If p.IsAnyGroup() Then groups.Add(p)

            p = p.ParentGroup
        End While
    End Sub

End Class


Friend NotInheritable Class DrawableCodec

    Protected Friend Shared Function SerializeDrawable(drawable As IDrawable, id As Guid) As DrawableData
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
            Return Nothing
        End If

        Return data
    End Function

    Protected Friend Shared Sub SerializeGroupWrapperState(group As IDrawable, groupData As GroupData)
        Try
            Dim element = TryCast(group.DrawableElement, FrameworkElement)
            If element Is Nothing Then Return

            Dim wrapper = TryCast(element.Parent, ContentControl)
            If wrapper Is Nothing Then
                ' Not on canvas yet; fall back to element's Canvas position/size
                groupData.Left = CanvasUtil.GetLeftSafe(element)
                groupData.Top = CanvasUtil.GetTopSafe(element)
                groupData.Width = CanvasUtil.GetWidthSafe(element)
                groupData.Height = CanvasUtil.GetHeightSafe(element)
                Return
            End If

            groupData.Left = CanvasUtil.GetLeftSafe(wrapper)
            groupData.Top = CanvasUtil.GetTopSafe(wrapper)
            groupData.Width = wrapper.ActualWidth
            groupData.Height = wrapper.ActualHeight

            groupData.ZIndex = Panel.GetZIndex(wrapper)
            groupData.IsHidden = group.IsHidden

            Dim rt = TryCast(wrapper.RenderTransform, RotateTransform)
            If rt IsNot Nothing Then groupData.RotationAngle = rt.Angle

            Dim contentFe = TryCast(wrapper.Content, FrameworkElement)
            If contentFe IsNot Nothing Then
                Dim tg = TryCast(contentFe.RenderTransform, TransformGroup)
                If tg IsNot Nothing Then
                    Dim st = tg.Children.OfType(Of ScaleTransform)().FirstOrDefault()
                    If st IsNot Nothing Then
                        groupData.ScaleX = st.ScaleX
                        groupData.ScaleY = st.ScaleY
                    End If
                Else
                    Dim st = TryCast(contentFe.RenderTransform, ScaleTransform)
                    If st IsNot Nothing Then
                        groupData.ScaleX = st.ScaleX
                        groupData.ScaleY = st.ScaleY
                    End If
                End If
            End If

        Catch
        End Try
    End Sub

    Protected Friend Shared Function DeserializeDrawable(data As DrawableData) As FrameworkElement
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

        If TypeOf element Is Shape Then
            Dim shape = CType(element, Shape)
            shape.Stroke = DeserializeBrush(data.StrokeColor)
            shape.StrokeThickness = data.StrokeThickness
            shape.Fill = DeserializeBrush(data.FillColor)
        ElseIf TypeOf element Is TextBox Then
            Dim textBox = CType(element, TextBox)
            textBox.Foreground = DeserializeBrush(data.FillColor)
        End If

        If Math.Abs(data.ScaleX - 1.0) > 0.01 OrElse Math.Abs(data.ScaleY - 1.0) > 0.01 Then
            Dim tg As New TransformGroup()
            tg.Children.Add(New ScaleTransform(data.ScaleX, data.ScaleY))
            element.RenderTransform = tg
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