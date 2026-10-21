Imports PolyCut.Core

Public Module ConfigurationDefaults
    Private ReadOnly Configuration As New ProcessorConfiguration()
    Private ReadOnly Owners As New Dictionary(Of Type, Object) From {{GetType(ProcessorConfiguration), Configuration}, {GetType(DrawingConfiguration), Configuration.DrawingConfig}, {GetType(CuttingConfiguration), Configuration.CuttingConfig}, {GetType(ExportConfiguration), Configuration.ExportConfig}}

    Public Function Read(expression As BindingExpression) As Object
        Dim source = expression?.ResolvedSource
        If source Is Nothing Then Return DependencyProperty.UnsetValue
        Dim owner As Object = Nothing
        If Not Owners.TryGetValue(source.GetType(), owner) Then Return DependencyProperty.UnsetValue
        Return owner.GetType().GetProperty(expression.ResolvedSourcePropertyName).GetValue(owner)
    End Function
End Module
