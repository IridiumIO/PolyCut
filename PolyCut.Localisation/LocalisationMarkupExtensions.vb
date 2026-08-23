Imports System.Globalization
Imports System.Windows
Imports System.Windows.Data
Imports System.Windows.Markup

''' <summary>
''' Localised literal for XAML:
'''
'''     Text="{loc:T Hello world}"
'''     Text="{loc:T Hello world, Context=Dialog}"
'''
''' The result is a one-way binding to <see cref="LocalisationState.Version"/> so the text re-translates when the language changes.
''' </summary>
<MarkupExtensionReturnType(GetType(String))>
Public Class T
    Inherits MarkupExtension

    Public Sub New()
    End Sub

    Public Sub New(source As String)
        Me.Source = source
    End Sub

    <ConstructorArgument("source")>
    Public Property Source As String

    Public Property Context As String

    Public Overrides Function ProvideValue(serviceProvider As IServiceProvider) As Object
        Dim binding As New System.Windows.Data.Binding(NameOf(LocalisationState.Version)) With {
            .Source = LocalisationState.Instance,
            .Mode = BindingMode.OneWay,
            .Converter = New TranslationConverter(Source, Context)
        }

        Return binding.ProvideValue(serviceProvider)
    End Function
End Class

''' <summary>
''' Standard WPF Binding with a localised format string.
'''
'''     Text="{loc:Binding Count, Format={}{0} objects selected}"
'''
''' Everything else behaves as a normal WPF Binding.
''' </summary>
Public Class Binding
    Inherits MarkupExtension

    Public Sub New()
    End Sub

    Public Sub New(path As String)
        Me.Path = path
    End Sub

    <ConstructorArgument("path")>
    Public Property Path As String

    Public Property Format As String
    Public Property Context As String

    Public Property Source As Object
    Public Property RelativeSource As RelativeSource
    Public Property ElementName As String

    Public Property Converter As IValueConverter
    Public Property ConverterParameter As Object
    Public Property ConverterCulture As CultureInfo

    Public Property Mode As BindingMode = BindingMode.Default
    Public Property UpdateSourceTrigger As UpdateSourceTrigger = UpdateSourceTrigger.Default

    Public Property FallbackValue As Object = DependencyProperty.UnsetValue
    Public Property TargetNullValue As Object = DependencyProperty.UnsetValue

    Public Overrides Function ProvideValue(serviceProvider As IServiceProvider) As Object

        Dim valueBinding As System.Windows.Data.Binding

        If String.IsNullOrEmpty(Path) Then
            valueBinding = New System.Windows.Data.Binding()
        Else
            valueBinding = New System.Windows.Data.Binding(Path)
        End If

        If Source IsNot Nothing Then valueBinding.Source = Source
        If RelativeSource IsNot Nothing Then valueBinding.RelativeSource = RelativeSource
        If Not String.IsNullOrEmpty(ElementName) Then valueBinding.ElementName = ElementName

        valueBinding.Mode = Mode
        valueBinding.UpdateSourceTrigger = UpdateSourceTrigger
        valueBinding.Converter = Converter
        valueBinding.ConverterParameter = ConverterParameter

        If ConverterCulture IsNot Nothing Then valueBinding.ConverterCulture = ConverterCulture
        If FallbackValue IsNot DependencyProperty.UnsetValue Then valueBinding.FallbackValue = FallbackValue
        If TargetNullValue IsNot DependencyProperty.UnsetValue Then valueBinding.TargetNullValue = TargetNullValue

        If String.IsNullOrEmpty(Format) Then Return valueBinding.ProvideValue(serviceProvider)

        Dim versionBinding As New System.Windows.Data.Binding(NameOf(LocalisationState.Version)) With {
            .Source = LocalisationState.Instance,
            .Mode = BindingMode.OneWay
        }

        Dim multiBinding As New MultiBinding With {
            .Mode = BindingMode.OneWay,
            .Converter = New LocalisedFormatConverter(Format, Context)
        }

        multiBinding.Bindings.Add(valueBinding)
        multiBinding.Bindings.Add(versionBinding)

        Return multiBinding.ProvideValue(serviceProvider)

    End Function

End Class
