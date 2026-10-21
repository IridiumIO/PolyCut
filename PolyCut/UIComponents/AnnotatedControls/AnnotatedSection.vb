Imports System.Windows.Markup

<ContentProperty(NameOf(AnnotatedSection.Children))>
Public Class AnnotatedSection
    Inherits Wpf.Ui.Controls.CardExpander

    Private ReadOnly _panel As New StackPanel With {.Margin = New Thickness(15, 0, 15, 10), .FlowDirection = FlowDirection.LeftToRight}

    Public Sub New()
        Margin = New Thickness(-15, 0, -15, 0)
        Background = Brushes.Transparent
        BorderBrush = Brushes.Transparent
        ContentPadding = New Thickness(0)
        FlowDirection = FlowDirection.RightToLeft
        IsExpanded = True
        SetResourceReference(StyleProperty, "CustomCardExpanderStyle")
        Dim label As New Label With {.Margin = New Thickness(10, 0, 10, 0), .FlowDirection = FlowDirection.LeftToRight}
        label.SetBinding(ContentControl.ContentProperty, New Binding(NameOf(LabelText)) With {.Source = Me})
        Header = label
        Content = _panel
    End Sub

    Public ReadOnly Property Children As UIElementCollection
        Get
            Return _panel.Children
        End Get
    End Property

    Public Shared ReadOnly LabelTextProperty As DependencyProperty = DependencyProperty.Register(NameOf(LabelText), GetType(String), GetType(AnnotatedSection), New PropertyMetadata(String.Empty))
    Public Property LabelText As String
        Get
            Return DirectCast(GetValue(LabelTextProperty), String)
        End Get
        Set(value As String)
            SetValue(LabelTextProperty, value)
        End Set
    End Property

End Class
