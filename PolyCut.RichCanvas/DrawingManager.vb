
Imports PolyCut.Shared

Public Class DrawingManager
    Private _currentShape As Shape
    Private _startPos As Point

    Public Shared SNAPTOGRID As Boolean = False

    Public ReadOnly Property TextEditor As TextEditingController

    Public Sub New()
        TextEditor = New TextEditingController()
        AddHandler TextEditor.SessionFinished, AddressOf OnTextSessionFinished
    End Sub

    Private Sub OnTextSessionFinished(element As UIElement)
        RaiseEvent DrawingFinished(Me, element)
    End Sub

    Public Sub StartDrawing(mode As CanvasMode, startPoint As Point, pcanvas As PolyCanvas)
        TextEditor.CommitActiveText(pcanvas)
        _startPos = startPoint

        Select Case mode
            Case CanvasMode.Line
                _currentShape = CreateLine(startPoint)
            Case CanvasMode.Rectangle, CanvasMode.Ellipse, CanvasMode.RegistrationMark
                _currentShape = CreateShape(startPoint, mode)
            Case CanvasMode.Path
                _currentShape = CreatePen(startPoint)
            Case CanvasMode.Text
                _currentShape = Nothing
        End Select

        If _currentShape IsNot Nothing Then
            pcanvas.Children.Add(_currentShape)
        End If
    End Sub



    Public Sub UpdateDrawing(mode As CanvasMode, currentPoint As Point, squareAspect As Boolean, snapToGrid As Boolean)
        If _currentShape Is Nothing Then Return
        DrawingManager.SNAPTOGRID = snapToGrid
        Select Case mode
            Case CanvasMode.Line
                UpdateLine(DirectCast(_currentShape, Line), currentPoint, squareAspect)
            Case CanvasMode.Rectangle, CanvasMode.RegistrationMark, CanvasMode.Ellipse
                UpdateRectShape(DirectCast(_currentShape, Shape), currentPoint, squareAspect)
            Case CanvasMode.Path
                UpdatePen(DirectCast(_currentShape, Polyline), currentPoint)
        End Select
    End Sub

    Public Event DrawingFinished(sender As Object, element As UIElement)

    Public Sub FinishDrawing(mode As CanvasMode, pCanvas As PolyCanvas, ctextbox As TextBox)

        If mode = CanvasMode.Text Then
            TextEditor.StartNewText(_startPos, ctextbox.FontSize, ctextbox.FontFamily, pCanvas)
            Return
        End If


        If _currentShape Is Nothing Then Return

        Dim currentCursorPosition As Point = Mouse.GetPosition(pCanvas)

        If mode = CanvasMode.Line Then
            Dim line As Line = DirectCast(_currentShape, Line)
            _currentShape = FinaliseLine(line)

            If IsClick(_startPos, currentCursorPosition) Then
                pCanvas.Children.Remove(_currentShape)
                _currentShape = Nothing
                Return
            End If

        ElseIf mode = CanvasMode.Path Then
            Dim polyline As Polyline = DirectCast(_currentShape, Polyline)
            pCanvas.Children.Remove(polyline)
            _currentShape = FinalisePolyline(polyline)

            If polyline.Points.Count < 2 Then
                _currentShape = Nothing
                Return
            End If

        Else

            If IsClick(_startPos, currentCursorPosition) Then
                pCanvas.Children.Remove(_currentShape)
                _currentShape = Nothing
                Return
            End If
        End If


        RaiseEvent DrawingFinished(Me, _currentShape)

        pCanvas.Children.Remove(_currentShape)
        _currentShape = Nothing
    End Sub

    Public ReadOnly Property IsDrawing As Boolean
        Get
            Return _currentShape IsNot Nothing
        End Get
    End Property

    Public Sub CancelDrawing(pcanvas As PolyCanvas)
        TextEditor.CancelActiveText(pcanvas)
        If _currentShape Is Nothing Then Return
        If pcanvas IsNot Nothing AndAlso pcanvas.Children.Contains(_currentShape) Then
            pcanvas.Children.Remove(_currentShape)
        End If
        _currentShape = Nothing
    End Sub

    Private Shared Function IsClick(start As Point, current As Point) As Boolean
        Dim dx = current.X - start.X
        Dim dy = current.Y - start.Y
        Return dx * dx + dy * dy < 2
    End Function

    Private Shared Function CreateLine(startPoint As Point) As Line
        Dim line As New Line With {
            .Stroke = Brushes.Black,
            .StrokeThickness = 0.5,
            .X1 = startPoint.X,
            .Y1 = startPoint.Y,
            .X2 = startPoint.X,
            .Y2 = startPoint.Y,
            .StrokeStartLineCap = PenLineCap.Square,
            .StrokeEndLineCap = PenLineCap.Square,
            .StrokeDashCap = PenLineCap.Square
        }

        If SNAPTOGRID Then
            line.X1 = Math.Round((line.X1 - PolyCanvas.GridDefinition.InsetLeft) / PolyCanvas.GridDefinition.Spacing) * PolyCanvas.GridDefinition.Spacing + PolyCanvas.GridDefinition.InsetLeft
            line.Y1 = Math.Round((line.Y1 - PolyCanvas.GridDefinition.InsetTop) / PolyCanvas.GridDefinition.Spacing) * PolyCanvas.GridDefinition.Spacing + PolyCanvas.GridDefinition.InsetTop
        End If

        Return line

    End Function

    Private Shared Function CreatePen(startPoint As Point) As Polyline

        If SNAPTOGRID Then startPoint = SnapPoint(startPoint)

        Dim polyline As New Polyline With {
            .Stroke = Brushes.Black,
            .StrokeThickness = 0.5,
            .StrokeStartLineCap = PenLineCap.Round,
            .StrokeEndLineCap = PenLineCap.Round,
            .StrokeDashCap = PenLineCap.Round,
            .StrokeLineJoin = PenLineJoin.Round,
            .Points = New PointCollection() From {startPoint}
        }
        Return polyline
    End Function

    Private Shared Function CreateShape(startPoint As Point, mode As CanvasMode) As Shape

        If SNAPTOGRID Then startPoint = SnapPoint(startPoint)

        Dim shape As Shape

        If mode = CanvasMode.Ellipse Then
            shape = New Ellipse
        Else
            shape = New Rectangle

            If mode = CanvasMode.RegistrationMark Then
                RegistrationMarkHelper.SetIsRegistrationMark(shape, True)
            End If
        End If

        Dim isRegistrationMark As Boolean = mode = CanvasMode.RegistrationMark

        shape.Stroke = If(isRegistrationMark, Brushes.Magenta, Brushes.Black)
        shape.StrokeThickness = 0.5
        shape.Fill = If(isRegistrationMark, Brushes.Magenta, Brushes.Transparent)
        shape.Width = 0
        shape.Height = 0
        shape.StrokeStartLineCap = PenLineCap.Round
        shape.StrokeEndLineCap = PenLineCap.Round
        shape.StrokeLineJoin = PenLineJoin.Round

        Canvas.SetLeft(shape, startPoint.X)
        Canvas.SetTop(shape, startPoint.Y)

        Return shape
    End Function


    Private Sub UpdateLine(line As Line, currentPoint As Point, squareAspect As Boolean)

        Dim newPoint As Point = currentPoint

        If squareAspect Then
            Dim dx = currentPoint.X - _startPos.X
            Dim dy = currentPoint.Y - _startPos.Y
            Dim angle = Math.Atan2(dy, dx) * (180 / Math.PI)
            Dim snappedAngle = Math.Round(angle / 45) * 45
            Dim length = Math.Sqrt(dx * dx + dy * dy)
            Dim snappedDx = Math.Cos(snappedAngle * (Math.PI / 180)) * length
            Dim snappedDy = Math.Sin(snappedAngle * (Math.PI / 180)) * length
            newPoint.X = _startPos.X + snappedDx
            newPoint.Y = _startPos.Y + snappedDy
        End If

        If SNAPTOGRID Then newPoint = SnapPoint(newPoint)

        line.X2 = newPoint.X
        line.Y2 = newPoint.Y

    End Sub

    Private Shared Sub UpdatePen(polyline As Polyline, currentPoint As Point)

        If SNAPTOGRID Then currentPoint = SnapPoint(currentPoint)

        Dim lastPoint = polyline.Points(polyline.Points.Count - 1)
        If lastPoint <> currentPoint Then
            polyline.Points.Add(currentPoint)
        End If
    End Sub

    Private Sub UpdateRectShape(shape As Shape, currentPoint As Point, squareAspect As Boolean)

        Dim sp = _startPos
        Dim cp = currentPoint

        If SNAPTOGRID Then
            sp = SnapPoint(sp)
            cp = SnapPoint(cp)
        End If

        Dim x = Math.Min(cp.X, sp.X)
        Dim y = Math.Min(cp.Y, sp.Y)
        Dim w = Math.Abs(cp.X - sp.X)
        Dim h = Math.Abs(cp.Y - sp.Y)

        If squareAspect Then
            Dim size = Math.Max(w, h)
            shape.Width = size
            shape.Height = size
            Canvas.SetLeft(shape, If(cp.X < sp.X, sp.X - size, sp.X))
            Canvas.SetTop(shape, If(cp.Y < sp.Y, sp.Y - size, sp.Y))
        Else
            shape.Width = w
            shape.Height = h
            Canvas.SetLeft(shape, x)
            Canvas.SetTop(shape, y)
        End If
    End Sub

    Private Shared Function SnapPoint(p As Point) As Point
        Dim gd = PolyCanvas.GridDefinition
        Dim s = gd.Spacing
        Dim x = Math.Round((p.X - gd.InsetLeft) / s, MidpointRounding.AwayFromZero) * s + gd.InsetLeft
        Dim y = Math.Round((p.Y - gd.InsetTop) / s, MidpointRounding.AwayFromZero) * s + gd.InsetTop
        Return New Point(x, y)
    End Function

    Private Shared Function FinaliseLine(l As Line) As Line
        Dim negativeDirection As Boolean = l.X2 < l.X1 OrElse (l.X1 = l.X2 AndAlso l.Y2 < l.Y1)

        If negativeDirection Then
            Dim tempX As Double = l.X1
            Dim tempY As Double = l.Y1

            l.X1 = l.X2
            l.Y1 = l.Y2
            l.X2 = tempX
            l.Y2 = tempY

        End If

        Dim offsetX As Double = l.X1 - l.StrokeThickness / 2
        Dim offsetY As Double = Math.Min(l.Y1, l.Y2) - l.StrokeThickness / 2

        l.X1 -= offsetX
        l.X2 -= offsetX
        l.Y1 -= offsetY
        l.Y2 -= offsetY

        Canvas.SetLeft(l, offsetX)
        Canvas.SetTop(l, offsetY)

        Return l

    End Function



    Private Shared Function FinalisePolyline(polyline As Polyline) As Path

        Dim isCtrlPressed As Boolean = Keyboard.IsKeyDown(Key.LeftCtrl) OrElse Keyboard.IsKeyDown(Key.RightCtrl)

        polyline.Points = RamerDouglasPeucker(polyline.Points, epsilon:=1.0)



        Dim minX As Double = polyline.Points.Min(Function(p) p.X)
        Dim minY As Double = polyline.Points.Min(Function(p) p.Y)

        Dim offsetX As Double = minX - polyline.StrokeThickness / 2
        Dim offsetY As Double = minY - polyline.StrokeThickness / 2
        For i As Integer = 0 To polyline.Points.Count - 1
            Dim point As Point = polyline.Points(i)
            polyline.Points(i) = New Point(point.X - offsetX, point.Y - offsetY)
        Next

        Dim path As Path = ConvertPolylineToBezierPath(polyline, smoothingFactor:=If(isCtrlPressed, 0, 0.1))
        If path Is Nothing Then Return Nothing

        Dim bounds As Rect = path.Data.Bounds
        path.Width = bounds.Width + polyline.StrokeThickness
        path.Height = bounds.Height + polyline.StrokeThickness

        Canvas.SetLeft(path, offsetX)
        Canvas.SetTop(path, offsetY)

        Return path

    End Function

    Private Shared Function ConvertPolylineToBezierPath(polyline As Polyline, smoothingFactor As Double) As Path
        If polyline.Points.Count < 2 Then
            Dim singlePoint As Point = polyline.Points(0)

            Dim spathFigure As New PathFigure With {
                .StartPoint = singlePoint,
                .IsClosed = False
            }

            Return CreatePath(spathFigure, polyline.Stroke, polyline.StrokeThickness)
        End If

        ' Check if the final segment is nearly at the start point
        Dim startPoint As Point = polyline.Points(0)
        Dim endPoint As Point = polyline.Points(polyline.Points.Count - 1)
        Dim deltaX As Double = endPoint.X - startPoint.X
        Dim deltaY As Double = endPoint.Y - startPoint.Y
        Dim distance As Double = Math.Sqrt(deltaX * deltaX + deltaY * deltaY)

        ' Close the path if the distance is below a threshold
        Dim closeThreshold As Double = 5.0 ' Adjust this value as needed
        Dim shouldClose = distance <= closeThreshold
        If shouldClose Then
            polyline.Points(polyline.Points.Count - 1) = startPoint
        End If

        ' Generate Bézier control points
        Dim bezierSegments = GenerateBezierControlPoints(polyline.Points, smoothingFactor)

        ' Create a PathFigure to hold the segments
        Dim pathFigure As New PathFigure With {
            .StartPoint = polyline.Points(0),
            .IsClosed = shouldClose
        }




        For Each segment In bezierSegments
            pathFigure.Segments.Add(segment)
        Next

        Return CreatePath(pathFigure, polyline.Stroke, 0.5)
    End Function

    Private Shared Function CreatePath(figure As PathFigure, stroke As Brush, strokeThickness As Double) As Path
        Dim pathGeometry As New PathGeometry()
        pathGeometry.Figures.Add(figure)

        Return New Path With {
            .Stroke = stroke,
            .StrokeThickness = strokeThickness,
            .Data = pathGeometry
        }
    End Function

    Private Shared Function GenerateBezierControlPoints(points As PointCollection, smoothingFactor As Double) As List(Of BezierSegment)
        Dim bezierSegments As New List(Of BezierSegment)()

        If points.Count < 2 Then
            ' Not enough points to create Bézier curves
            Return bezierSegments
        End If

        For i As Integer = 0 To points.Count - 2
            Dim p0 As Point = If(i = 0, points(i), points(i - 1)) ' Previous point or current point for the first segment
            Dim p1 As Point = points(i) ' Current point
            Dim p2 As Point = points(i + 1) ' Next point
            Dim p3 As Point = If(i + 2 < points.Count, points(i + 2), points(i + 1)) ' Next-next point or next point for the last segment

            ' Calculate control points
            Dim cp1 As New Point(
                p1.X + (p2.X - p0.X) * smoothingFactor,
                p1.Y + (p2.Y - p0.Y) * smoothingFactor
            )

            Dim cp2 As New Point(
                p2.X - (p3.X - p1.X) * smoothingFactor,
                p2.Y - (p3.Y - p1.Y) * smoothingFactor
            )

            ' Create a Bézier segment
            bezierSegments.Add(New BezierSegment(cp1, cp2, p2, True))
        Next

        Return bezierSegments
    End Function

    Private Shared Function RamerDouglasPeucker(points As PointCollection, epsilon As Double) As PointCollection
        If points.Count < 3 Then
            ' If there are fewer than 3 points, return the original points
            Return points
        End If

        ' Find the point farthest from the line segment between the first and last points
        Dim firstPoint As Point = points(0)
        Dim lastPoint As Point = points(points.Count - 1)
        Dim maxDistance As Double = 0
        Dim index As Integer = 0

        For i As Integer = 1 To points.Count - 2
            Dim distance As Double = PerpendicularDistance(points(i), firstPoint, lastPoint)
            If distance > maxDistance Then
                maxDistance = distance
                index = i
            End If
        Next

        ' If the maximum distance is greater than the tolerance, recursively simplify
        If maxDistance > epsilon Then
            ' Recursively simplify the segments
            Dim leftSegment As PointCollection = RamerDouglasPeucker(New PointCollection(points.Take(index + 1)), epsilon)
            Dim rightSegment As PointCollection = RamerDouglasPeucker(New PointCollection(points.Skip(index)), epsilon)

            ' Combine the results, excluding the duplicate point at the junction
            Dim result As New PointCollection(leftSegment)
            result.RemoveAt(result.Count - 1)
            For Each point In rightSegment
                result.Add(point)
            Next

            Return result
        Else
            ' If the maximum distance is less than the tolerance, return the endpoints
            Return New PointCollection() From {firstPoint, lastPoint}
        End If
    End Function

    Private Shared Function PerpendicularDistance(point As Point, lineStart As Point, lineEnd As Point) As Double
        Dim dx As Double = lineEnd.X - lineStart.X
        Dim dy As Double = lineEnd.Y - lineStart.Y

        ' If the line segment is a point, return the distance to the point
        If dx = 0 AndAlso dy = 0 Then
            Return Math.Sqrt((point.X - lineStart.X) ^ 2 + (point.Y - lineStart.Y) ^ 2)
        End If

        ' Calculate the projection of the point onto the line
        Dim t As Double = ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / (dx * dx + dy * dy)
        t = Math.Max(0, Math.Min(1, t)) ' Clamp t to the range [0, 1]

        ' Find the closest point on the line
        Dim closestPoint As New Point(lineStart.X + t * dx, lineStart.Y + t * dy)

        ' Return the distance from the point to the closest point on the line
        Return Math.Sqrt((point.X - closestPoint.X) ^ 2 + (point.Y - closestPoint.Y) ^ 2)
    End Function

End Class