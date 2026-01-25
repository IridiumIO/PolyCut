Imports System.Windows.Controls.Primitives

Imports CommunityToolkit.Mvvm.ComponentModel

Imports PolyCut.Shared

Imports Svg

Public Class BaseDrawable : Inherits ObservableObject : Implements IDrawable

    Public Property Name As String Implements IDrawable.Name
    Private _drawableElement As FrameworkElement
    Public Property DrawableElement As FrameworkElement Implements IDrawable.DrawableElement
        Get
            Return _drawableElement
        End Get
        Set(value As FrameworkElement)
            _drawableElement = value
            If _drawableElement IsNot Nothing Then
                Try
                    If TypeOf _drawableElement Is System.Windows.Shapes.Shape Then
                        Dim s = CType(_drawableElement, System.Windows.Shapes.Shape)
                        If s.Stroke IsNot Nothing Then _stroke = s.Stroke
                        If s.Fill IsNot Nothing Then _fill = s.Fill
                        If s.StrokeThickness > 0 Then _strokeThickness = s.StrokeThickness
                    ElseIf TypeOf _drawableElement Is TextBox Then
                        Dim tb = CType(_drawableElement, TextBox)
                        If tb.Foreground IsNot Nothing Then _fill = tb.Foreground
                    End If
                Catch
                End Try
            End If
            ApplyVisualStyle()
        End Set
    End Property
    Public Property Children As IEnumerable(Of IDrawable) Implements IDrawable.Children
    Public Property IsHidden As Boolean Implements IDrawable.IsHidden
        Get
            Return DrawableElement.Visibility = Visibility.Collapsed
        End Get
        Set(value As Boolean)
            If value Then
                DrawableElement.Visibility = Visibility.Collapsed
                IsSelected = False
            Else
                DrawableElement.Visibility = Visibility.Visible
            End If
        End Set
    End Property

    Private _isSelected As Boolean = False

    Public Property IsSelected As Boolean Implements IDrawable.IsSelected
        Get
            Return _isSelected
        End Get
        Set(value As Boolean)
            If _isSelected = value Then Return ' No change

            _isSelected = value
            RaiseEvent SelectionChanged(Me, EventArgs.Empty)

            OnPropertyChanged(NameOf(IsSelected))
        End Set
    End Property

    Public ReadOnly Property VisualName As String Implements IDrawable.VisualName

    Public Property ParentGroup As IDrawable Implements IDrawable.ParentGroup

    Public Event SelectionChanged(sender As Object, e As EventArgs) Implements IDrawable.SelectionChanged

    Public Function GetTransformedSVGElement() As SvgVisualElement Implements IDrawable.GetTransformedSVGElement
        Throw New NotImplementedException()
    End Function

    Public Function DrawingToSVG() As SvgVisualElement Implements IDrawable.DrawingToSVG
        Throw New NotImplementedException()
    End Function

    Private _stroke As System.Windows.Media.Brush = Brushes.Transparent
    Private _fill As System.Windows.Media.Brush = Brushes.Transparent
    Private _strokeThickness As Double = 0

    Public Property Stroke As System.Windows.Media.Brush Implements IDrawable.Stroke
        Get
            Return _stroke
        End Get
        Set(value As System.Windows.Media.Brush)
            _stroke = value
            ApplyVisualStyle()
            OnPropertyChanged(NameOf(Stroke))
        End Set
    End Property

    Public Property Fill As System.Windows.Media.Brush Implements IDrawable.Fill
        Get
            Return _fill
        End Get
        Set(value As System.Windows.Media.Brush)
            _fill = value
            ApplyVisualStyle()
            OnPropertyChanged(NameOf(Fill))
        End Set
    End Property

    Public Property StrokeThickness As Double Implements IDrawable.StrokeThickness
        Get
            Return _strokeThickness
        End Get
        Set(value As Double)
            _strokeThickness = value
            ApplyVisualStyle()
            OnPropertyChanged(NameOf(StrokeThickness))
        End Set
    End Property

    Private Sub ApplyVisualStyle()
        If _drawableElement Is Nothing Then Return

        Try
            Dim effectiveStroke As System.Windows.Media.Brush = _stroke

            ' If thickness is 0, remove stroke entirely (Nothing = no stroke). If positive and stroke is none/transparent, enable with default black
            If _strokeThickness <= 0.001 Then
                effectiveStroke = Nothing
            ElseIf _strokeThickness > 0.001 AndAlso (_stroke Is Nothing OrElse _stroke Is Brushes.Transparent) Then
                effectiveStroke = Brushes.Black
                _stroke = Brushes.Black
            End If

            If TypeOf _drawableElement Is System.Windows.Shapes.Shape Then
                Dim s = CType(_drawableElement, System.Windows.Shapes.Shape)
                s.Stroke = effectiveStroke
                s.Fill = _fill
                s.StrokeThickness = _strokeThickness
                s.InvalidateVisual()
                Return
            End If

            Dim cc = TryCast(_drawableElement, ContentControl)
            If cc IsNot Nothing Then
                Dim contentShape = TryCast(cc.Content, System.Windows.Shapes.Shape)
                If contentShape IsNot Nothing Then
                    contentShape.Stroke = effectiveStroke
                    contentShape.Fill = _fill
                    contentShape.StrokeThickness = _strokeThickness
                    contentShape.InvalidateVisual()
                Else
                    Dim tb = TryCast(cc.Content, TextBox)
                    If tb IsNot Nothing Then
                        tb.Foreground = If(TypeOf _fill Is System.Windows.Media.Brush, CType(_fill, System.Windows.Media.Brush), tb.Foreground)
                    End If
                End If
                cc.InvalidateVisual()
                Return
            End If

            Dim tbDirect = TryCast(_drawableElement, TextBox)
            If tbDirect IsNot Nothing Then
                tbDirect.Foreground = If(TypeOf _fill Is System.Windows.Media.Brush, CType(_fill, System.Windows.Media.Brush), tbDirect.Foreground)
                tbDirect.InvalidateVisual()
                Return
            End If

            Dim childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(_drawableElement)
            For i = 0 To childCount - 1
                Dim child = System.Windows.Media.VisualTreeHelper.GetChild(_drawableElement, i)
                Dim s = TryCast(child, System.Windows.Shapes.Shape)
                If s IsNot Nothing Then
                    s.Stroke = effectiveStroke
                    s.Fill = _fill
                    s.StrokeThickness = _strokeThickness
                    s.InvalidateVisual()
                    Exit For
                End If
            Next
        Catch
        End Try
    End Sub
End Class
