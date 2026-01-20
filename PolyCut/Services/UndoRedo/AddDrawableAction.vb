Imports PolyCut.[Shared]

Public Class AddDrawableAction : Implements IUndoableAction

    Private ReadOnly _manager As IDrawableManager
    Private ReadOnly _element As FrameworkElement
    Private _drawable As IDrawable
    Private _parentGroup As DrawableGroup
    Private _indexInCollection As Integer = -1

    Public Sub New(manager As IDrawableManager, element As FrameworkElement)
        _manager = manager
        _element = element
    End Sub

    Public Sub New(manager As IDrawableManager, element As FrameworkElement, parentGroup As DrawableGroup)
        _manager = manager
        _element = element
        _parentGroup = parentGroup
    End Sub


    Public ReadOnly Property Description As String Implements IUndoableAction.Description
        Get
            Return $"Add: {_drawable?.Name}"
        End Get
    End Property

    Public Function Execute() As Boolean Implements IUndoableAction.Execute
        _drawable = CreateDrawableFromElement(_element)
        If _drawable Is Nothing Then Return False

        AddDrawableToManager()

        ' Capture the index the drawable was placed at so redo can restore it exactly
        If _manager.DrawableCollection IsNot Nothing Then
            _indexInCollection = _manager.DrawableCollection.IndexOf(_drawable)
        End If

        Return True
    End Function

    Private Function CreateDrawableFromElement(element As FrameworkElement) As IDrawable
        Dim drawable As IDrawable = Nothing

        If TypeOf element Is Line Then
            drawable = New DrawableLine(element)
        ElseIf TypeOf element Is Rectangle Then
            drawable = New DrawableRectangle(element)
        ElseIf TypeOf element Is Ellipse Then
            drawable = New DrawableEllipse(element)
        ElseIf TypeOf element Is System.Windows.Controls.TextBox Then
            drawable = New DrawableText(element)
            drawable.StrokeThickness = 0
        ElseIf TypeOf element Is System.Windows.Shapes.Path Then
            drawable = New DrawablePath(element)
        End If

        If drawable IsNot Nothing Then
            drawable.Name = GenerateDrawableName(drawable)
        End If

        Return drawable
    End Function

    Private Function GenerateDrawableName(drawable As IDrawable) As String
        If TypeOf drawable Is DrawableText Then
            Dim textDrawable = CType(drawable, DrawableText)
            Dim textElement = TryCast(textDrawable.DrawableElement, System.Windows.Controls.TextBox)
            If textElement IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(textElement.Text) Then
                Dim textContent = textElement.Text.Trim()
                Return If(textContent.Length > 15, textContent.Substring(0, 15) & "...", textContent)
            End If
        End If

        Dim baseName As String

        If TypeOf drawable Is DrawableLine Then
            baseName = "Line"
        ElseIf TypeOf drawable Is DrawableRectangle Then
            baseName = "Rect"
        ElseIf TypeOf drawable Is DrawableEllipse Then
            baseName = "Ellipse"
        ElseIf TypeOf drawable Is DrawableText Then
            baseName = "Text"
        ElseIf TypeOf drawable Is DrawablePath Then
            baseName = "Path"
        Else
            Return "Drawable1"
        End If

        Dim existingNumbers As New HashSet(Of Integer)
        For Each d In _manager.DrawableCollection
            If d IsNot Nothing AndAlso Not String.IsNullOrEmpty(d.Name) AndAlso d.Name.StartsWith(baseName, StringComparison.OrdinalIgnoreCase) Then
                Dim numberPart = d.Name.Substring(baseName.Length)
                Dim number As Integer
                If Integer.TryParse(numberPart, number) Then
                    existingNumbers.Add(number)
                End If
            End If
        Next

        Dim nextNumber As Integer = 1
        While existingNumbers.Contains(nextNumber)
            nextNumber += 1
        End While

        Return $"{baseName}{nextNumber}"
    End Function

    Private Sub AddDrawableToManager()
        If _drawable Is Nothing Then Return

        Dim mainVM = TryCast(_manager, MainViewModel)
        If mainVM Is Nothing Then Return

        ' Respect a parent group if provided; otherwise fall back to DrawingGroup
        If _parentGroup Is Nothing Then
            _parentGroup = mainVM.DrawingGroup
        End If

        ' Ensure parent group is present in ImportedGroups if necessary
        If _parentGroup IsNot Nothing AndAlso Not (_parentGroup Is mainVM.DrawingGroup) Then
            If Not mainVM.ImportedGroups.Contains(_parentGroup) Then
                mainVM.ImportedGroups.Add(_parentGroup)
            End If
        End If

        _parentGroup.AddChild(_drawable)
        If Not _manager.DrawableCollection.Contains(_drawable) Then
            _manager.AddDrawableToCollection(_drawable, -1)
        End If
    End Sub

    Public Sub Undo() Implements IUndoableAction.Undo
        If _drawable Is Nothing Then Return

        _manager.RemoveDrawableFromCollection(_drawable)

        If _parentGroup IsNot Nothing Then
            _parentGroup.RemoveChild(_drawable)
        End If

        _manager.ClearDrawableParent(_drawable)
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        If _drawable Is Nothing Then Return

        If _parentGroup IsNot Nothing Then
            If Not _parentGroup.GroupChildren.Contains(_drawable) Then
                _parentGroup.AddChild(_drawable)
            End If

            If Not _manager.DrawableCollection.Contains(_drawable) Then
                _manager.AddDrawableToCollection(_drawable, _indexInCollection)
            End If
        Else
            _manager.AddDrawableToCollection(_drawable, _indexInCollection)
        End If
    End Sub

End Class



