Imports System.Windows.Controls.Primitives

Imports CommunityToolkit.Mvvm.ComponentModel

Imports PolyCut.Shared

Imports Svg

Public Class BaseDrawable : Inherits ObservableObject : Implements IDrawable

    Public Property Name As String Implements IDrawable.Name
    Public Property DrawableElement As FrameworkElement Implements IDrawable.DrawableElement
    Public Property Children As IEnumerable(Of IDrawable) Implements IDrawable.Children
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
        Throw New NotImplementedException()
    End Function

    Public Function DrawingToSVG() As SvgVisualElement Implements IDrawable.DrawingToSVG
        Throw New NotImplementedException()
    End Function
End Class
