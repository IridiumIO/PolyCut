Imports System.Diagnostics
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Data
Imports System.Windows.Documents


Partial Public NotInheritable Class L

    ' ====================
    ' Attached property: L.Value
    ' ====================

    Public Shared ReadOnly ValueProperty As DependencyProperty = DependencyProperty.RegisterAttached("Value", GetType(String), GetType(L), New PropertyMetadata(Nothing, AddressOf OnValueChanged))

    Public Shared Sub SetValue(target As DependencyObject, value As String)
        target.SetValue(ValueProperty, value)
    End Sub

    Public Shared Function GetValue(target As DependencyObject) As String
        Return DirectCast(target.GetValue(ValueProperty), String)
    End Function

    Private Shared Sub OnValueChanged(target As DependencyObject, e As DependencyPropertyChangedEventArgs)
        ApplyValue(target)
    End Sub

    ' ====================
    ' Attached property: L.Context
    ' ====================

    Public Shared ReadOnly ContextProperty As DependencyProperty = DependencyProperty.RegisterAttached("Context", GetType(String), GetType(L), New FrameworkPropertyMetadata(Nothing, FrameworkPropertyMetadataOptions.Inherits, AddressOf OnContextChanged))

    Public Shared Sub SetContext(target As DependencyObject, value As String)
        target.SetValue(ContextProperty, value)
    End Sub

    Public Shared Function GetContext(target As DependencyObject) As String
        Return DirectCast(target.GetValue(ContextProperty), String)
    End Function

    Private Shared Sub OnContextChanged(target As DependencyObject, e As DependencyPropertyChangedEventArgs)
        If GetValue(target) Is Nothing Then Return ' Context may be assigned after Value in XAML.
        ApplyValue(target)
    End Sub

    ' ====================
    ' Applied-target tracking
    ' ====================
    ' These private attached properties let L remember which target property it owns.
    ' This is important because Context may be applied after Value and cause localisation to run twice.

    Private Shared ReadOnly AppliedTargetProperty As DependencyProperty = DependencyProperty.RegisterAttached("AppliedTarget", GetType(DependencyProperty), GetType(L))

    Private Shared Sub ApplyValue(target As DependencyObject)

        Dim source = GetValue(target)
        Dim targetProperty = ResolveTargetProperty(target)

        If targetProperty Is Nothing Then
            LocalisationWarning($"Cannot determine localisation target for {target.GetType().FullName}.")
            Return
        End If

        Dim ownedProperty = TryCast(target.GetValue(AppliedTargetProperty), DependencyProperty)

        If source Is Nothing Then
            If ownedProperty Is targetProperty Then target.ClearValue(targetProperty)
            target.ClearValue(AppliedTargetProperty)
            Return
        End If

        If ownedProperty Is Nothing Then

            Dim existing = target.ReadLocalValue(targetProperty)

            If existing IsNot DependencyProperty.UnsetValue Then
                LocalisationWarning($"{target.GetType().Name}.{targetProperty.Name} already has a local value or binding. L.Value was ignored.")
                Return
            End If

            target.SetValue(AppliedTargetProperty, targetProperty)

        ElseIf ownedProperty IsNot targetProperty Then

            LocalisationWarning($"{target.GetType().Name} localisation target unexpectedly changed.")
            Return

        Else

            Dim existingBinding = BindingOperations.GetBinding(target, targetProperty)

            If existingBinding Is Nothing OrElse existingBinding.Source IsNot LocalisationState.Instance OrElse Not TypeOf existingBinding.Converter Is TranslationConverter Then
                LocalisationWarning($"{target.GetType().Name}.{targetProperty.Name} was modified after localisation. L.Value will not overwrite it.")
                Return
            End If

        End If

        Dim binding As New System.Windows.Data.Binding(NameOf(LocalisationState.Version)) With {
        .Source = LocalisationState.Instance,
        .Mode = BindingMode.OneWay,
        .Converter = New TranslationConverter(source, GetContext(target))
    }

        BindingOperations.SetBinding(target, targetProperty, binding)

    End Sub

    ' ====================
    ' Target-property resolution
    ' ====================

    Private Shared ReadOnly TargetCache As New Dictionary(Of Type, DependencyProperty)
    Private Shared ReadOnly TargetCacheLock As New Object()

    Private Shared Function ResolveTargetProperty(target As DependencyObject) As DependencyProperty

        Dim type = target.GetType()

        SyncLock TargetCacheLock

            Dim cached As DependencyProperty = Nothing

            If TargetCache.TryGetValue(type, cached) Then Return cached

            cached = ResolveTargetPropertyUncached(target)

            ' Dictionary allows Nothing, so unsupported types are cached too.
            TargetCache(type) = cached

            Return cached

        End SyncLock

    End Function

    Private Shared Function ResolveTargetPropertyUncached(target As DependencyObject) As DependencyProperty

        ' Order is important: test more specific types before their base classes.
        If TypeOf target Is Window Then Return Window.TitleProperty
        If TypeOf target Is TextBlock Then Return TextBlock.TextProperty
        If TypeOf target Is AccessText Then Return AccessText.TextProperty
        If TypeOf target Is Run Then Return Run.TextProperty
        If TypeOf target Is DataGridColumn Then Return DataGridColumn.HeaderProperty
        If TypeOf target Is GridViewColumn Then Return GridViewColumn.HeaderProperty
        If TypeOf target Is HeaderedContentControl Then Return HeaderedContentControl.HeaderProperty
        If TypeOf target Is HeaderedItemsControl Then Return HeaderedItemsControl.HeaderProperty
        If TypeOf target Is ContentControl Then Return ContentControl.ContentProperty

        Return Nothing

    End Function

End Class
