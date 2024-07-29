Imports System.Linq.Expressions
Imports System.Reflection

Public Class ResizeRotateChrome
    Inherits Control

    ' Default values for the properties
    Private Shared ReadOnly DefaultThumbSize As Double = 9.0
    Private Shared ReadOnly DefaultThumbStrokeThickness As Double = 1.0
    Private Shared ReadOnly DefaultThumbMargin As Thickness = New Thickness(-DefaultThumbSize / 2)

    Private Shared ReadOnly DefaultRotateThumbSize As Double = 50.0
    Private Shared ReadOnly DefaultRotateThumbOffset As Thickness = New Thickness(0, -48, 0, 0)
    Private Shared ReadOnly DefaultRotateThumbSymbolSize As Double = 18.0
    Private Shared ReadOnly DefaultRotateThumbBackSize As Double = 42.0

    Private Shared ReadOnly DefaultAdornedStrokeWidth As Double = 1.0

    Private Shared ReadOnly DefaultCardinalThumbSize As Double = 14.0
    Private Shared ReadOnly DefaultCardinalThumbMargin As Thickness = New Thickness(-DefaultCardinalThumbSize / 2)

    Public Shared ReadOnly ThumbSizeProperty As DependencyProperty = RegisterDependencyProperty(NameOf(ThumbSize), DefaultThumbSize)
    Public Shared ReadOnly ThumbStrokeThicknessProperty As DependencyProperty = RegisterDependencyProperty(NameOf(ThumbStrokeThickness), DefaultThumbStrokeThickness)
    Public Shared ReadOnly ThumbMarginProperty As DependencyProperty = RegisterDependencyProperty(NameOf(ThumbMargin), DefaultThumbMargin)

    Public Shared ReadOnly RotateThumbSizeProperty As DependencyProperty = RegisterDependencyProperty(NameOf(RotateThumbSize), DefaultRotateThumbSize)
    Public Shared ReadOnly RotateThumbOffsetProperty As DependencyProperty = RegisterDependencyProperty(NameOf(RotateThumbOffset), DefaultRotateThumbOffset)
    Public Shared ReadOnly RotateThumbSymbolSizeProperty As DependencyProperty = RegisterDependencyProperty(NameOf(RotateThumbSymbolSize), DefaultRotateThumbSymbolSize)
    Public Shared ReadOnly RotateThumbBackSizeProperty As DependencyProperty = RegisterDependencyProperty(NameOf(RotateThumbBackSize), DefaultRotateThumbBackSize)

    Public Shared ReadOnly AdornedStrokeWidthProperty As DependencyProperty = RegisterDependencyProperty(NameOf(AdornedStrokeWidth), DefaultAdornedStrokeWidth)

    Public Shared ReadOnly CardinalThumbSizeProperty As DependencyProperty = RegisterDependencyProperty(NameOf(CardinalThumbSize), DefaultCardinalThumbSize)
    Public Shared ReadOnly CardinalThumbMarginProperty As DependencyProperty = RegisterDependencyProperty(NameOf(CardinalThumbMargin), DefaultCardinalThumbMargin)

    Shared Sub New()
        FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(GetType(ResizeRotateChrome), New FrameworkPropertyMetadata(GetType(ResizeRotateChrome)))
    End Sub

    Public Sub OnScaleChanged(message As Object)
        If TypeOf message IsNot ScaleChangedMessage Then Return

        Dim newScale = CType(message, ScaleChangedMessage).NewScale
        Debug.WriteLine(newScale)
        ThumbSize = 9 / newScale
        ThumbMargin = New Thickness(-ThumbSize / 2)
        ThumbStrokeThickness = 1 / newScale

        RotateThumbSize = 50 / newScale
        RotateThumbOffset = New Thickness(0, -48 / newScale, 0, 0)
        RotateThumbSymbolSize = 18 / newScale
        RotateThumbBackSize = 42 / newScale

        AdornedStrokeWidth = 1 / newScale

        CardinalThumbSize = 14 / newScale
        CardinalThumbMargin = New Thickness(-CardinalThumbSize / 2)

        InvalidateArrange()

    End Sub

    Public Property ThumbSize As Double
        Get
            Return GetValue(ThumbSizeProperty)
        End Get
        Set(value As Double)
            SetValue(ThumbSizeProperty, value)
        End Set
    End Property

    Public Property RotateThumbSize As Double
        Get
            Return GetValue(RotateThumbSizeProperty)
        End Get
        Set(value As Double)
            SetValue(RotateThumbSizeProperty, value)
        End Set
    End Property

    Public Property AdornedStrokeWidth As Double
        Get
            Return GetValue(AdornedStrokeWidthProperty)
        End Get
        Set(value As Double)
            SetValue(AdornedStrokeWidthProperty, value)
        End Set
    End Property

    Public Property RotateThumbOffset As Thickness
        Get
            Return GetValue(RotateThumbOffsetProperty)
        End Get
        Set(value As Thickness)
            SetValue(RotateThumbOffsetProperty, value)
        End Set
    End Property

    Public Property RotateThumbBackSize As Double
        Get
            Return GetValue(RotateThumbBackSizeProperty)
        End Get
        Set(value As Double)
            SetValue(RotateThumbBackSizeProperty, value)
        End Set
    End Property

    Public Property ThumbMargin As Thickness
        Get
            Return GetValue(ThumbMarginProperty)
        End Get
        Set(value As Thickness)
            SetValue(ThumbMarginProperty, value)
        End Set
    End Property

    Public Property RotateThumbSymbolSize As Double
        Get
            Return GetValue(RotateThumbSymbolSizeProperty)
        End Get
        Set(value As Double)
            SetValue(RotateThumbSymbolSizeProperty, value)
        End Set
    End Property

    Public Property ThumbStrokeThickness As Double
        Get
            Return GetValue(ThumbStrokeThicknessProperty)
        End Get
        Set(value As Double)
            SetValue(ThumbStrokeThicknessProperty, value)
        End Set
    End Property

    Public Property CardinalThumbSize As Double
        Get
            Return GetValue(CardinalThumbSizeProperty)
        End Get
        Set(value As Double)
            SetValue(CardinalThumbSizeProperty, value)
        End Set
    End Property

    Public Property CardinalThumbMargin As Thickness
        Get
            Return GetValue(CardinalThumbMarginProperty)
        End Get
        Set(value As Thickness)
            SetValue(CardinalThumbMarginProperty, value)
        End Set
    End Property

    Private Shared Function RegisterDependencyProperty(Of T)(propertyName As String, defaultValue As T) As DependencyProperty
        Return DependencyProperty.Register(propertyName, GetType(T), GetType(ResizeRotateChrome), New PropertyMetadata(defaultValue))
    End Function


End Class


Public Class LazyDependency

    ' Generic method to register a DependencyProperty
    Public Shared Function Register(Of TOwner As DependencyObject, TProperty)(
        expression As Expression(Of Func(Of TOwner, TProperty)),
        typeMetadata As PropertyMetadata) As DependencyProperty

        ' Get the property name using the expression
        Dim member = TryCast(expression.Body, MemberExpression)
        If member Is Nothing Then
            Throw New ArgumentException("Expression is not a member access", NameOf(expression))
        End If

        Dim propertyInfo = TryCast(member.Member, PropertyInfo)
        If propertyInfo Is Nothing Then
            Throw New ArgumentException("Expression is not a property access", NameOf(expression))
        End If

        Dim propertyName = propertyInfo.Name

        ' Use reflection to get the property type
        Dim propertyType As Type = propertyInfo.PropertyType

        ' Register the DependencyProperty
        Return DependencyProperty.Register(propertyName, propertyType, GetType(TOwner), typeMetadata)
    End Function


End Class
