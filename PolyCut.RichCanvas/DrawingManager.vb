
Imports PolyCut.Shared

Public Class DrawingManager
    Private _currentShape As Shape
    Private _startPos As Point

    Public Sub StartDrawing(mode As CanvasMode, startPoint As Point, pcanvas As PolyCanvas)
        _startPos = startPoint

        Select Case mode
            Case CanvasMode.Line
                _currentShape = CreateLine(startPoint)
            Case CanvasMode.Rectangle
                _currentShape = CreateRectangle(startPoint)
            Case CanvasMode.Ellipse
                _currentShape = CreateEllipse(startPoint)
            Case CanvasMode.Path
                _currentShape = CreatePen(startPoint)

        End Select

        If _currentShape IsNot Nothing Then
            pcanvas.Children.Add(_currentShape)
        End If
    End Sub

    Public Sub UpdateDrawing(mode As CanvasMode, currentPoint As Point, squareAspect As Boolean)
        If _currentShape Is Nothing Then Return

        Select Case mode
            Case CanvasMode.Line
                UpdateLine(DirectCast(_currentShape, Line), currentPoint, squareAspect)
            Case CanvasMode.Rectangle
                UpdateRectangle(DirectCast(_currentShape, Rectangle), currentPoint, squareAspect)
            Case CanvasMode.Ellipse
                UpdateEllipse(DirectCast(_currentShape, Ellipse), currentPoint, squareAspect)
            Case CanvasMode.Path
                UpdatePen(DirectCast(_currentShape, Polyline), currentPoint)
        End Select
    End Sub

    Public Event DrawingFinished(sender As Object, element As UIElement)

    Public Sub FinishDrawing(mode As CanvasMode, pCanvas As PolyCanvas)

        If mode = CanvasMode.Text Then
            Dim fontSize As Double = 12
            Dim fontFamily As FontFamily = New FontFamily("Arial")
            Dim textBox = CreateTextBox(_startPos, fontSize, fontFamily)
            Canvas.SetLeft(textBox, _startPos.X)
            Canvas.SetTop(textBox, _startPos.Y)
            pCanvas.Children.Add(textBox)
            AddHandler textBox.LostFocus, AddressOf OnTextBoxLostFocus
            textBox.Focus()

            Return
        End If


        If _currentShape Is Nothing Then Return

        If mode = CanvasMode.Line Then
            Dim line As Line = DirectCast(_currentShape, Line)
            _currentShape = FinaliseLine(line)
        ElseIf mode = CanvasMode.Path Then
            Dim polyline As Polyline = DirectCast(_currentShape, Polyline)
            pCanvas.Children.Remove(polyline)
            _currentShape = FinalisePolyline(polyline)
        End If

        ' Raise the DrawingFinished event
        RaiseEvent DrawingFinished(Me, _currentShape)

        ' Remove the shape from the canvas
        pCanvas.Children.Remove(_currentShape)
        _currentShape = Nothing
    End Sub

    Private Sub OnTextBoxLostFocus(sender As Object, e As RoutedEventArgs)
        Dim textBox = DirectCast(sender, TextBox)
        Dim pCanvas = DirectCast(textBox.Parent, PolyCanvas)

        If pCanvas.Children.Contains(textBox) Then
            pCanvas.Children.Remove(textBox)
            If Not String.IsNullOrEmpty(textBox.Text) Then
                RaiseEvent DrawingFinished(Me, textBox)
            End If
        End If

        ' Remove the event handler
        RemoveHandler textBox.LostFocus, AddressOf OnTextBoxLostFocus
    End Sub


    Private Function CreateLine(startPoint As Point) As Line
        Return New Line With {
            .Stroke = Brushes.Black,
            .StrokeThickness = 1,
            .X1 = startPoint.X,
            .Y1 = startPoint.Y,
            .X2 = startPoint.X,
            .Y2 = startPoint.Y,
            .StrokeStartLineCap = PenLineCap.Round,
            .StrokeEndLineCap = PenLineCap.Round,
            .StrokeDashCap = PenLineCap.Round
        }
    End Function

    Private Function CreatePen(startPoint As Point) As Polyline
        Dim polyline As New Polyline With {
            .Stroke = Brushes.Black,
            .StrokeThickness = 1,
            .StrokeStartLineCap = PenLineCap.Round,
            .StrokeEndLineCap = PenLineCap.Round,
            .StrokeDashCap = PenLineCap.Round,
            .StrokeLineJoin = PenLineJoin.Round,
            .Points = New PointCollection() From {startPoint}
        }
        Return polyline
    End Function

    Private Function CreateRectangle(startPoint As Point) As Rectangle
        Dim rect As New Rectangle With {
            .Stroke = Brushes.Black,
            .StrokeThickness = 1,
            .Width = 0,
            .Height = 0,
            .Fill = Brushes.Transparent,
            .StrokeLineJoin = PenLineJoin.Round,
            .StrokeStartLineCap = PenLineCap.Round,
            .StrokeEndLineCap = PenLineCap.Round
        }
        Canvas.SetLeft(rect, startPoint.X)
        Canvas.SetTop(rect, startPoint.Y)
        Return rect
    End Function

    Private Function CreateEllipse(startPoint As Point) As Ellipse
        Dim ellipse As New Ellipse With {
            .Stroke = Brushes.Black,
            .StrokeThickness = 1,
            .Width = 0,
            .Height = 0,
            .Fill = Brushes.Transparent,
            .StrokeStartLineCap = PenLineCap.Round,
            .StrokeEndLineCap = PenLineCap.Round
        }
        Canvas.SetLeft(ellipse, startPoint.X)
        Canvas.SetTop(ellipse, startPoint.Y)
        Return ellipse
    End Function


    Private Function CreateTextBox(p As Point, fontSize As Double, fontFamily As FontFamily) As TextBox
        Dim tb As New TextBox With {
            .Width = Double.NaN,
            .Height = Double.NaN,
            .Background = Brushes.Transparent,
            .BorderBrush = Brushes.Transparent,
            .Foreground = Brushes.Black,
            .BorderThickness = New Thickness(1),
            .Style = Nothing,
            .Text = "",
            .AcceptsReturn = False,
            .AcceptsTab = True,
            .FontSize = fontSize,
            .FontFamily = fontFamily,
            .FontWeight = FontWeights.Regular,
            .Padding = New Thickness(0)
        }

        Canvas.SetLeft(tb, p.X)
        Canvas.SetTop(tb, p.Y)

        Return tb

    End Function


    Private Sub UpdateLine(line As Line, currentPoint As Point, squareAspect As Boolean)
        If squareAspect Then
            Dim dx = currentPoint.X - _startPos.X
            Dim dy = currentPoint.Y - _startPos.Y
            Dim angle = Math.Atan2(dy, dx) * (180 / Math.PI)
            Dim snappedAngle = Math.Round(angle / 45) * 45
            Dim length = Math.Sqrt(dx * dx + dy * dy)
            Dim snappedDx = Math.Cos(snappedAngle * (Math.PI / 180)) * length
            Dim snappedDy = Math.Sin(snappedAngle * (Math.PI / 180)) * length
            line.X2 = _startPos.X + snappedDx
            line.Y2 = _startPos.Y + snappedDy
        Else
            line.X2 = currentPoint.X
            line.Y2 = currentPoint.Y
        End If
    End Sub

    Private Sub UpdatePen(polyline As Polyline, currentPoint As Point)
        If polyline.Points.Count > 0 Then
            Dim lastPoint = polyline.Points(polyline.Points.Count - 1)
            If lastPoint <> currentPoint Then
                polyline.Points.Add(currentPoint)
            End If
        Else
            polyline.Points.Add(currentPoint)
        End If
    End Sub

    Private Sub UpdateRectangle(rect As Rectangle, currentPoint As Point, squareAspect As Boolean)
        Dim x = Math.Min(currentPoint.X, _startPos.X)
        Dim y = Math.Min(currentPoint.Y, _startPos.Y)
        Dim w = Math.Abs(currentPoint.X - _startPos.X)
        Dim h = Math.Abs(currentPoint.Y - _startPos.Y)

        If squareAspect Then
            Dim size = Math.Max(w, h)
            rect.Width = size
            rect.Height = size
            Canvas.SetLeft(rect, If(currentPoint.X < _startPos.X, _startPos.X - size, _startPos.X))
            Canvas.SetTop(rect, If(currentPoint.Y < _startPos.Y, _startPos.Y - size, _startPos.Y))
        Else
            rect.Width = w
            rect.Height = h
            Canvas.SetLeft(rect, x)
            Canvas.SetTop(rect, y)
        End If
    End Sub

    Private Sub UpdateEllipse(ellipse As Ellipse, currentPoint As Point, squareAspect As Boolean)
        Dim x = Math.Min(currentPoint.X, _startPos.X)
        Dim y = Math.Min(currentPoint.Y, _startPos.Y)
        Dim w = Math.Abs(currentPoint.X - _startPos.X)
        Dim h = Math.Abs(currentPoint.Y - _startPos.Y)

        If squareAspect Then
            Dim size = Math.Max(w, h)
            ellipse.Width = size
            ellipse.Height = size
            Canvas.SetLeft(ellipse, If(currentPoint.X < _startPos.X, _startPos.X - size, _startPos.X))
            Canvas.SetTop(ellipse, If(currentPoint.Y < _startPos.Y, _startPos.Y - size, _startPos.Y))
        Else
            ellipse.Width = w
            ellipse.Height = h
            Canvas.SetLeft(ellipse, x)
            Canvas.SetTop(ellipse, y)
        End If
    End Sub

    Private Function FinaliseLine(l As Line) As Line
        Dim negativeDirection As Boolean = l.X2 < l.X1 OrElse (l.X1 = l.X2 AndAlso l.Y2 < l.Y1)

        If negativeDirection Then
            Dim tempX As Double = l.X1 * 1
            Dim tempY As Double = l.Y1 * 1

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



    Private Function FinalisePolyline(polyline As Polyline) As Path
        Dim simplifiedPoints As PointCollection = RamerDouglasPeucker(polyline.Points, epsilon:=2.0)
        polyline.Points = simplifiedPoints


        Dim minX As Double = polyline.Points.Min(Function(p) p.X)
        Dim minY As Double = polyline.Points.Min(Function(p) p.Y)

        Dim maxX As Double = polyline.Points.Max(Function(p) p.X)
        Dim maxY As Double = polyline.Points.Max(Function(p) p.Y)


        Dim offsetX As Double = minX - polyline.StrokeThickness / 2
        Dim offsetY As Double = minY - polyline.StrokeThickness / 2
        For i As Integer = 0 To polyline.Points.Count - 1
            Dim point As Point = polyline.Points(i)
            polyline.Points(i) = New Point(point.X - offsetX, point.Y - offsetY)
        Next

        Dim path As Path = ConvertPolylineToBezierPath(polyline, smoothingFactor:=0.1)
        If path Is Nothing Then Return Nothing

        Dim bounds As Rect = path.Data.Bounds
        path.Width = bounds.Width + polyline.StrokeThickness
        path.Height = bounds.Height + polyline.StrokeThickness

        Canvas.SetLeft(path, offsetX)
        Canvas.SetTop(path, offsetY)

        Return path

    End Function

    Private Function ConvertPolylineToBezierPath(polyline As Polyline, smoothingFactor As Double) As Path
        If polyline.Points.Count < 2 Then
            ' Generate a single-point path
            Dim singlePoint As Point = polyline.Points(0)

            ' Create a PathFigure with a single point
            Dim spathFigure As New PathFigure With {
                .StartPoint = singlePoint,
                .IsClosed = False
            }

            ' Create a PathGeometry and add the PathFigure
            Dim spathGeometry As New PathGeometry()
            spathGeometry.Figures.Add(spathFigure)

            ' Create the Path and set its geometry
            Dim singlePointPath As New Path With {
                .Stroke = polyline.Stroke,
                .StrokeThickness = polyline.StrokeThickness,
                .Data = spathGeometry
            }

            Return singlePointPath
        End If

        ' Generate Bézier control points
        Dim bezierSegments = GenerateBezierControlPoints(polyline.Points, smoothingFactor)

        ' Create a PathFigure to hold the segments
        Dim pathFigure As New PathFigure With {
            .StartPoint = polyline.Points(0),
            .IsClosed = False
        }

        ' Add the Bézier segments to the PathFigure
        For Each segment In bezierSegments
            pathFigure.Segments.Add(segment)
        Next

        ' Create a PathGeometry and add the PathFigure
        Dim pathGeometry As New PathGeometry()
        pathGeometry.Figures.Add(pathFigure)

        ' Create the Path and set its geometry
        Dim path As New Path With {
            .Stroke = polyline.Stroke,
            .StrokeThickness = 1,
            .Data = pathGeometry
        }

        Return path
    End Function

    Private Function GenerateBezierControlPoints(points As PointCollection, smoothingFactor As Double) As List(Of BezierSegment)
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
            Dim cp1 As Point = New Point(
                p1.X + (p2.X - p0.X) * smoothingFactor,
                p1.Y + (p2.Y - p0.Y) * smoothingFactor
            )

            Dim cp2 As Point = New Point(
                p2.X - (p3.X - p1.X) * smoothingFactor,
                p2.Y - (p3.Y - p1.Y) * smoothingFactor
            )

            ' Create a Bézier segment
            bezierSegments.Add(New BezierSegment(cp1, cp2, p2, True))
        Next

        Return bezierSegments
    End Function

    Private Function RamerDouglasPeucker(points As PointCollection, epsilon As Double) As PointCollection
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

    Private Function PerpendicularDistance(point As Point, lineStart As Point, lineEnd As Point) As Double
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