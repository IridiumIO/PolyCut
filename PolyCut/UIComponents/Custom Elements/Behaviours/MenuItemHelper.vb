Imports System.ComponentModel
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Data
Imports System.Windows.Media

Public NotInheritable Class MenuItemHelper

    Public Shared ReadOnly FooterProperty As DependencyProperty =
        DependencyProperty.RegisterAttached(
            "Footer",
            GetType(String),
            GetType(MenuItemHelper),
            New PropertyMetadata(Nothing, AddressOf OnFooterChanged))

    Public Shared Sub SetFooter(obj As DependencyObject, value As String)
        obj.SetValue(FooterProperty, value)
    End Sub

    Public Shared Function GetFooter(obj As DependencyObject) As String
        Return CType(obj.GetValue(FooterProperty), String)
    End Function

    Private Shared ReadOnly _isHookedProperty As DependencyProperty =
        DependencyProperty.RegisterAttached(
            "IsHooked",
            GetType(Boolean),
            GetType(MenuItemHelper),
            New PropertyMetadata(False))

    Private Shared Sub OnFooterChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim mi = TryCast(d, MenuItem)
        If mi Is Nothing Then Return

        ' Hook once, then rebuild on Loaded (and also when Header chsanges)
        If Not CBool(mi.GetValue(_isHookedProperty)) Then
            mi.SetValue(_isHookedProperty, True)

            AddHandler mi.Loaded,
                Sub(sender, args)
                    RebuildHeader(DirectCast(sender, MenuItem))
                End Sub

            ' Rebuild if Header changes later
            DependencyPropertyDescriptor.FromProperty(HeaderedItemsControl.HeaderProperty, GetType(MenuItem)).
                AddValueChanged(mi,
                    Sub(sender, args)
                        RebuildHeader(DirectCast(sender, MenuItem))
                    End Sub)
        End If

        RebuildHeader(mi)
    End Sub

    Private Shared Sub RebuildHeader(mi As MenuItem)
        Dim footerText = GetFooter(mi)
        If String.IsNullOrWhiteSpace(footerText) Then Return

        Dim existingGrid = TryCast(mi.Header, Grid)
        If existingGrid IsNot Nothing AndAlso Equals(existingGrid.Tag, GetType(MenuItemHelper)) Then
            Dim tb = existingGrid.Children.OfType(Of TextBlock)().FirstOrDefault()
            If tb IsNot Nothing Then tb.Text = footerText
            Return
        End If

        Dim headerText = TryCast(mi.Header, String)
        If String.IsNullOrWhiteSpace(headerText) Then Return

        Dim grid As New Grid With {
            .Tag = GetType(MenuItemHelper),' marker
            .MinWidth = 170
        }
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        grid.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})

        ' Use AccessText so _Save/_OPen access keys still work
        Dim header As New AccessText With {
            .Text = headerText,
            .VerticalAlignment = VerticalAlignment.Center
        }

        Dim footer As New TextBlock With {
            .Text = footerText,
            .VerticalAlignment = VerticalAlignment.Center,
            .HorizontalAlignment = HorizontalAlignment.Right,
            .FontSize = 10,
            .Margin = New Thickness(15, 0, 0, 0),
            .Opacity = 0.55
        }

        ' Bind footer foreground to MenuItem.Foreground so it works in light/dark themes (if I ever do that...)
        footer.SetBinding(TextBlock.ForegroundProperty, New Binding("Foreground") With {.Source = mi})

        Grid.SetColumn(header, 0)
        Grid.SetColumn(footer, 1)

        grid.Children.Add(header)
        grid.Children.Add(footer)

        mi.Header = grid
    End Sub

End Class
