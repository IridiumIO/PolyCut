Imports CommunityToolkit.Mvvm.ComponentModel

Imports PolyCut.Shared

Imports Svg

Partial Public Class BaseDrawable : Inherits ObservableObject : Implements IDrawable

    <ImplementsProperty(GetType(IDrawable), NameOf(IDrawable.Name))>
    <ObservableProperty> Private _name As String

    <ImplementsProperty(GetType(IDrawable), NameOf(IDrawable.DrawableElement))>
    <ObservableProperty> Private _drawableElement As FrameworkElement

    Private Sub OnDrawableElementChanged(oldValue As FrameworkElement, newValue As FrameworkElement)
        If newValue IsNot Nothing Then
            Try
                If TypeOf newValue Is System.Windows.Shapes.Shape Then
                    Dim s = CType(newValue, System.Windows.Shapes.Shape)
                    If s.Stroke IsNot Nothing Then _stroke = s.Stroke
                    If s.Fill IsNot Nothing Then _fill = s.Fill
                    If s.StrokeThickness > 0 Then _strokeThickness = s.StrokeThickness
                ElseIf TypeOf newValue Is TextBox Then
                    Dim tb = CType(newValue, TextBox)
                    If tb.Foreground IsNot Nothing Then _fill = tb.Foreground
                End If
            Catch
            End Try
        End If
        ApplyVisualStyle()
    End Sub


    Public Shared Function DrawableFactory(element As FrameworkElement) As IDrawable
        If RegistrationMarkHelper.IsRegistrationMark(element) Then Return New DrawableRegistrationMark(DirectCast(element, Shape))
        Dim geometric = TryCast(element, GeometricShape)
        If geometric IsNot Nothing Then
            Select Case geometric.Kind
                Case "Rectangle" : Return New DrawableRectangle(geometric)
                Case "Ellipse" : Return New DrawableEllipse(geometric)
                Case "Line" : Return New DrawableLine(geometric)
                Case Else : Return New DrawablePath(geometric)
            End Select
        End If
        If TypeOf element Is Rectangle Then
            Return New DrawableRectangle(element)
        ElseIf TypeOf element Is Ellipse Then
            Return New DrawableEllipse(element)
        ElseIf TypeOf element Is Line Then
            Return New DrawableLine(element)
        ElseIf TypeOf element Is Path Then
            Return New DrawablePath(element)
        ElseIf TypeOf element Is TextBox Then
            Return New DrawableText(element)
        Else
            Return New BaseDrawable() With {.DrawableElement = element}
        End If
    End Function


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
            OnPropertyChanged(NameOf(IsHidden))
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
        Dim component = DrawingToSVG()
        If component Is Nothing Then Return Nothing
        Return SvgExportHelper.BakeToRoot(component, DrawableElement)
    End Function

    Public Overridable Function DrawingToSVG() As SvgVisualElement Implements IDrawable.DrawingToSVG
        Throw New NotImplementedException()
    End Function

    'SVG paint-server helpers

    Protected Function CreateSvgFillServer() As SvgColourServer
        If Me.Fill Is Nothing Then Return Nothing
        Try
            Return ColorAndBrushHelpers.BrushToSvgColourServer(Me.Fill)
        Catch
            Return Nothing
        End Try
    End Function

    Protected Function CreateSvgStrokeServer() As SvgColourServer
        If Me.StrokeThickness <= 0.001 OrElse Me.Stroke Is Nothing Then Return Nothing
        Try
            Return ColorAndBrushHelpers.BrushToSvgColourServer(Me.Stroke)
        Catch
            Return Nothing
        End Try
    End Function

    Private _stroke As System.Windows.Media.Brush = Brushes.Transparent
    Private _fill As System.Windows.Media.Brush = Brushes.Transparent
    Private _strokeThickness As Double = 0

    Public Overridable Property Stroke As System.Windows.Media.Brush Implements IDrawable.Stroke
        Get
            Return _stroke
        End Get
        Set(value As System.Windows.Media.Brush)
            _stroke = value
            ApplyVisualStyle()
            OnPropertyChanged(NameOf(Stroke))
        End Set
    End Property

    Public Overridable Property Fill As System.Windows.Media.Brush Implements IDrawable.Fill
        Get
            Return _fill
        End Get
        Set(value As System.Windows.Media.Brush)
            _fill = value
            ApplyVisualStyle()
            OnPropertyChanged(NameOf(Fill))
        End Set
    End Property

    Public Overridable Property StrokeThickness As Double Implements IDrawable.StrokeThickness
        Get
            Return _strokeThickness
        End Get
        Set(value As Double)
            _strokeThickness = value
            ApplyVisualStyle()
            OnPropertyChanged(NameOf(StrokeThickness))
        End Set
    End Property

    Overridable Sub ApplyVisualStyle()
        If _drawableElement Is Nothing Then Return
        Dim effectiveStroke = _stroke
        ' Default to black if no stroke is set
        If _strokeThickness <= 0.001 Then
            effectiveStroke = Nothing
        ElseIf _stroke Is Nothing OrElse _stroke Is Brushes.Transparent Then
            effectiveStroke = Brushes.Black
            _stroke = Brushes.Black
        End If

        Dim shape = TryCast(_drawableElement, GeometricShape)
        If shape IsNot Nothing Then
            shape.Stroke = effectiveStroke
            shape.Fill = _fill
            shape.StrokeThickness = _strokeThickness
        Else
            Dim textBox = TryCast(_drawableElement, TextBox)
            If textBox IsNot Nothing Then textBox.Foreground = If(_fill, Brushes.Transparent)
        End If
    End Sub



End Class
