Public Class AnnotatedSlider
    Inherits AnnotatedNumberBox

    Private ReadOnly _slider As New Slider With {.IsSnapToTickEnabled = True, .VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(0, 0, 10, 0)}

    Public Sub New()
        EditorWidth = 100
        _slider.SetBinding(Slider.MinimumProperty, New Binding(NameOf(Minimum)) With {.Source = Me})
        _slider.SetBinding(Slider.MaximumProperty, New Binding(NameOf(Maximum)) With {.Source = Me})
        _slider.SetBinding(Slider.TickFrequencyProperty, New Binding(NameOf(TickFrequency)) With {.Source = Me})
        _slider.SetBinding(Slider.ValueProperty, New Binding(NameOf(Value)) With {.Source = Me, .Mode = BindingMode.TwoWay, .UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged})
        Dim number = DirectCast(EditorHost.Content, FrameworkElement)
        EditorHost.Content = Nothing
        Dim editor As New Grid()
        editor.ColumnDefinitions.Add(New ColumnDefinition())
        editor.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Grid.SetColumn(number, 1)
        editor.Children.Add(_slider)
        editor.Children.Add(number)
        SetEditor(editor, True)
    End Sub

    Public Shared ReadOnly MinimumProperty As DependencyProperty = DependencyProperty.Register(NameOf(Minimum), GetType(Double), GetType(AnnotatedSlider), New PropertyMetadata(0.0))
    Public Property Minimum As Double
        Get
            Return DirectCast(GetValue(MinimumProperty), Double)
        End Get
        Set(value As Double)
            SetValue(MinimumProperty, value)
        End Set
    End Property

    Public Shared ReadOnly MaximumProperty As DependencyProperty = DependencyProperty.Register(NameOf(Maximum), GetType(Double), GetType(AnnotatedSlider), New PropertyMetadata(100.0))
    Public Property Maximum As Double
        Get
            Return DirectCast(GetValue(MaximumProperty), Double)
        End Get
        Set(value As Double)
            SetValue(MaximumProperty, value)
        End Set
    End Property

    Public Shared ReadOnly TickFrequencyProperty As DependencyProperty = DependencyProperty.Register(NameOf(TickFrequency), GetType(Double), GetType(AnnotatedSlider), New PropertyMetadata(1.0))
    Public Property TickFrequency As Double
        Get
            Return DirectCast(GetValue(TickFrequencyProperty), Double)
        End Get
        Set(value As Double)
            SetValue(TickFrequencyProperty, value)
        End Set
    End Property

End Class
