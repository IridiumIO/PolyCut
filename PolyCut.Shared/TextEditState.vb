Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media

Public NotInheritable Class TextEditState
    Private ReadOnly _text As String
    Private ReadOnly _characters As TextCharacteristics
    Private ReadOnly _width As Double
    Private ReadOnly _height As Double
    Private ReadOnly _transform As Transform
    Private ReadOnly _origin As Point
    Private ReadOnly _wrapperWidth As Double
    Private ReadOnly _wrapperHeight As Double
    Private ReadOnly _left As Double
    Private ReadOnly _top As Double
    Private ReadOnly _wrapperTransform As Transform
    Private ReadOnly _wrapperOrigin As Point
    Private ReadOnly _hasWrapper As Boolean

    Public Sub New(textBox As TextBox)
        _text = textBox.Text
        _characters = TextCharacteristics.FromTextBox(textBox)
        _width = textBox.Width
        _height = textBox.Height
        _transform = textBox.RenderTransform.CloneCurrentValue()
        _origin = textBox.RenderTransformOrigin
        Dim wrapper = TryCast(textBox.Parent, ContentControl)
        _hasWrapper = wrapper IsNot Nothing
        If Not _hasWrapper Then Return
        _wrapperWidth = wrapper.Width
        _wrapperHeight = wrapper.Height
        _left = Canvas.GetLeft(wrapper)
        _top = Canvas.GetTop(wrapper)
        _wrapperTransform = wrapper.RenderTransform.CloneCurrentValue()
        _wrapperOrigin = wrapper.RenderTransformOrigin
    End Sub

    Public Function HasChanges(other As TextEditState) As Boolean
        If other Is Nothing Then Return False
        ' Entering edit mode can auto-fit the layout even without an actual edit. Only text, typography, or an explicit font-size matrix change starts history.
        Return _text <> other._text OrElse Not _characters.SameAs(other._characters) OrElse
               _transform.Value <> other._transform.Value OrElse
               _hasWrapper <> other._hasWrapper OrElse
               (_hasWrapper AndAlso _wrapperTransform.Value <> other._wrapperTransform.Value)
    End Function

    Public Sub ApplyTo(textBox As TextBox)
        Dim restoring = TextEditHelper.GetIsRestoring(textBox)
        TextEditHelper.SetIsRestoring(textBox, True)
        Try
            textBox.Text = _text
            _characters.ApplyTo(textBox)
            textBox.Width = _width
            textBox.Height = _height
            textBox.RenderTransform = _transform.CloneCurrentValue()
            textBox.RenderTransformOrigin = _origin
            Dim wrapper = TryCast(textBox.Parent, ContentControl)
            If _hasWrapper AndAlso wrapper IsNot Nothing Then
                wrapper.Width = _wrapperWidth
                wrapper.Height = _wrapperHeight
                Canvas.SetLeft(wrapper, _left)
                Canvas.SetTop(wrapper, _top)
                wrapper.RenderTransform = _wrapperTransform.CloneCurrentValue()
                wrapper.RenderTransformOrigin = _wrapperOrigin
                wrapper.UpdateLayout()
            Else
                textBox.UpdateLayout()
            End If
        Finally
            TextEditHelper.SetIsRestoring(textBox, restoring)
        End Try
    End Sub
End Class
