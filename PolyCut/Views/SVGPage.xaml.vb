﻿Imports System.ComponentModel
Imports System.Data.SqlTypes
Imports System.Xml
Imports WPF.Ui.Controls
Imports SharpVectors
Imports System.Windows.Media.Animation
Imports System.IO
Imports CommunityToolkit.Mvvm.ComponentModel
Imports Svg
Imports System.Windows.Controls.Primitives
Class SVGPage : Implements INavigableView(Of MainViewModel)

    Public ReadOnly Property ViewModel As MainViewModel Implements INavigableView(Of MainViewModel).ViewModel



    Sub New(viewmodel As MainViewModel)

        Me.ViewModel = viewmodel
        DataContext = viewmodel

        InitializeComponent()
        zoomPanControl.Scale = 2
        zoomPanControl.TranslateTransform.X = -viewmodel.Printer.BedWidth / 2
        zoomPanControl.TranslateTransform.Y = -viewmodel.Printer.BedHeight / 2
        AddHandler viewmodel.CuttingMat.PropertyChanged, AddressOf PropertyChangedHandler
        AddHandler viewmodel.Printer.PropertyChanged, AddressOf PropertyChangedHandler
        AddHandler viewmodel.Configuration.PropertyChanged, AddressOf PropertyChangedHandler
        'AddHandler viewmodel.PropertyChanged, AddressOf MainVMPropertyChangedHandler


        Transform()
    End Sub



    Private Sub MainVMPropertyChangedHandler(sender As Object, e As PropertyChangedEventArgs)
        ''Handle SVGComponents being updated
        'If e.PropertyName = NameOf(ViewModel.SVGComponents) Then
        '    svgDrawing.Children.Clear()
        '    For Each cl In ViewModel.SVGComponents
        '        If cl.IsVisualElement Then
        '            cl.SetCanvas()
        '            svgDrawing.Children.Add(cl.ECanvas)
        '            cl.ECanvas.SubscribeToZoomBorderScaling(zoomPanControl)

        '        End If
        '    Next


        'End If
    End Sub

    Private Sub PropertyChangedHandler(sender As Object, e As PropertyChangedEventArgs)

        Dim alignmentPropertyNames = {
            NameOf(ViewModel.CuttingMat.SelectedVerticalAlignment),
            NameOf(ViewModel.CuttingMat.SelectedHorizontalAlignment),
            NameOf(ViewModel.CuttingMat.SelectedRotation),
            NameOf(ViewModel.CuttingMat)}

        If alignmentPropertyNames.Contains(e.PropertyName) Then
            Transform()
        End If
        ViewModel.GCodePaths.Clear()
        ViewModel.GCode = ""

    End Sub

    Private Sub Transform()
        Dim ret = CalculateOutputs(ViewModel.CuttingMat.SelectedRotation, ViewModel.CuttingMat.SelectedHorizontalAlignment, ViewModel.CuttingMat.SelectedVerticalAlignment)


        CuttingMat_RenderTransform.X = ret.Item1
        CuttingMat_RenderTransform.Y = ret.Item2
    End Sub

    Function CalculateOutputs(rotation As Integer, alignmentH As String, alignmentV As String) As Tuple(Of Double, Double)
        Dim x As Double = 0
        Dim y As Double = 0

        Dim CuttingMatWidth = ViewModel.CuttingMat.Width
        Dim CuttingMatHeight = ViewModel.CuttingMat.Height

        Select Case rotation
            Case 0
            ' No rotation
            Case 90
                ' 90 degrees rotation
                Select Case alignmentV
                    Case "Top"
                        If alignmentH = "Left" Then
                            x = 355.6
                        ElseIf alignmentH = "Right" Then
                            x = 330.2
                        End If
                    Case "Bottom"
                        If alignmentH = "Left" Then
                            x = 355.6
                            y = 25.4
                        ElseIf alignmentH = "Right" Then
                            x = 330.2
                            y = 25.4
                        End If
                End Select
            Case 180
                ' 180 degrees rotation
                x = 330.2
                y = 355.6
            Case 270
                ' 270 degrees rotation
                Select Case alignmentV
                    Case "Top"
                        If alignmentH = "Left" Then
                            y = 330.2
                        ElseIf alignmentH = "Right" Then
                            x = -25.4
                            y = 330.2
                        End If
                    Case "Bottom"
                        If alignmentH = "Left" Then
                            y = 355.6
                        ElseIf alignmentH = "Right" Then
                            x = -25.4
                            y = 355.6
                        End If
                End Select
        End Select

        Return Tuple.Create(x, y)
    End Function



    Dim translation As Point

    Private Sub HoverAlignment(sender As Object, e As MouseEventArgs) Handles cuttingMat_AlignmentBoxes.MouseEnter, cuttingMat_AlignmentBoxes.MouseLeave

        If e.RoutedEvent Is MouseEnterEvent Then

            Dim opacityAnimation As New DoubleAnimation(0.5, New Duration(TimeSpan.FromSeconds(0.3)))

            DupCuttingMatBounds.BeginAnimation(UIElement.OpacityProperty, opacityAnimation)



        Else
            ' Stop any existing animations and set final state when MouseLeave
            Dim op = DupCuttingMatBounds.Opacity
            DupCuttingMatBounds.BeginAnimation(UIElement.OpacityProperty, Nothing)
            DupCuttingMatBounds.Opacity = op

            ' Opacity animation for MouseLeave
            Dim opacityAnimation As New DoubleAnimation(0, TimeSpan.FromSeconds(1))
            opacityAnimation.EasingFunction = New ExponentialEase() With {.EasingMode = EasingMode.EaseIn, .Exponent = 4}
            DupCuttingMatBounds.BeginAnimation(UIElement.OpacityProperty, opacityAnimation)
        End If

    End Sub



    Private Sub svgElementContextMenu(sender As Object, e As MouseButtonEventArgs) Handles mainCanvas.MouseRightButtonUp

        'If TypeOf (e.Source) Is resizableSVGCanvas AndAlso e.Source.Parent Is svgDrawing Then
        '    e.Handled = True

        'End If
    End Sub

    Private Sub Page_Drop(sender As Object, e As DragEventArgs)
        If e.Data.GetDataPresent(DataFormats.FileDrop) Then

            Dim files() As String = TryCast(e.Data.GetData(DataFormats.FileDrop), String())

            ViewModel.DragSVGs(files)

        End If
    End Sub



    Private StartPos As Point
    Private _drawingLine As Line
    Private _drawingRect As Rectangle
    Private _drawingEllipse As Ellipse
    Private _drawingTextbox As TextBox

    Private Sub MainView_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles zoomPanControl.MouseDown

        If TypeOf (e.OriginalSource.Parent) IsNot resizableSVGCanvas Then
            Debug.WriteLine("MouseDown")
            resizableSVGCanvas.DeSelectAll()
            zoomPanControl.MoveFocus(New TraversalRequest(FocusNavigationDirection.Previous))
        End If

    End Sub

    Private Sub DrawingCanvas_MouseDown(sender As Object, e As MouseButtonEventArgs) Handles zoomPanControl.MouseDown
        StartPos = e.GetPosition(mainCanvas)
        If _drawingTextbox IsNot Nothing Then
            ViewModel.CanvasToolMode = CanvasMode.Selection
            _drawingTextbox.MoveFocus(New TraversalRequest(FocusNavigationDirection.Next))
        End If
        Debug.WriteLine(ViewModel.DrawableCollection.Count)
        If ViewModel.CanvasToolMode <> CanvasMode.Selection Then
            For Each child In ViewModel.DrawableCollection
                If TypeOf child.Parent Is ContentControl Then

                    Selector.SetIsSelected(child.Parent, False)
                End If
            Next
        End If

        Select Case ViewModel.CanvasToolMode
            Case CanvasMode.Line
                _drawingLine = CreateLine(e.GetPosition(mainCanvas))
                mainCanvas.Children.Add(_drawingLine)
            Case CanvasMode.Rectangle
                _drawingRect = CreateRectangle(e.GetPosition(mainCanvas))
                mainCanvas.Children.Add(_drawingRect)

            Case CanvasMode.Ellipse
                _drawingEllipse = CreateEllipse(e.GetPosition(mainCanvas))
                mainCanvas.Children.Add(_drawingEllipse)


        End Select


    End Sub

    Private Function CreateLine(p As Point) As Line
        Return New Line With {
            .Stroke = Brushes.Black,
            .StrokeThickness = 1,
            .X1 = StartPos.X,
            .Y1 = StartPos.Y,
            .X2 = StartPos.X,
            .Y2 = StartPos.Y,
            .StrokeStartLineCap = PenLineCap.Round,
            .StrokeEndLineCap = PenLineCap.Round,
            .StrokeDashCap = PenLineCap.Round
        }

    End Function

    Private Function CreateRectangle(p As Point) As Rectangle

        Dim rct As New Rectangle With {
            .Stroke = Brushes.Black,
            .StrokeThickness = 1,
            .Width = 0,
            .Height = 0,
            .Fill = Brushes.Transparent,
            .StrokeLineJoin = PenLineJoin.Round,
            .StrokeStartLineCap = PenLineCap.Round,
            .StrokeEndLineCap = PenLineCap.Round
        }

        Canvas.SetLeft(rct, p.X)
        Canvas.SetTop(rct, p.Y)

        Return rct

    End Function

    Private Function CreateEllipse(p As Point) As Ellipse
        Dim elp As New Ellipse With {
            .Stroke = Brushes.Black,
            .StrokeThickness = 1,
            .Width = 0,
            .Height = 0,
            .Fill = Brushes.Transparent,
            .StrokeStartLineCap = PenLineCap.Round,
            .StrokeEndLineCap = PenLineCap.Round
        }

        Canvas.SetLeft(elp, p.X)
        Canvas.SetTop(elp, p.Y)

        Return elp
    End Function


    Private Sub drawingCanvas_MouseMove(sender As Object, e As MouseEventArgs) Handles zoomPanControl.MouseMove
        If Not e.LeftButton = MouseButtonState.Pressed Then Return

        Dim squareAspect = Keyboard.IsKeyDown(Key.LeftShift)
        Dim p = e.GetPosition(mainCanvas)

        Select Case ViewModel.CanvasToolMode

            Case CanvasMode.Line
                If squareAspect Then
                    Dim dx = (p.X - StartPos.X)
                    Dim dy = (p.Y - StartPos.Y)
                    Dim angle = Math.Atan2(dy, dx) * (180 / Math.PI)
                    Dim snappedAngle = Math.Round(angle / 45) * 45
                    Dim length = Math.Sqrt(dx * dx + dy * dy)
                    Dim snappedDx = Math.Cos(snappedAngle * (Math.PI / 180)) * length
                    Dim snappedDy = Math.Sin(snappedAngle * (Math.PI / 180)) * length
                    _drawingLine.X2 = StartPos.X + snappedDx
                    _drawingLine.Y2 = StartPos.Y + snappedDy
                Else
                    _drawingLine.X2 = p.X
                    _drawingLine.Y2 = p.Y
                End If

            Case CanvasMode.Rectangle
                Dim x = Math.Min(p.X, StartPos.X)
                Dim y = Math.Min(p.Y, StartPos.Y)
                Dim w = Math.Abs(p.X - StartPos.X)
                Dim h = Math.Abs(p.Y - StartPos.Y)

                If squareAspect Then
                    Dim size = Math.Max(w, h)
                    _drawingRect.Width = size
                    _drawingRect.Height = size
                    Canvas.SetLeft(_drawingRect, If(p.X < StartPos.X, StartPos.X - size, StartPos.X))
                    Canvas.SetTop(_drawingRect, If(p.Y < StartPos.Y, StartPos.Y - size, StartPos.Y))
                Else
                    _drawingRect.Width = w
                    _drawingRect.Height = h
                    Canvas.SetLeft(_drawingRect, x)
                    Canvas.SetTop(_drawingRect, y)
                End If

            Case CanvasMode.Ellipse
                Dim x = Math.Min(p.X, StartPos.X)
                Dim y = Math.Min(p.Y, StartPos.Y)
                Dim w = Math.Abs(p.X - StartPos.X)
                Dim h = Math.Abs(p.Y - StartPos.Y)

                If squareAspect Then
                    Dim size = Math.Max(w, h)
                    _drawingEllipse.Width = size
                    _drawingEllipse.Height = size
                    Canvas.SetLeft(_drawingEllipse, If(p.X < StartPos.X, StartPos.X - size, StartPos.X))
                    Canvas.SetTop(_drawingEllipse, If(p.Y < StartPos.Y, StartPos.Y - size, StartPos.Y))
                Else
                    _drawingEllipse.Width = w
                    _drawingEllipse.Height = h
                    Canvas.SetLeft(_drawingEllipse, x)
                    Canvas.SetTop(_drawingEllipse, y)
                End If

        End Select

    End Sub


    Private Function CreateTextBox(p As Point) As TextBox
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
            .FontSize = ViewModel.CanvasFontSize,
            .FontFamily = ViewModel.CanvasFontFamily,
            .FontWeight = FontWeights.Regular,
            .Padding = New Thickness(0)
        }

        Canvas.SetLeft(tb, p.X)
        Canvas.SetTop(tb, p.Y)

        Return tb

    End Function

    Private Sub tb_LostFocus()

        If mainCanvas.Children.Contains(_drawingTextbox) Then
            mainCanvas.Children.Remove(_drawingTextbox)
            If Not _drawingTextbox.Text = "" Then GenerateSVGFromText(_drawingTextbox)
            _drawingTextbox = Nothing
        End If

    End Sub

    Private Sub tb_GotFocus(sender As Object, e As RoutedEventArgs)
        Debug.WriteLine(sender.GetHashCode)
    End Sub


    Private Sub DrawingCanvas_MouseUp(sender As Object, e As MouseButtonEventArgs) Handles zoomPanControl.MouseUp

        Select Case ViewModel.CanvasToolMode
            Case CanvasMode.Line
                mainCanvas.Children.Remove(_drawingLine)
                If _drawingLine.X1 = _drawingLine.X2 AndAlso _drawingLine.Y1 = _drawingLine.Y2 Then Return
                GenerateSVGFromLine(_drawingLine)

            Case CanvasMode.Rectangle
                mainCanvas.Children.Remove(_drawingRect)
                If _drawingRect.Width < 1 AndAlso _drawingRect.Height < 1 Then Return
                GenerateSVGFromRect(_drawingRect)

            Case CanvasMode.Ellipse
                mainCanvas.Children.Remove(_drawingEllipse)
                If _drawingEllipse.Width < 1 AndAlso _drawingEllipse.Height < 1 Then Return
                GenerateSVGFromEllipse(_drawingEllipse)

            Case CanvasMode.Text
                If TypeOf (e.OriginalSource) Is TextBlock Then Return
                _drawingTextbox = CreateTextBox(e.GetPosition(mainCanvas))
                mainCanvas.Children.Add(_drawingTextbox)
                _drawingTextbox.Focus()
                AddHandler _drawingTextbox.LostFocus, AddressOf tb_LostFocus
                AddHandler _drawingTextbox.GotFocus, AddressOf tb_GotFocus

        End Select


    End Sub



    Private Sub GenerateSVGFromLine(l As Line)

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

        ViewModel.AddDrawableElement(l)
    End Sub


    Private Sub GenerateSVGFromRect(r As Rectangle)
        ViewModel.AddDrawableElement(r)
    End Sub

    Private Sub GenerateSVGFromEllipse(e As Ellipse)
        ViewModel.AddDrawableElement(e)
    End Sub

    Private Sub GenerateSVGFromText(t As TextBox)
        ViewModel.AddDrawableElement(t)
    End Sub

    Private Sub SVGPageView_Unloaded(sender As Object, e As RoutedEventArgs)
        For Each child In mainCanvas.Children
            Selector.SetIsSelected(child, False)
        Next
    End Sub
End Class
