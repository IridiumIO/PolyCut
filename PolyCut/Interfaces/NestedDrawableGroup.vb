Imports System.Collections.ObjectModel
Imports System.Collections.Specialized

Imports PolyCut.RichCanvas

Imports PolyCut.[Shared]

Imports Svg
Imports Svg.Transforms

Public Class NestedDrawableGroup : Inherits BaseDrawable : Implements IDrawable

    'Working:
    '- SVG import
    '- Canvas view/manipulation
    '- GCode generation 
    '- File project save
    '- Undo/redo

    'Known Not working
    '- Apply stroke/fill cascading to children elements
    '- Boolean ops with groups


    Public Property GroupChildren As New ObservableCollection(Of IDrawable)

    ' Flattened view for UI (all leaf drawables under this group, including nested groups)
    Public ReadOnly Property DisplayChildren As New ObservableCollection(Of IDrawable)
    Public Overloads ReadOnly Property VisualName As String Implements IDrawable.VisualName

    Private ReadOnly _innerCanvas As New Canvas With {.ClipToBounds = False}

    Public ReadOnly Property InnerCanvas As Canvas
        Get
            Return _innerCanvas
        End Get
    End Property

    Public Sub New(Optional groupName As String = "Group")
        Name = If(groupName, "Group")
        VisualName = "Group"

        ' Viewbox scales the inner canvas when wrapper is resized
        Dim vb As New Viewbox With {
            .Stretch = Stretch.Fill,
            .StretchDirection = StretchDirection.Both,
            .Child = _innerCanvas
        }

        MyBase.DrawableElement = vb

        AddHandler GroupChildren.CollectionChanged, AddressOf OnGroupChildrenChanged
    End Sub


    Public Shared Function CreateNestedGroup(children As IEnumerable(Of IDrawable), groupName As String) As NestedDrawableGroup
        Dim grp As New NestedDrawableGroup(groupName)

        Dim items = children?.
        Where(Function(d) d IsNot Nothing AndAlso d.DrawableElement IsNot Nothing).
        ToList()

        If items Is Nothing OrElse items.Count = 0 Then Return grp

        Dim style As Style = Nothing
        Try
            style = CType(Application.Current.FindResource("DesignerItemStyle"), Style)
        Catch
            style = Nothing
        End Try

        Dim wrappers As New List(Of (drawable As IDrawable, wrapper As ContentControl))

        For Each d In items
            Dim fe = d.DrawableElement
            Dim w = DrawableWrapperFactory.CreateDesignerWrapperForChild(fe, d, style)
            If w Is Nothing Then Continue For

            Dim left = Canvas.GetLeft(fe) : If Double.IsNaN(left) Then left = 0
            Dim top = Canvas.GetTop(fe) : If Double.IsNaN(top) Then top = 0

            Canvas.SetLeft(w, left)
            Canvas.SetTop(w, top)

            w.IsHitTestVisible = False

            wrappers.Add((d, w))
            grp.AddChild(d)
        Next

        Dim bounds As Rect = GetBoundsFromWrappers(wrappers.Select(Function(x) x.wrapper))

        ' Localize wrappers into group-local space and insert into INNER canvas (not DrawableElement)
        grp.InnerCanvas.Children.Clear()
        For Each pair In wrappers
            Dim w = pair.wrapper
            Dim wLeft = Canvas.GetLeft(w) : If Double.IsNaN(wLeft) Then wLeft = 0
            Dim wTop = Canvas.GetTop(w) : If Double.IsNaN(wTop) Then wTop = 0

            Canvas.SetLeft(w, wLeft - bounds.Left)
            Canvas.SetTop(w, wTop - bounds.Top)

            grp.InnerCanvas.Children.Add(w)
        Next

        ' IMPORTANT: set the "native" size of the inner canvas so the Viewbox has a base size to scale from
        grp.InnerCanvas.Width = bounds.Width
        grp.InnerCanvas.Height = bounds.Height

        ' Now set the Viewbox (DrawableElement) size+world position so PolyCanvas wrapper will pick it up
        Dim vb = TryCast(grp.DrawableElement, Viewbox)
        If vb IsNot Nothing Then
            vb.Width = bounds.Width
            vb.Height = bounds.Height
            Canvas.SetLeft(vb, bounds.Left)
            Canvas.SetTop(vb, bounds.Top)
        End If

        Return grp
    End Function


    Private Sub OnGroupChildrenChanged(sender As Object, e As NotifyCollectionChangedEventArgs)
        RebuildDisplayChildren()
    End Sub


    Private Iterator Function EnumerateLeafChildren() As IEnumerable(Of IDrawable)
        For Each ch In GroupChildren
            If ch Is Nothing Then Continue For

            Dim nested = TryCast(ch, NestedDrawableGroup)
            If nested IsNot Nothing Then
                For Each leaf In nested.EnumerateLeafChildren()
                    Yield leaf
                Next
            Else
                Yield ch
            End If
        Next
    End Function

    Public Sub RebuildDisplayChildren()
        DisplayChildren.Clear()
        For Each leaf In EnumerateLeafChildren()
            DisplayChildren.Add(leaf)
        Next
    End Sub

    Public Shadows Property Children As IEnumerable(Of IDrawable)
        Get
            Return GroupChildren
        End Get
        Set(value As IEnumerable(Of IDrawable))
            RemoveHandler GroupChildren.CollectionChanged, AddressOf OnGroupChildrenChanged
            GroupChildren.Clear()
            If value IsNot Nothing Then
                For Each c In value
                    GroupChildren.Add(c)
                Next
            End If
            AddHandler GroupChildren.CollectionChanged, AddressOf OnGroupChildrenChanged
            RebuildDisplayChildren()
        End Set
    End Property

    Public Sub AddChild(child As IDrawable)
        If child Is Nothing Then Return
        If GroupChildren.Contains(child) Then Return

        If child.ParentGroup IsNot Nothing Then
            Dim prior = TryCast(child.ParentGroup, DrawableGroup)
            If prior IsNot Nothing Then prior.GroupChildren.Remove(child)
        End If
        child.ParentGroup = Me
        GroupChildren.Add(child)
    End Sub

    Public Sub RemoveChild(child As IDrawable)
        If child Is Nothing Then Return
        If GroupChildren.Contains(child) Then
            GroupChildren.Remove(child)
            child.ParentGroup = Nothing
        End If
    End Sub


    Public Function GetAllLeafChildren() As List(Of IDrawable)
        Return EnumerateLeafChildren().ToList()
    End Function



    Public Overloads Function DrawingToSVG() As SvgVisualElement Implements IDrawable.DrawingToSVG
        Dim g As New SvgGroup()
        Return g
    End Function

    Public Overloads Function GetTransformedSVGElement() As SvgVisualElement Implements IDrawable.GetTransformedSVGElement
        Dim gRoot As New SvgGroup()
        If gRoot.Transforms Is Nothing Then gRoot.Transforms = New SvgTransformCollection()

        ' ----- Find group wrapper + viewbox + inner canvas -----
        Dim vb = TryCast(Me.DrawableElement, Viewbox)
        Dim inner = TryCast(vb?.Child, Canvas)

        Dim groupWrapper = TryCast(Me.DrawableElement?.Parent, ContentControl)

        ' 1) Apply GROUP WRAPPER transforms to gRoot (translate + rendertransform about center)
        If groupWrapper IsNot Nothing Then
            ApplyWrapperTransformAsSvgGroup(gRoot, groupWrapper)
        Else
            ' Fallback if export before it’s wrapped (I forget why I needed this)
            Dim fe = TryCast(Me.DrawableElement, FrameworkElement)
            If fe IsNot Nothing Then ApplyCanvasTranslateIfAny(gRoot, fe)
        End If

        ' 2) Apply VIEWBOX layout scaling as a nested group
        Dim gScaled As New SvgGroup()
        If gScaled.Transforms Is Nothing Then gScaled.Transforms = New SvgTransformCollection()

        If vb IsNot Nothing AndAlso inner IsNot Nothing Then
            Dim baseW = GetWidthSafe(inner)
            Dim baseH = GetHeightSafe(inner)

            Dim finalW As Double = If(groupWrapper IsNot Nothing, GetWidthSafe(groupWrapper), GetWidthSafe(vb))
            Dim finalH As Double = If(groupWrapper IsNot Nothing, GetHeightSafe(groupWrapper), GetHeightSafe(vb))

            If baseW > 0 AndAlso baseH > 0 AndAlso finalW > 0 AndAlso finalH > 0 Then
                gScaled.Transforms.Add(New SvgScale(CSng(finalW / baseW), CSng(finalH / baseH)))
            End If

        End If

        ' 3) Export children under gScaled so their translations/centers scale correctly
        Dim groupCanvas As Canvas = inner
        If groupCanvas IsNot Nothing Then
            For Each childDrawable In Me.GroupChildren
                If childDrawable Is Nothing Then Continue For

                Dim childWrapper = TryCast(childDrawable.DrawableElement?.Parent, ContentControl)
                If childWrapper Is Nothing Then Continue For

                Dim gChild As New SvgGroup()
                ApplyWrapperTransformAsSvgGroup(gChild, childWrapper)

                Dim childSvg As SvgVisualElement = Nothing
                Try
                    Dim nested = TryCast(childDrawable, NestedDrawableGroup)
                    If nested IsNot Nothing Then
                        ' IMPORTANT: nested groups export their !!contents!! (viewbox scale + children),
                        ' NOT their outer wrapper transform (already applied it via gChild)
                        childSvg = nested.GetSvgContentsOnly(childWrapper)
                    Else
                        childSvg = childDrawable.DrawingToSVG()
                    End If
                Catch
                    childSvg = Nothing
                End Try
                If childSvg Is Nothing Then Continue For

                gChild.Children.Add(childSvg)
                gScaled.Children.Add(gChild)
            Next
        End If

        gRoot.Children.Add(gScaled)
        Return gRoot
    End Function
    ' --- Helpers ---

    Friend Function GetSvgContentsOnly(selfWrapper As ContentControl) As SvgGroup
        Dim gScaled As New SvgGroup()
        If gScaled.Transforms Is Nothing Then gScaled.Transforms = New SvgTransformCollection()

        Dim vb = TryCast(Me.DrawableElement, Viewbox)
        Dim inner = TryCast(vb?.Child, Canvas)

        ' --- Viewbox scale (Stretch.Fill) ---
        If vb IsNot Nothing AndAlso inner IsNot Nothing AndAlso selfWrapper IsNot Nothing Then
            Dim baseW = GetWidthSafe(inner)
            Dim baseH = GetHeightSafe(inner)

            Dim finalW = GetWidthSafe(selfWrapper)
            Dim finalH = GetHeightSafe(selfWrapper)

            If baseW > 0 AndAlso baseH > 0 AndAlso finalW > 0 AndAlso finalH > 0 Then
                gScaled.Transforms.Add(New SvgScale(CSng(finalW / baseW), CSng(finalH / baseH)))
            End If
        End If

        ' --- Children (recurse groups) ---
        For Each childDrawable In Me.GroupChildren
            If childDrawable Is Nothing Then Continue For

            Dim childWrapper = TryCast(childDrawable.DrawableElement?.Parent, ContentControl)
            If childWrapper Is Nothing Then Continue For

            Dim gChild As New SvgGroup()
            ApplyWrapperTransformAsSvgGroup(gChild, childWrapper)

            Dim childSvg As SvgVisualElement = Nothing

            Dim nested = TryCast(childDrawable, NestedDrawableGroup)
            If nested IsNot Nothing Then
                ' Nested group: export its CONTENTS only (so wrapper transform isn't doubled)
                childSvg = nested.GetSvgContentsOnly(childWrapper)
            Else
                ' Leaf: local SVG
                childSvg = childDrawable.DrawingToSVG()
            End If

            If childSvg Is Nothing Then Continue For
            gChild.Children.Add(childSvg)
            gScaled.Children.Add(gChild)
        Next

        Return gScaled
    End Function

    Private Shared Sub ApplyWrapperTransformAsSvgGroup(g As SvgGroup, wrapper As ContentControl)
        If g.Transforms Is Nothing Then g.Transforms = New SvgTransformCollection()

        ' 1) Canvas translate
        Dim left = GetLeftSafe(wrapper)
        Dim top = GetTopSafe(wrapper)
        If left <> 0 OrElse top <> 0 Then g.Transforms.Add(New SvgTranslate(CSng(left), CSng(top)))

        ' 2) RenderTransform about wrapper center
        Dim rt As Transform = wrapper.RenderTransform
        If rt Is Nothing OrElse rt.Value.IsIdentity Then Return

        Dim w = GetWidthSafe(wrapper)
        Dim h = GetHeightSafe(wrapper)
        Dim cx As Single = w / 2.0
        Dim cy As Single = h / 2.0
        Dim r = TryCast(rt, RotateTransform)

        If r IsNot Nothing Then
            g.Transforms.Add(New SvgRotate(r.Angle, cx, cy))
            Return
        End If

        Dim s = TryCast(rt, ScaleTransform)
        If s IsNot Nothing Then
            g.Transforms.Add(New SvgTranslate(cx, cy))
            g.Transforms.Add(New SvgScale(s.ScaleX, s.ScaleY))
            g.Transforms.Add(New SvgTranslate(-cx, -cy))
            Return
        End If

        ' Fallback: matrix 
        Dim m As Matrix = rt.Value
        Dim composed As Matrix = Matrix.Identity
        composed.Translate(cx, cy)
        composed = Matrix.Multiply(m, composed)
        composed.Translate(-cx, -cy)

        ApplySvgMatrixTransform(g, composed)
    End Sub

    Private Shared Sub ApplySvgMatrixTransform(svgElem As SvgVisualElement, m As Matrix)
        If svgElem.Transforms Is Nothing Then svgElem.Transforms = New SvgTransformCollection()
        Dim values As New List(Of Single) From {m.M11, m.M12, m.M21, m.M22, m.OffsetX, m.OffsetY}
        svgElem.Transforms.Add(New SvgMatrix(values))
    End Sub

    Private Shared Sub ApplyCanvasTranslateIfAny(g As SvgGroup, fe As FrameworkElement)
        If g.Transforms Is Nothing Then g.Transforms = New SvgTransformCollection()
        Dim left = GetLeftSafe(fe)
        Dim top = GetTopSafe(fe)
        If left <> 0 OrElse top <> 0 Then g.Transforms.Add(New SvgTranslate(CSng(left), CSng(top)))
    End Sub
    Public Sub SetNativeSize(w As Double, h As Double)
        Dim inner = GetInnerCanvas()
        If inner Is Nothing Then Return

        If w > 0 AndAlso Not Double.IsNaN(w) Then inner.Width = w
        If h > 0 AndAlso Not Double.IsNaN(h) Then inner.Height = h
    End Sub

    Public Function GetNativeSize() As (Double, Double)
        Dim inner = GetInnerCanvas()
        If inner Is Nothing Then Return (0, 0)
        Return (inner.Width, inner.Height)
    End Function

    Public Sub RebuildGroupVisualFromChildren(designerItemStyle As Style)
        Dim vb = TryCast(Me.DrawableElement, Viewbox)
        If vb Is Nothing Then Return

        Dim inner = TryCast(vb.Child, Canvas)
        If inner Is Nothing Then
            inner = New Canvas With {.ClipToBounds = False}
            vb.Child = inner
        End If

        inner.Children.Clear()

        ' Compute bounds in WORLD space based on child element positions + sizes
        Dim bounds = CalculateWorldBoundsFromChildren()

        ' Set native size if not already present (needed for Viewbox scaling)
        If (inner.Width <= 0 OrElse Double.IsNaN(inner.Width)) AndAlso bounds.Width > 0 Then inner.Width = bounds.Width
        If (inner.Height <= 0 OrElse Double.IsNaN(inner.Height)) AndAlso bounds.Height > 0 Then inner.Height = bounds.Height

        ' Ensure the Viewbox has a concrete size if missing
        Dim vbe = DirectCast(vb, FrameworkElement)
        If (vbe.Width <= 0 OrElse Double.IsNaN(vbe.Width)) AndAlso bounds.Width > 0 Then vbe.Width = bounds.Width
        If (vbe.Height <= 0 OrElse Double.IsNaN(vbe.Height)) AndAlso bounds.Height > 0 Then vbe.Height = bounds.Height

        ' World position for the group element (PolyCanvas wrappering reads this)
        Canvas.SetLeft(vbe, bounds.Left)
        Canvas.SetTop(vbe, bounds.Top)

        ' Create child wrappers and place them in LOCAL space
        For Each child In GroupChildren
            If child?.DrawableElement Is Nothing Then Continue For

            Dim fe = child.DrawableElement

            Dim left = GetLeftSafe(fe)
            Dim top = GetTopSafe(fe)

            Dim wrapper = DrawableWrapperFactory.CreateDesignerWrapperForChild(fe, child, designerItemStyle)
            If wrapper Is Nothing Then Continue For

            ' Localize into group space
            Canvas.SetLeft(wrapper, left - bounds.Left)
            Canvas.SetTop(wrapper, top - bounds.Top)

            wrapper.IsHitTestVisible = False
            inner.Children.Add(wrapper)

            ' Keep model links consistent
            child.ParentGroup = Me
        Next
    End Sub

    Private Function GetInnerCanvas() As Canvas
        Dim vb = TryCast(Me.DrawableElement, Viewbox)
        Return TryCast(vb?.Child, Canvas)
    End Function

    Private Function CalculateWorldBoundsFromChildren() As Rect
        Dim minX = Double.MaxValue, minY = Double.MaxValue
        Dim maxX = Double.MinValue, maxY = Double.MinValue

        For Each child In GroupChildren
            Dim fe = child?.DrawableElement
            If fe Is Nothing Then Continue For

            Dim left = GetLeftSafe(fe)
            Dim top = GetTopSafe(fe)
            Dim w = GetWidthSafe(fe)
            Dim h = GetHeightSafe(fe)

            minX = Math.Min(minX, left)
            minY = Math.Min(minY, top)
            maxX = Math.Max(maxX, left + w)
            maxY = Math.Max(maxY, top + h)
        Next

        If minX = Double.MaxValue Then Return New Rect(0, 0, 0, 0)
        Return New Rect(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY))
    End Function

    Private Shared Function GetBoundsFromWrappers(wrappers As IEnumerable(Of ContentControl)) As Rect
        Dim minX = Double.MaxValue, minY = Double.MaxValue
        Dim maxX = Double.MinValue, maxY = Double.MinValue

        For Each w In wrappers
            If w Is Nothing Then Continue For
            Dim left = CanvasUtil.GetLeftSafe(w)
            Dim top = CanvasUtil.GetTopSafe(w)
            Dim ww = CanvasUtil.GetWidthSafe(w)
            Dim hh = CanvasUtil.GetHeightSafe(w)

            minX = Math.Min(minX, left)
            minY = Math.Min(minY, top)
            maxX = Math.Max(maxX, left + ww)
            maxY = Math.Max(maxY, top + hh)
        Next

        If minX = Double.MaxValue Then Return New Rect(0, 0, 0, 0)
        Return New Rect(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY))
    End Function



End Class



Public Module DrawableWrapperFactory

    'TODO: Merge with Polycanvas.AddChild creation so it's all in one place
    Public Function CreateDesignerWrapperForChild(
        child As FrameworkElement,
        parentIDrawable As IDrawable,
        designerItemStyle As Style
    ) As ContentControl

        If child Is Nothing Then Return Nothing

        Dim wrapper As New ContentControl With {
            .Content = child,
            .Width = If(Not Double.IsNaN(child.Width) AndAlso child.Width > 0, child.Width, child.ActualWidth),
            .Height = If(Not Double.IsNaN(child.Height) AndAlso child.Height > 0, child.Height, child.ActualHeight),
            .RenderTransform = New RotateTransform(0),
            .Background = Brushes.Transparent,
            .IsHitTestVisible = True,
            .ClipToBounds = False,
            .Style = designerItemStyle
        }

        If TypeOf child Is Canvas Then DirectCast(child, Canvas).ClipToBounds = True

        If TypeOf child Is Line Then
            Dim line = DirectCast(child, Line)
            wrapper.Width = Math.Abs(line.X2 - line.X1) + line.StrokeThickness
            wrapper.Height = Math.Abs(line.Y2 - line.Y1) + line.StrokeThickness
            MetadataHelper.SetOriginalEndPoint(wrapper, New Point(line.X2, line.Y2))
        ElseIf TypeOf child Is Path Then
            DirectCast(child, Path).Stretch = Stretch.Fill
        End If

        MetadataHelper.SetOriginalDimensions(wrapper, (wrapper.Width, wrapper.Height))

        child.HorizontalAlignment = HorizontalAlignment.Stretch
        child.Width = Double.NaN
        child.Height = Double.NaN

        If parentIDrawable IsNot Nothing Then MetadataHelper.SetDrawableReference(wrapper, parentIDrawable)

        Return wrapper
    End Function




End Module
