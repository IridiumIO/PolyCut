﻿Imports System.Windows.Media.Animation
Imports Wpf.Ui.Controls

Class MonitorPage : Implements INavigableView(Of MainViewModel)

    Public ReadOnly Property ViewModel As MainViewModel Implements INavigableView(Of MainViewModel).ViewModel


    Sub New(viewmodel As MainViewModel)

        Me.ViewModel = viewmodel
        DataContext = viewmodel
        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.

    End Sub


    Private Async Sub webView_IsVisibleChanged(sender As Object, e As DependencyPropertyChangedEventArgs) Handles webView.IsVisibleChanged

        Dim animation As New DoubleAnimation(-20, TimeSpan.FromSeconds(1))
        Dim storyboard As New Storyboard()

        ' Set the target property to the Y property of the TranslateTransform
        storyboard.SetTarget(animation, webViewTranslateTransform)
        storyboard.SetTargetProperty(animation, New PropertyPath(TranslateTransform.YProperty))

        ' Create a Storyboard and add the animation
        Storyboard.Children.Add(animation)

        ' Begin the animation
        AddHandler CompositionTarget.Rendering, Sub()
                                                    webView.UpdateWindowPos()
                                                End Sub

        storyboard.Begin()

    End Sub


    Private Sub CompositionTarget_Rendering(sender As Object, e As EventArgs)
        ' Get the current time of the animation
        webView.UpdateWindowPos()
    End Sub

End Class
