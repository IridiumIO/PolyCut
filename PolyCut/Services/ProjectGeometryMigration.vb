Imports PolyCut.Shared

Friend NotInheritable Class ProjectGeometryMigration
    Private Sub New()
    End Sub

    Public Shared Sub Upgrade(project As ProjectData)
        Dim nestedChildren As New HashSet(Of Guid)(project.Groups.
            Where(Function(g) String.Equals(g.GroupType, "NestedDrawableGroup", StringComparison.OrdinalIgnoreCase)).
            SelectMany(Function(g) g.ChildIds))
        For Each data As DrawableData In project.Drawables
            If data.GeometricBounds Then Continue For
            Select Case data.Type
                Case "Rectangle", "Ellipse"
                    data.GeometricBounds = True
                Case "Path"
                    Dim nested = nestedChildren.Contains(data.Id)
                    If Not String.IsNullOrWhiteSpace(data.PathData) Then
                        Dim bounds = Geometry.Parse(data.PathData).Bounds
                        If Not bounds.IsEmpty AndAlso (nested OrElse bounds.Width = 0 OrElse bounds.Height = 0) Then Rebase(data, bounds)
                    End If
                    data.GeometricBounds = True
                Case "Line"
                    Dim stroke = Math.Max(0, data.StrokeThickness)
                    Dim oldWidth = If(data.Width > 0, data.Width, Math.Abs(data.LineX2 - data.LineX1) + stroke)
                    Dim oldHeight = If(data.Height > 0, data.Height, Math.Abs(data.LineY2 - data.LineY1) + stroke)
                    Dim start As New Point(data.LineX1, data.LineY1)
                    Dim finish As New Point(data.LineX2, data.LineY2)
                    If oldWidth > 0 AndAlso oldHeight > 0 Then
                        Dim xMin = Math.Min(oldWidth / 2, stroke / 2)
                        Dim yMin = Math.Min(oldHeight / 2, stroke / 2)
                        Dim xMax = Math.Max(xMin, oldWidth - stroke / 2)
                        Dim yMax = Math.Max(yMin, oldHeight - stroke / 2)
                        start = New Point(If(data.LineX1 <= data.LineX2, xMin, xMax), If(data.LineY1 <= data.LineY2, yMin, yMax))
                        finish = New Point(If(data.LineX1 <= data.LineX2, xMax, xMin), If(data.LineY1 <= data.LineY2, yMax, yMin))
                    End If
                    data.Width = oldWidth
                    data.Height = oldHeight
                    Rebase(data, New Rect(start, finish))
                    data.LineX1 = If(start.X <= finish.X, 0, 1)
                    data.LineY1 = If(start.Y <= finish.Y, 0, 1)
                    data.LineX2 = If(start.X <= finish.X, 1, 0)
                    data.LineY2 = If(start.Y <= finish.Y, 1, 0)
                    data.GeometricBounds = True
            End Select
        Next
    End Sub

    Private Shared Sub Rebase(data As DrawableData, bounds As Rect)
        ' Move the local origin to the new geometry bounds without moving any cutting points. Presever old transform points
        Dim oldWidth = data.Width
        Dim oldHeight = data.Height
        Dim mirror As Matrix = Matrix.Identity
        mirror.ScaleAt(data.ScaleX, data.ScaleY, oldWidth / 2, oldHeight / 2)
        Dim affine = If(String.IsNullOrWhiteSpace(data.AffineTransform), Matrix.Identity, Matrix.Parse(data.AffineTransform))
        affine.Rotate(data.RotationAngle)
        Dim pivot As New Point(oldWidth * data.TransformOriginX, oldHeight * data.TransformOriginY)
        Dim world As Matrix = Matrix.Identity
        world.Translate(-pivot.X, -pivot.Y)
        world.Append(affine)
        world.Translate(pivot.X + data.Left, pivot.Y + data.Top)

        Dim oldOrigin = world.Transform(mirror.Transform(New Point(bounds.X, bounds.Y)))
        Dim newMirror As Matrix = Matrix.Identity
        newMirror.ScaleAt(data.ScaleX, data.ScaleY, bounds.Width / 2, bounds.Height / 2)
        Dim newPivot As New Point(bounds.Width * data.TransformOriginX, bounds.Height * data.TransformOriginY)
        Dim newWorld As Matrix = Matrix.Identity
        newWorld.Translate(-newPivot.X, -newPivot.Y)
        newWorld.Append(affine)
        newWorld.Translate(newPivot.X, newPivot.Y)
        Dim newOrigin = newWorld.Transform(newMirror.Transform(New Point()))
        data.Width = bounds.Width
        data.Height = bounds.Height
        data.Left = oldOrigin.X - newOrigin.X
        data.Top = oldOrigin.Y - newOrigin.Y
    End Sub
End Class
