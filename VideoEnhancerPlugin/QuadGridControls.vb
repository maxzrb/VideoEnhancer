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

    End Module

    ''' <summary>LakeUI 原生视频槽：图片由 ModernPanel.Image 渲染，子标签只负责交互与文本。</summary>
    Friend Class VideoSlotCard
        Inherits ModernPanel

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
            Image = Nothing
            AccessibleName = If(String.IsNullOrWhiteSpace(_filePath), "空视频槽", System.IO.Path.GetFileName(_filePath))
            _badge.BackColor1 = If(String.IsNullOrWhiteSpace(_filePath),
                                   Color.FromArgb(58, 58, 58),
                                   Color.FromArgb(0, 120, 212))
            _hint.Visible = String.IsNullOrWhiteSpace(_filePath)
            _fileName.Visible = Not _hint.Visible
            _fileName.Text = If(_hint.Visible, "", System.IO.Path.GetFileName(_filePath))
            BorderColor = If(_hint.Visible, Color.FromArgb(68, 68, 68), Color.FromArgb(96, 205, 255))
            Invalidate()
        End Sub

        Friend Sub SetPreviewImage(image As Image)
            Image = image
            _hint.Visible = String.IsNullOrWhiteSpace(_filePath)
            _fileName.Visible = Not _hint.Visible
            Invalidate()
        End Sub

        Public Sub New()
            AllowDrop = True
            Cursor = Cursors.Hand
            BackColor = Color.Transparent
            BackColor1 = Color.FromArgb(34, 34, 38)
            BorderColor = Color.FromArgb(68, 68, 68)
            BorderSize = 1
            BorderRadius = 9
            ' Zoom 保持缩略图比例，避免不同视频尺寸被强行拉伸。
            ImageMode = ModernPanel.ImageFillMode.Zoom

            _badge.Text = "1"
            _badge.TextAlign = HtmlColorLabel.TextAlignEnum.Center
            _badge.ForeColor = Color.White
            _badge.Font = New Font("Microsoft YaHei UI", 10.0F, FontStyle.Bold)
            _badge.BackColor1 = Color.FromArgb(58, 58, 58)
            _badge.BorderRadius = 7
            _badge.BorderSize = 0
            _badge.BackgroundSource = Me

            _hint.Text = "拖放视频或点击选择"
            _hint.TextAlign = HtmlColorLabel.TextAlignEnum.Center
            _hint.ForeColor = Color.FromArgb(205, 205, 205)
            _hint.Font = New Font("Microsoft YaHei UI", 11.0F, FontStyle.Regular)
            _hint.BackColor1 = Color.Transparent
            _hint.BorderSize = 0
            _hint.BackgroundSource = Me

            _fileName.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _fileName.ForeColor = Color.FromArgb(235, 239, 244)
            _fileName.Font = New Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular)
            _fileName.BackColor1 = Color.Transparent
            _fileName.BorderSize = 0
            _fileName.BackgroundSource = Me
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
            ' ModernPanel 的基类构造函数可能在派生字段初始化前触发布局。
            If _badge Is Nothing OrElse _hint Is Nothing OrElse _fileName Is Nothing Then Return
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

        Protected Overrides Sub OnCreateControl()
            MyBase.OnCreateControl()
            _badge.Text = (SlotIndex + 1).ToString()
        End Sub
    End Class

End Namespace
