Imports System.Collections
Imports System.Windows.Controls.Primitives

Public Class AnnotatedComboBox
    Inherits AnnotatedControl

    Public Sub New()
        Dim editor As New ComboBox With {.MinWidth = 120}
        editor.SetBinding(ItemsControl.ItemsSourceProperty, New Binding(NameOf(ItemsSource)) With {.Source = Me})
        editor.SetBinding(Selector.SelectedItemProperty, New Binding(NameOf(SelectedItem)) With {.Source = Me, .Mode = BindingMode.TwoWay})
        SetEditor(editor)
    End Sub

    Protected Overrides ReadOnly Property BoundValueProperty As DependencyProperty
        Get
            Return SelectedItemProperty
        End Get
    End Property

    Public Shared ReadOnly ItemsSourceProperty As DependencyProperty = DependencyProperty.Register(NameOf(ItemsSource), GetType(IEnumerable), GetType(AnnotatedComboBox), New PropertyMetadata(Nothing))
    Public Property ItemsSource As IEnumerable
        Get
            Return DirectCast(GetValue(ItemsSourceProperty), IEnumerable)
        End Get
        Set(value As IEnumerable)
            SetValue(ItemsSourceProperty, value)
        End Set
    End Property

    Public Shared ReadOnly SelectedItemProperty As DependencyProperty = DependencyProperty.Register(NameOf(SelectedItem), GetType(Object), GetType(AnnotatedComboBox), New FrameworkPropertyMetadata(Nothing, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault))
    Public Property SelectedItem As Object
        Get
            Return DirectCast(GetValue(SelectedItemProperty), Object)
        End Get
        Set(value As Object)
            SetValue(SelectedItemProperty, value)
        End Set
    End Property

End Class
