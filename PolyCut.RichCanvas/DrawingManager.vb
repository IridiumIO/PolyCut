
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
                Dim line As Line = DirectCast(_currentShape, Line)
                Debug.WriteLine($"Line: X1={line.X1}, Y1={line.Y1}, X2={line.X2}, Y2={line.Y2}")
            Case CanvasMode.Rectangle
                UpdateRectangle(DirectCast(_currentShape, Rectangle), currentPoint, squareAspect)
                Dim rect As Rectangle = DirectCast(_currentShape, Rectangle)
                Debug.WriteLine($"Rectangle: X={Canvas.GetLeft(rect)}, Y={Canvas.GetTop(rect)}, Width={rect.Width}, Height={rect.Height}")
            Case CanvasMode.Ellipse
                UpdateEllipse(DirectCast(_currentShape, Ellipse), currentPoint, squareAspect)
                Dim ellipse As Ellipse = DirectCast(_currentShape, Ellipse)
                Debug.WriteLine($"Ellipse: X={Canvas.GetLeft(ellipse)}, Y={Canvas.GetTop(ellipse)}, Width={ellipse.Width}, Height={ellipse.Height}")
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
            line = FinaliseLine(line)
            _currentShape = line
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


End Class