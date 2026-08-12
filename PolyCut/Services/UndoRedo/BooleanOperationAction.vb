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
            Dim geometry = GeometryHitTestHelper.GetTransformedGeometry(drawable)
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
        newPath.Fill = _selectedItems.FirstOrDefault(Function(f) f.Fill IsNot Nothing AndAlso CType(f.Fill, SolidColorBrush).Color.A <> 0)?.Fill
        newPath.Stroke = _selectedItems.First.Stroke
        newPath.StrokeThickness = _selectedItems.First.StrokeThickness

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

    Public Sub Undo() Implements IUndoableAction.Undo
        _compositeAction?.Undo()
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        _compositeAction?.Redo()
    End Sub

End Class
