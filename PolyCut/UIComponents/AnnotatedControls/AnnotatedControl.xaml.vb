Imports System.Globalization
Imports System.Windows.Markup

<ContentProperty(NameOf(AnnotatedControl.Children))>
Public Class AnnotatedControl
    Public Sub New()
        InitializeComponent()
        ToolTip = New ToolTip()
        ToolTipService.SetShowOnDisabled(Me, True)
        ToolTipService.SetInitialShowDelay(Me, 300)
        ToolTipService.SetBetweenShowDelay(Me, 0)
        AddHandler Loaded, Sub() RefreshChildren()
    End Sub

    Public ReadOnly Property Children As UIElementCollection
        Get
            Return ChildPanel.Children
        End Get
    End Property

    Protected Overridable ReadOnly Property BoundValueProperty As DependencyProperty
        Get
            Return Nothing
        End Get
    End Property

    Protected Overridable ReadOnly Property ShowChildren As Boolean
        Get
            Return True
        End Get
    End Property

    Protected Sub SetEditor(editor As FrameworkElement, Optional belowLabel As Boolean = False)
        EditorHost.Content = editor
        If Not belowLabel Then Return
        Grid.SetRow(EditorHost, 1)
        Grid.SetColumn(EditorHost, 0)
        Grid.SetColumnSpan(EditorHost, 2)
        LabelBlock.Margin = New Thickness(1, 10, 0, 0)
    End Sub

    Protected Sub RefreshChildren()
        ChildHost.Visibility = If(Children.Count > 0 AndAlso ShowChildren, Visibility.Visible, Visibility.Collapsed)
    End Sub

    Private Function ValueBinding() As BindingExpression
        Return If(BoundValueProperty Is Nothing, Nothing, GetBindingExpression(BoundValueProperty))
    End Function

    Protected Overrides Sub OnToolTipOpening(e As ToolTipEventArgs)
        Dim content As New StackPanel With {.MaxWidth = 320}
        content.SetBinding(TextElement.FontSizeProperty, New Binding(NameOf(FontSize)) With {.Source = ToolTip})
        AddParagraph(content, ToolTipMain)
        AddParagraph(content, ToolTipSub, 0.75)
        If ToolTipTemplate IsNot Nothing Then content.Children.Add(New ContentPresenter With {.Content = DataContext, .ContentTemplate = ToolTipTemplate})
        If Not String.IsNullOrWhiteSpace(ToolTipRequirements) Then AddParagraph(content, "Requires: {0}".LTF(ToolTipRequirements))
        Dim value = ConfigurationDefaults.Read(ValueBinding())
        If value IsNot DependencyProperty.UnsetValue Then AddParagraph(content, "Default: {0}".LTF(FormatDefault(value)))
        DirectCast(ToolTip, ToolTip).Content = content

        e.Handled = content.Children.Count = 0
        MyBase.OnToolTipOpening(e)
    End Sub

    Private Shared Sub AddParagraph(panel As Panel, text As String, Optional opacity As Double = 1)
        If String.IsNullOrWhiteSpace(text) Then Return
        panel.Children.Add(New TextBlock With {.Text = text, .TextAlignment = TextAlignment.Left, .TextWrapping = TextWrapping.Wrap, .Opacity = opacity, .Margin = New Thickness(0, If(panel.Children.Count = 0, 0, 8), 0, 0)})
    End Sub

    Private Function FormatDefault(value As Object) As String
        If TypeOf value Is Boolean Then Return If(value, "Enabled".LT(), "Disabled".LT())
        If value Is Nothing OrElse String.IsNullOrEmpty(value.ToString()) Then Return ""
        If value.GetType().IsEnum Then Return CStr(New EnumToDescriptionConverter().Convert(value, GetType(String), Nothing, CultureInfo.CurrentCulture)).LT()
        Dim text = If(TypeOf value Is IFormattable, DirectCast(value, IFormattable).ToString("0.####", CultureInfo.CurrentCulture), value.ToString())
        Return If(String.IsNullOrEmpty(UnitText), text, text & " " & UnitText)
    End Function

    Public Shared ReadOnly LabelTextProperty As DependencyProperty = DependencyProperty.Register(NameOf(LabelText), GetType(String), GetType(AnnotatedControl), New PropertyMetadata(String.Empty))
    Public Property LabelText As String
        Get
            Return DirectCast(GetValue(LabelTextProperty), String)
        End Get
        Set(value As String)
            SetValue(LabelTextProperty, value)
        End Set
    End Property

    Public Shared ReadOnly UnitTextProperty As DependencyProperty = DependencyProperty.Register(NameOf(UnitText), GetType(String), GetType(AnnotatedControl), New PropertyMetadata(String.Empty))
    Public Property UnitText As String
        Get
            Return DirectCast(GetValue(UnitTextProperty), String)
        End Get
        Set(value As String)
            SetValue(UnitTextProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ToolTipMainProperty As DependencyProperty = DependencyProperty.Register(NameOf(ToolTipMain), GetType(String), GetType(AnnotatedControl), New PropertyMetadata(String.Empty))
    Public Property ToolTipMain As String
        Get
            Return DirectCast(GetValue(ToolTipMainProperty), String)
        End Get
        Set(value As String)
            SetValue(ToolTipMainProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ToolTipSubProperty As DependencyProperty = DependencyProperty.Register(NameOf(ToolTipSub), GetType(String), GetType(AnnotatedControl), New PropertyMetadata(String.Empty))
    Public Property ToolTipSub As String
        Get
            Return DirectCast(GetValue(ToolTipSubProperty), String)
        End Get
        Set(value As String)
            SetValue(ToolTipSubProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ToolTipRequirementsProperty As DependencyProperty = DependencyProperty.Register(NameOf(ToolTipRequirements), GetType(String), GetType(AnnotatedControl), New PropertyMetadata(String.Empty))
    Public Property ToolTipRequirements As String
        Get
            Return DirectCast(GetValue(ToolTipRequirementsProperty), String)
        End Get
        Set(value As String)
            SetValue(ToolTipRequirementsProperty, value)
        End Set
    End Property

    Public Shared ReadOnly EditorWidthProperty As DependencyProperty = DependencyProperty.Register(NameOf(EditorWidth), GetType(Double), GetType(AnnotatedControl), New PropertyMetadata(120.0))
    Public Property EditorWidth As Double
        Get
            Return DirectCast(GetValue(EditorWidthProperty), Double)
        End Get
        Set(value As Double)
            SetValue(EditorWidthProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ToolTipTemplateProperty As DependencyProperty = DependencyProperty.Register(NameOf(ToolTipTemplate), GetType(DataTemplate), GetType(AnnotatedControl), New PropertyMetadata(Nothing))
    Public Property ToolTipTemplate As DataTemplate
        Get
            Return DirectCast(GetValue(ToolTipTemplateProperty), DataTemplate)
        End Get
        Set(value As DataTemplate)
            SetValue(ToolTipTemplateProperty, value)
        End Set
    End Property

End Class
