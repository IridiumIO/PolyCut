Imports PolyCut.[Shared]
Imports PolyCut.RichCanvas

Public Class BooleanOperationAction : Implements IUndoableAction

    Private ReadOnly _manager As IDrawableManager
    Private ReadOnly _selectedItems As List(Of IDrawable)
    Private ReadOnly _combineMode As GeometryCombineMode
    Private _compositeAction As CompositeAction
    Private _operationName As String

    Public Sub New(manager As IDrawableManager, selectedItems As IEnumerable(Of IDrawable), combineMode As GeometryCombineMode)
        _manager = manager
        _selectedItems = selectedItems.ToList()
        _combineMode = combineMode

        Select Case combineMode
            Case GeometryCombineMode.Union
                _operationName = "Union"
            Case GeometryCombineMode.Intersect
                _operationName = "Intersect"
            Case GeometryCombineMode.Exclude
                _operationName = "Subtract"
            Case GeometryCombineMode.Xor
                _operationName = "XOR"
        End Select
    End Sub

    Public ReadOnly Property Description As String Implements IUndoableAction.Description
        Get
            Return _operationName
        End Get
    End Property

    Public Function Execute() As Boolean Implements IUndoableAction.Execute
        If _selectedItems.Count < 2 Then Return False

        Dim mainVM = TryCast(_manager, MainViewModel)
        If mainVM Is Nothing Then Return False

        Dim geometries As New List(Of Geometry)
        For Each drawable In _selectedItems
            Dim geometry = GetTransformedGeometry(drawable)
            If geometry IsNot Nothing Then
                geometries.Add(geometry)
            End If
        Next

        If geometries.Count < 2 Then Return False

        Dim result As Geometry = geometries(0)
        For i = 1 To geometries.Count - 1
            result = New CombinedGeometry(_combineMode, result, geometries(i))
        Next

        Dim pathGeometry = result.GetFlattenedPathGeometry(0.05, ToleranceType.Absolute)
        If pathGeometry.Figures.Count = 0 OrElse pathGeometry.Bounds.IsEmpty Then
            Return False
        End If

        Dim bounds = pathGeometry.Bounds
        Dim localGeometry = CreateLocalGeometry(pathGeometry, bounds)
        Dim newPath = CreatePathElement(localGeometry, bounds)

        Dim parentGroups As New HashSet(Of DrawableGroup)()
        For Each drawable In _selectedItems
            Dim pg = mainVM.GetParentGroup(drawable)
            If pg IsNot Nothing Then parentGroups.Add(pg)
        Next

        Dim insertionGroup = mainVM.GetTopLevelGroup(mainVM.GetParentGroup(_selectedItems(0)))
        If insertionGroup Is Nothing Then insertionGroup = mainVM.DrawingGroup

        Dim actions As New List(Of IUndoableAction)()

        Dim addAction As New AddDrawableAction(_manager, newPath, insertionGroup)
        If Not addAction.Execute() Then Return False
        actions.Add(addAction)

        For Each drawable In _selectedItems
            Dim removeAction As New RemoveDrawableAction(_manager, drawable)
            If removeAction.Execute() Then
                actions.Add(removeAction)
            End If
        Next

        For Each grp In parentGroups
            If grp Is mainVM.DrawingGroup Then Continue For
            If Not grp.GroupChildren.Any() AndAlso Not mainVM.IsAncestorOf(grp, insertionGroup) Then
                Dim removeGroupAction As New RemoveGroupAction(_manager, grp)
                If removeGroupAction.Execute() Then
                    actions.Add(removeGroupAction)
                End If
            End If
        Next

        _compositeAction = New CompositeAction(actions)
        Return True
    End Function

    Private Function CreateLocalGeometry(pathGeometry As PathGeometry, bounds As Rect) As PathGeometry
        Dim localGeometry As New PathGeometry()
        For Each figure In pathGeometry.Figures
            Dim newFigure As New PathFigure() With {
                .StartPoint = New Point(figure.StartPoint.X - bounds.Left, figure.StartPoint.Y - bounds.Top),
                .IsClosed = figure.IsClosed,
                .IsFilled = figure.IsFilled
            }

            For Each segment In figure.Segments
                If TypeOf segment Is LineSegment Then
                    Dim line = CType(segment, LineSegment)
                    newFigure.Segments.Add(New LineSegment(
                        New Point(line.Point.X - bounds.Left, line.Point.Y - bounds.Top), line.IsStroked))
                ElseIf TypeOf segment Is PolyLineSegment Then
                    Dim polyLine = CType(segment, PolyLineSegment)
                    Dim newPoints As New PointCollection()
                    For Each pt In polyLine.Points
                        newPoints.Add(New Point(pt.X - bounds.Left, pt.Y - bounds.Top))
                    Next
                    newFigure.Segments.Add(New PolyLineSegment(newPoints, polyLine.IsStroked))
                ElseIf TypeOf segment Is BezierSegment Then
                    Dim bezier = CType(segment, BezierSegment)
                    newFigure.Segments.Add(New BezierSegment(
                        New Point(bezier.Point1.X - bounds.Left, bezier.Point1.Y - bounds.Top),
                        New Point(bezier.Point2.X - bounds.Left, bezier.Point2.Y - bounds.Top),
                        New Point(bezier.Point3.X - bounds.Left, bezier.Point3.Y - bounds.Top),
                        bezier.IsStroked))
                Else
                    newFigure.Segments.Add(segment)
                End If
            Next

            localGeometry.Figures.Add(newFigure)
        Next
        Return localGeometry
    End Function

    Private Function CreatePathElement(localGeometry As PathGeometry, bounds As Rect) As System.Windows.Shapes.Path
        Dim localBounds = localGeometry.Bounds
        Dim newPath As New System.Windows.Shapes.Path With {
            .Data = localGeometry,
            .Stroke = Brushes.Black,
            .StrokeThickness = 0.5,
            .Fill = Brushes.Transparent,
            .Stretch = Stretch.None,
            .Width = localBounds.Width,
            .Height = localBounds.Height
        }

        Canvas.SetLeft(newPath, bounds.Left)
        Canvas.SetTop(newPath, bounds.Top)
        Return newPath
    End Function

    Private Function GetTransformedGeometry(drawable As IDrawable) As Geometry
        If drawable?.DrawableElement Is Nothing Then Return Nothing

        Dim element = drawable.DrawableElement
        Dim wrapper = TryCast(element.Parent, ContentControl)
        If wrapper Is Nothing Then Return Nothing

        Dim geometry As Geometry = Nothing

        If TypeOf element Is Rectangle Then
            Dim rect = CType(element, Rectangle)
            geometry = New RectangleGeometry(New Rect(0, 0, rect.ActualWidth, rect.ActualHeight))

        ElseIf TypeOf element Is Ellipse Then
            Dim ellipse = CType(element, Ellipse)
            Dim radiusX = ellipse.ActualWidth / 2
            Dim radiusY = ellipse.ActualHeight / 2
            geometry = New EllipseGeometry(New Point(radiusX, radiusY), radiusX, radiusY)

        ElseIf TypeOf element Is Line Then
            Dim line = CType(element, Line)
            Dim lineGeometry As New LineGeometry(New Point(line.X1, line.Y1), New Point(line.X2, line.Y2))
            Dim thickness = If(line.StrokeThickness > 0, line.StrokeThickness, 1.0)
            geometry = lineGeometry.GetWidenedPathGeometry(New Pen(Brushes.Black, thickness))

        ElseIf TypeOf element Is System.Windows.Shapes.Path Then
            Dim path = CType(element, System.Windows.Shapes.Path)
            If path.Data IsNot Nothing Then
                geometry = path.Data.Clone()
            End If

        ElseIf TypeOf element Is TextBox Then
            Dim textBox = CType(element, TextBox)
            If Not String.IsNullOrEmpty(textBox.Text) Then
                Dim formattedText As New FormattedText(
                    textBox.Text,
                    Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    New Typeface(textBox.FontFamily, textBox.FontStyle, textBox.FontWeight, textBox.FontStretch),
                    textBox.FontSize,
                    Brushes.Black,
                    1.0)

                geometry = formattedText.BuildGeometry(New Point(0, 0))
            End If
        End If

        If geometry Is Nothing Then Return Nothing

        Dim elementTransformGroup = TryCast(element.RenderTransform, TransformGroup)
        If elementTransformGroup IsNot Nothing Then
            Dim elementScale = elementTransformGroup.Children.OfType(Of ScaleTransform)().FirstOrDefault()
            If elementScale IsNot Nothing Then
                Dim scaleTransform = New ScaleTransform(elementScale.ScaleX, elementScale.ScaleY,
                    geometry.Bounds.Width / 2, geometry.Bounds.Height / 2)
                geometry = Geometry.Combine(geometry, geometry, GeometryCombineMode.Union, scaleTransform)
            End If
        End If

        Dim transformGroup As New TransformGroup()

        If Not TypeOf element Is TextBox Then
            If geometry.Bounds.Width > 0 AndAlso geometry.Bounds.Height > 0 Then
                Dim scaleX = wrapper.ActualWidth / geometry.Bounds.Width
                Dim scaleY = wrapper.ActualHeight / geometry.Bounds.Height
                transformGroup.Children.Add(New ScaleTransform(scaleX, scaleY))
            End If
        End If

        Dim rotateTransform = TryCast(wrapper.RenderTransform, RotateTransform)
        If rotateTransform IsNot Nothing Then
            transformGroup.Children.Add(New RotateTransform(rotateTransform.Angle,
                wrapper.ActualWidth / 2, wrapper.ActualHeight / 2))
        End If

        Dim left = Canvas.GetLeft(wrapper)
        Dim top = Canvas.GetTop(wrapper)
        If Not Double.IsNaN(left) AndAlso Not Double.IsNaN(top) Then
            If TypeOf element Is TextBox Then
                transformGroup.Children.Add(New TranslateTransform(left + 3, top + 1))
            Else
                transformGroup.Children.Add(New TranslateTransform(left, top))
            End If
        End If

        Return Geometry.Combine(geometry, geometry, GeometryCombineMode.Union, transformGroup)
    End Function

    Public Sub Undo() Implements IUndoableAction.Undo
        _compositeAction?.Undo()
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        _compositeAction?.Redo()
    End Sub

End Class
