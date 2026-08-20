Imports System
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.IO
Imports System.Windows.Forms
Imports LakeUI

Namespace videoenhancer

    Friend Module QuadGridDrawing
        Friend Function RoundedPath(rect As RectangleF, radius As Single) As GraphicsPath
            Dim path As New GraphicsPath()
            Dim d = Math.Max(1.0F, Math.Min(radius * 2.0F, Math.Min(rect.Width, rect.Height)))
            path.AddArc(rect.X, rect.Y, d, d, 180, 90)
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90)
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90)
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90)
            path.CloseFigure()
            Return path
        End Function

        Friend Sub DrawImageCover(g As Graphics, image As Image, target As Rectangle)
            If image Is Nothing OrElse target.Width <= 0 OrElse target.Height <= 0 Then Return
            Dim sourceRatio = image.Width / CDbl(Math.Max(1, image.Height))
            Dim targetRatio = target.Width / CDbl(Math.Max(1, target.Height))
            Dim source As RectangleF
            If sourceRatio > targetRatio Then
                Dim width = CSng(image.Height * targetRatio)
                source = New RectangleF((image.Width - width) / 2.0F, 0, width, image.Height)
            Else
                Dim height = CSng(image.Width / targetRatio)
                source = New RectangleF(0, (image.Height - height) / 2.0F, image.Width, height)
            End If
            g.DrawImage(image, target, source.X, source.Y, source.Width, source.Height, GraphicsUnit.Pixel)
        End Sub
    End Module

    ''' <summary>圆角、抗锯齿、带悬停状态的按钮；完全编译进插件，无额外运行库。</summary>
    Friend Class SmoothButton
        Inherits Button

        Private _hovered As Boolean
        Friend Property CornerRadius As Integer = 8
        Friend Property FillColor As Color = Color.FromArgb(42, 46, 54)
        Friend Property HoverFillColor As Color = Color.FromArgb(53, 59, 68)
        Friend Property PressedFillColor As Color = Color.FromArgb(34, 38, 45)
        Friend Property BorderColor As Color = Color.FromArgb(76, 84, 94)
        Friend Property BorderThickness As Single = 1.0F

        Public Sub New()
            FlatStyle = FlatStyle.Flat
            FlatAppearance.BorderSize = 0
            UseVisualStyleBackColor = False
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        End Sub

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            _hovered = True
            Invalidate()
            MyBase.OnMouseEnter(e)
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            _hovered = False
            Invalidate()
            MyBase.OnMouseLeave(e)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
            Dim rect = New RectangleF(0.5F, 0.5F, Math.Max(1, Width - 1.0F), Math.Max(1, Height - 1.0F))
            Using path = QuadGridDrawing.RoundedPath(rect, CornerRadius)
                Dim fill = If(Control.MouseButtons = System.Windows.Forms.MouseButtons.Left AndAlso ClientRectangle.Contains(PointToClient(Cursor.Position)),
                              PressedFillColor, If(_hovered, HoverFillColor, FillColor))
                Using brush As New SolidBrush(fill)
                    e.Graphics.FillPath(brush, path)
                End Using
                If BorderThickness > 0 Then
                    Using pen As New Pen(BorderColor, BorderThickness)
                        e.Graphics.DrawPath(pen, path)
                    End Using
                End If
            End Using
            Dim flags = TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis
            Select Case TextAlign
                Case ContentAlignment.BottomLeft, ContentAlignment.MiddleLeft, ContentAlignment.TopLeft
                    flags = flags Or TextFormatFlags.Left
                Case ContentAlignment.BottomRight, ContentAlignment.MiddleRight, ContentAlignment.TopRight
                    flags = flags Or TextFormatFlags.Right
                Case Else
                    flags = flags Or TextFormatFlags.HorizontalCenter
            End Select
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, flags)
        End Sub
    End Class

    ''' <summary>视频输入卡片：缩略图、编号、文件名和大号拖放提示在同一圆角控件内绘制。</summary>
    Friend Class VideoSlotCard
        Inherits Control

        Private _previewImage As Image
        Private _filePath As String = ""
        Private ReadOnly _badge As New HtmlColorLabel()
        Private ReadOnly _hint As New HtmlColorLabel()
        Private ReadOnly _fileName As New HtmlColorLabel()
        Friend Property SlotIndex As Integer
        Friend ReadOnly Property FilePath As String
            Get
                Return _filePath
            End Get
        End Property

        Friend Sub SetVideo(path As String)
            _filePath = If(path, "")
            AccessibleName = If(String.IsNullOrWhiteSpace(_filePath), "空视频槽", System.IO.Path.GetFileName(_filePath))
            _badge.BackColor1 = If(String.IsNullOrWhiteSpace(_filePath), Color.FromArgb(38, 43, 50), Color.FromArgb(28, 157, 99))
            _hint.Visible = String.IsNullOrWhiteSpace(_filePath)
            _fileName.Visible = Not _hint.Visible
            _fileName.Text = If(_hint.Visible, "", System.IO.Path.GetFileName(_filePath))
            Invalidate()
        End Sub

        Friend Sub SetPreviewImage(image As Image)
            _previewImage = image
            Invalidate()
        End Sub

        Public Sub New()
            AllowDrop = True
            Cursor = Cursors.Hand
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw Or
                     ControlStyles.SupportsTransparentBackColor, True)
            BackColor = Color.Transparent
            _badge.Text = "1"
            _badge.TextAlign = HtmlColorLabel.TextAlignEnum.Center
            _badge.ForeColor = Color.White
            _badge.Font = New Font("Microsoft YaHei UI", 10.0F, FontStyle.Bold)
            _badge.BackColor1 = Color.FromArgb(38, 43, 50)
            _badge.BorderRadius = 5
            _badge.BorderSize = 0
            _hint.Text = "拖入或浏览"
            _hint.TextAlign = HtmlColorLabel.TextAlignEnum.Center
            _hint.ForeColor = Color.FromArgb(200, 207, 216)
            _hint.Font = New Font("Microsoft YaHei UI", 18.0F, FontStyle.Regular)
            _hint.BackColor1 = Color.Transparent
            _hint.BorderSize = 0
            _fileName.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _fileName.ForeColor = Color.FromArgb(235, 239, 244)
            _fileName.Font = New Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular)
            _fileName.BackColor1 = Color.Transparent
            _fileName.BorderSize = 0
            _fileName.Visible = False
            For Each child As Control In New Control() {_badge, _hint, _fileName}
                child.Cursor = Cursors.Hand
                child.AllowDrop = True
                AddHandler child.Click, Sub(sender As Object, e As EventArgs) OnClick(e)
                AddHandler child.DragEnter, Sub(sender As Object, e As DragEventArgs) OnDragEnter(e)
                AddHandler child.DragDrop, Sub(sender As Object, e As DragEventArgs) OnDragDrop(e)
                Controls.Add(child)
            Next
        End Sub

        Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
            MyBase.OnLayout(levent)
            If Width <= 0 OrElse Height <= 0 Then Return
            Dim pad = Math.Max(6, Width \ 45)
            Dim imageHeight = Math.Max(28, CInt(Height * 0.62))
            Dim badgeSize = Math.Max(24, Math.Min(32, Height \ 4))
            _badge.Bounds = New Rectangle(pad + 4, pad + 4, badgeSize, badgeSize)
            _hint.Bounds = New Rectangle(pad, imageHeight, Math.Max(1, Width - pad * 2), Math.Max(1, Height - imageHeight - pad))
            _fileName.Bounds = New Rectangle(pad + 2, imageHeight, Math.Max(1, Width - pad * 2 - 4), Math.Max(1, Height - imageHeight - pad))
            _badge.BringToFront()
            If _hint.Visible Then _hint.BringToFront() Else _fileName.BringToFront()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)
            If Width <= 1 OrElse Height <= 1 Then Return
            Dim g = e.Graphics
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.InterpolationMode = InterpolationMode.HighQualityBicubic
            Dim outer = New RectangleF(0.5F, 0.5F, Math.Max(1, Width - 1.0F), Math.Max(1, Height - 1.0F))
            Using path = QuadGridDrawing.RoundedPath(outer, 9)
                Using brush As New LinearGradientBrush(ClientRectangle, Color.FromArgb(32, 37, 44), Color.FromArgb(25, 29, 35), 90.0F)
                    g.FillPath(brush, path)
                End Using
                Using pen As New Pen(If(String.IsNullOrWhiteSpace(_filePath), Color.FromArgb(67, 75, 85), Color.FromArgb(31, 177, 112)), If(String.IsNullOrWhiteSpace(_filePath), 1.0F, 1.5F))
                    g.DrawPath(pen, path)
                End Using
            End Using

            Dim pad = Math.Max(7, Width \ 45)
            Dim imageHeight = Math.Max(34, CInt(Height * 0.62))
            Dim imageRect = New Rectangle(pad, pad, Math.Max(1, Width - pad * 2), Math.Max(1, imageHeight - pad))
            If _previewImage IsNot Nothing Then
                Dim state = g.Save()
                Using clip = QuadGridDrawing.RoundedPath(imageRect, 6)
                    g.SetClip(clip)
                    QuadGridDrawing.DrawImageCover(g, _previewImage, imageRect)
                End Using
                g.Restore(state)
            Else
                Using brush As New SolidBrush(Color.FromArgb(22, 26, 31))
                    Using clip = QuadGridDrawing.RoundedPath(imageRect, 6)
                        g.FillPath(brush, clip)
                    End Using
                End Using
            End If

        End Sub

        Protected Overrides Sub OnCreateControl()
            MyBase.OnCreateControl()
            _badge.Text = (SlotIndex + 1).ToString()
        End Sub
    End Class

    ''' <summary>双缓冲自绘时间轴。拖动只更新视觉位置，Seek 由宿主在 MouseUp 时统一提交。</summary>
    Friend Class SmoothTimeline
        Inherits Control

        Private _minimum As Integer
        Private _maximum As Integer = 1
        Private _value As Integer
        Private _dragging As Boolean

        Friend Event ValueChanged(sender As Object, e As EventArgs)
        Friend Property Minimum As Integer
            Get
                Return _minimum
            End Get
            Set(value As Integer)
                _minimum = value
                If _maximum < _minimum Then _maximum = _minimum
                Value = _value
            End Set
        End Property
        Friend Property Maximum As Integer
            Get
                Return _maximum
            End Get
            Set(value As Integer)
                _maximum = Math.Max(_minimum, value)
                Value = _value
            End Set
        End Property
        Friend Property Value As Integer
            Get
                Return _value
            End Get
            Set(value As Integer)
                Dim nextValue = Math.Min(_maximum, Math.Max(_minimum, value))
                If nextValue = _value Then Return
                _value = nextValue
                Invalidate()
                RaiseEvent ValueChanged(Me, EventArgs.Empty)
            End Set
        End Property
        Friend ReadOnly Property IsDragging As Boolean
            Get
                Return _dragging
            End Get
        End Property

        Public Sub New()
            Cursor = Cursors.Hand
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw Or
                     ControlStyles.SupportsTransparentBackColor, True)
            BackColor = Color.Transparent
        End Sub

        Private Sub SetValueFromX(x As Integer)
            If Width <= 1 OrElse _maximum <= _minimum Then
                Value = _minimum
                Return
            End If
            Dim ratio = Math.Min(1.0, Math.Max(0.0, x / CDbl(Width - 1)))
            Value = CInt(Math.Round(_minimum + ratio * (_maximum - _minimum)))
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button <> MouseButtons.Left Then Return
            _dragging = True
            Capture = True
            SetValueFromX(e.X)
        End Sub

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            If _dragging Then SetValueFromX(e.X)
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            If _dragging AndAlso e.Button = MouseButtons.Left Then SetValueFromX(e.X)
            _dragging = False
            Capture = False
            MyBase.OnMouseUp(e)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)
            If Width <= 1 OrElse Height <= 1 Then Return
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
            Dim centerY = Height / 2.0F
            Dim track = New RectangleF(1.0F, centerY - 3.0F, Math.Max(1, Width - 2.0F), 6.0F)
            Using path = QuadGridDrawing.RoundedPath(track, 3)
                Using brush As New SolidBrush(Color.FromArgb(64, 72, 82))
                    e.Graphics.FillPath(brush, path)
                End Using
            End Using
            Dim ratio = If(_maximum <= _minimum, 0.0, (_value - _minimum) / CDbl(_maximum - _minimum))
            Dim filledWidth = CSng(Math.Max(0, (Width - 2) * ratio))
            If filledWidth > 0 Then
                Dim filled = New RectangleF(1.0F, centerY - 3.0F, filledWidth, 6.0F)
                Using path = QuadGridDrawing.RoundedPath(filled, 3)
                    Using brush As New LinearGradientBrush(filled, Color.FromArgb(33, 190, 121), Color.FromArgb(39, 145, 235), 0.0F)
                        e.Graphics.FillPath(brush, path)
                    End Using
                End Using
            End If
            Dim handleX = CSng(1 + (Width - 2) * ratio)
            Using shadow As New SolidBrush(Color.FromArgb(80, 0, 0, 0))
                e.Graphics.FillEllipse(shadow, handleX - 8, centerY - 7, 16, 16)
            End Using
            Using brush As New SolidBrush(Color.FromArgb(235, 242, 248))
                e.Graphics.FillEllipse(brush, handleX - 6, centerY - 6, 12, 12)
            End Using
        End Sub
    End Class

End Namespace
