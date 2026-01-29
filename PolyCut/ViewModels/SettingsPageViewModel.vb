Imports CommunityToolkit.Mvvm.ComponentModel

Public Class SettingsPageViewModel : Inherits ObservableObject

    <ObservableProperty> Private _MainVM As MainViewModel

    Private _GridConfig As GridConfiguration

    Sub New(viewmodel As MainViewModel)
        Me.MainVM = viewmodel
        Me._GridConfig = MainVM.UIConfiguration.GridConfig

        AddHandler MainVM.Printer.PropertyChanged, Sub(sender, e)
                                                       If e.PropertyName = NameOf(Printer.BedWidth) OrElse e.PropertyName = NameOf(Printer.BedHeight) Then
                                                           OnPropertyChanged(NameOf(GridClipRect))
                                                       End If
                                                   End Sub

        AddHandler _GridConfig.PropertyChanged, Sub(sender, e)

                                                    OnPropertyChanged(NameOf(GridLineVerticalEnd))
                                                    OnPropertyChanged(NameOf(GridLineHorizontalEnd))
                                                    OnPropertyChanged(NameOf(PrinterGridPreviewViewport))
                                                    OnPropertyChanged(NameOf(GridClipRect))

                                                End Sub

    End Sub



    Public ReadOnly Property PrinterGridPreviewViewport As Rect
        Get
            Return New Rect(_GridConfig.InsetLeft, _GridConfig.InsetTop, _GridConfig.Spacing, _GridConfig.Spacing) : End Get
    End Property

    Public ReadOnly Property GridLineVerticalEnd As Point
        Get
            Return New Point(0, _GridConfig.Spacing)
        End Get
    End Property

    Public ReadOnly Property GridLineHorizontalEnd As Point
        Get
            Return New Point(_GridConfig.Spacing, 0)
        End Get
    End Property

    Public ReadOnly Property GridClipRect As Rect
        Get
            Dim left = _GridConfig.InsetLeft
            Dim top = _GridConfig.InsetTop
            Dim right = MainVM.Printer.BedWidth - _GridConfig.InsetRight
            Dim bottom = MainVM.Printer.BedHeight - _GridConfig.InsetBottom
            Return New Rect(left, top, right - left, bottom - top)
        End Get
    End Property

End Class
