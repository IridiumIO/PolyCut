Imports System.IO

Imports CommunityToolkit.Mvvm.ComponentModel
Imports CommunityToolkit.Mvvm.Input

Imports PolyCut.RichCanvas

Public Class SVGPageViewModel : Inherits ObservableObject

    Public Property MainVM As MainViewModel

    Public Property PreviewKeyDownCommand As ICommand = New RelayCommand(Of String)(Sub(key) ShortcutKeyHandler(key))


    Public Sub New(mainvm As MainViewModel)
        Me.MainVM = mainvm
    End Sub

    Private Sub ShortcutKeyHandler(Key As String)

        If (Key = "]") AndAlso Keyboard.IsKeyDown(Windows.Input.Key.LeftCtrl) Then
            Dim currentSelected = DesignerItemDecorator.CurrentSelected
            If currentSelected Is Nothing Then Return
            Dim textbox As TextBox = TryCast(currentSelected.Content, TextBox)
            If textbox Is Nothing Then Return
            Dim currentFontSize As Double = textbox.FontSize
            textbox.FontSize = currentFontSize + 1

        ElseIf (Key = "[") AndAlso Keyboard.IsKeyDown(Windows.Input.Key.LeftCtrl) Then
            Dim currentSelected = DesignerItemDecorator.CurrentSelected
            If currentSelected Is Nothing Then Return
            Dim textbox As TextBox = TryCast(currentSelected.Content, TextBox)
            If textbox Is Nothing Then Return
            Dim currentFontSize As Double = textbox.FontSize
            textbox.FontSize = currentFontSize - 1

        End If
    End Sub


    Public Sub ProcessDroppedFiles(files() As String)

        For Each file In files
            Dim finfo As New FileInfo(file)

            If finfo.Exists AndAlso finfo.Extension = ".svg" Then
                MainVM.ModifySVGFiles(New SVGFile(file))
            End If

        Next

    End Sub



End Class
