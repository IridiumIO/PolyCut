Imports System.Windows.Controls.Primitives

Public Class DesignerItemDecorator : Inherits Control

    Private adorner As ResizeRotateAdorner
    Private Shared _currentSelected As ContentControl
    Private Shared ParentScale As Double = 1
    Public Shared ReadOnly ShowDecoratorProperty As DependencyProperty = DependencyProperty.Register("ShowDecorator", GetType(Boolean), GetType(DesignerItemDecorator), New FrameworkPropertyMetadata(False, AddressOf ShowDecoratorProperty_Changed))
    Public Property ShowDecorator As Boolean
        Get
            Return CBool(GetValue(ShowDecoratorProperty))
        End Get
        Set(value As Boolean)
            SetValue(ShowDecoratorProperty, value)
        End Set
    End Property

    Public Sub New()
        AddHandler Me.Unloaded, AddressOf DesignerItemDecorator_Unloaded
        AddHandler Me.DataContextChanged, AddressOf DesignerItemDecorator_DataContextChanged
        AddHandler Me.MouseLeftButtonDown, AddressOf DesignerItemDecorator_MouseDown
        EventAggregator.Subscribe(AddressOf OnScaleChanged)
        EventAggregator.Subscribe(AddressOf OnTranslationChanged)
    End Sub


    Private Sub OnTranslationChanged(message As Object)
        If TypeOf message IsNot TranslationChangedMessage Then Return
        InvalidateArrange()
    End Sub

    Private Sub OnScaleChanged(message As Object)
        If TypeOf message IsNot ScaleChangedMessage Then Return
        ParentScale = (DirectCast(message, ScaleChangedMessage).NewScale)
        If adorner IsNot Nothing Then adorner.chrome.OnScaleChanged(message)
    End Sub

    Private Sub HideAdorner()
        If adorner Is Nothing Then Return
        adorner.Visibility = Visibility.Hidden
    End Sub

    Private Sub DesignerItemDecorator_DataContextChanged()
        If DataContext Is Nothing Then Return
        InitialiseAdorner()

    End Sub

    Private Sub InitialiseAdorner()
        If adorner IsNot Nothing Then : adorner.Visibility = Visibility.Visible : Return : End If

        Dim adornerLayer As AdornerLayer = AdornerLayer.GetAdornerLayer(Me)
        Dim designerItem As ContentControl = TryCast(Me.DataContext, ContentControl)

        If adornerLayer Is Nothing OrElse designerItem Is Nothing Then Return

        adorner = New ResizeRotateAdorner(designerItem)
        adornerLayer.Add(adorner)
        adorner.Visibility = Visibility.Hidden
    End Sub


    Private Sub ShowAdorner()
        InitialiseAdorner()

        If ShowDecorator Then
            adorner.Visibility = Visibility.Visible
        Else
            adorner.Visibility = Visibility.Hidden
        End If


    End Sub
    Private Sub DesignerItemDecorator_Unloaded(sender As Object, e As RoutedEventArgs)
        If adorner Is Nothing Then Return
        Dim adornerLayer As AdornerLayer = AdornerLayer.GetAdornerLayer(Me)
        adornerLayer?.Remove(adorner)
        adorner = Nothing

    End Sub


    Private Shared Sub ShowDecoratorProperty_Changed(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim decorator As DesignerItemDecorator = d.TryCastAs(Of DesignerItemDecorator)
        If decorator IsNot Nothing Then
            If CBool(e.NewValue) Then
                decorator.ShowAdorner()
            Else
                decorator.HideAdorner()
            End If
        End If
    End Sub

    Private Sub DesignerItemDecorator_MouseDown(sender As Object, e As MouseButtonEventArgs)

        Dim ThisControl As ContentControl = TryCast(DataContext, ContentControl)

        If _currentSelected IsNot Nothing AndAlso _currentSelected IsNot ThisControl Then
            Selector.SetIsSelected(_currentSelected, False)
        End If

        If ThisControl.Content.Visibility <> Visibility.Visible Then Return

        Selector.SetIsSelected(ThisControl, True)
        _currentSelected = ThisControl
        adorner?.chrome.OnScaleChanged(New ScaleChangedMessage(ParentScale))
        e.Handled = True
    End Sub



End Class
