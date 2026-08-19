Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Globalization
Imports System.IO
Imports System.Text
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports LakeUI

Namespace videoenhancer

    ''' <summary>
    ''' 「制作四宫格比对视频」独立二级窗口（不影响 3FUI 主界面）：
    ''' 拖入/浏览 1-4 个视频，选择输出大小、缩放算法、分割线宽度与颜色，
    ''' 预览区实时渲染四宫格布局与分割线效果；输出时生成 xstack 滤镜 ffmpeg 命令并执行。
    ''' 少于 4 个视频时按 1+1+2 / 上下 / 左右 逻辑自动调整布局。
    ''' </summary>
    Friend Class QuadGridForm
        Inherits Form

        Private Enum GridKind
            Grid4
            TwoCol
            TwoRow
            TwoRight
            TwoLeft
            TwoTop
            TwoBottom
        End Enum

        ' ── 控件 ──
        Private ReadOnly _videos(3) As String
        Private ReadOnly _slotLabels(3) As Label
        Private ReadOnly _preview As New PictureBox()
        Private ReadOnly _cmbSize As New ComboBox()
        Private ReadOnly _cmbScale As New ComboBox()
        Private ReadOnly _numLine As New NumericUpDown()
        Private ReadOnly _btnColor As New Button()
        Private ReadOnly _cmbLayout As New ComboBox()
        Private ReadOnly _btnOutput As New Button()
        Private ReadOnly _lblStatus As New Label()
        Private ReadOnly _frameCache As New Dictionary(Of Integer, Image)()
        Private ReadOnly _framePath As New Dictionary(Of Integer, String)()
        Private ReadOnly _config As PluginConfig

        Private _lineColor As Color = Color.White
        Private _ffmpeg As String = ""
        Private _running As Boolean = False
        Private _process As Process

        Public Sub New(config As PluginConfig)
            _config = config
            Text = "制作四宫格比对视频"
            ClientSize = New Size(980, 640)
            MinimumSize = New Size(820, 520)
            StartPosition = FormStartPosition.CenterParent
            BackColor = Color.FromArgb(24, 24, 28)
            ForeColor = Color.FromArgb(220, 220, 220)
            Font = New Font("Microsoft YaHei UI", 9.0F)
            ResolveFfmpeg()
            BuildUi()
        End Sub
        ' ────────────────────────── UI 构建 ──────────────────────────

        Private Sub BuildUi()
            ' 左侧：槽位行 + 预览区
            Dim slotsTable As New TableLayoutPanel()
            slotsTable.Dock = DockStyle.Top
            slotsTable.Height = 104
            slotsTable.ColumnCount = 4
            slotsTable.RowCount = 1
            slotsTable.BackColor = Color.FromArgb(24, 24, 28)
            slotsTable.Padding = New Padding(6, 8, 6, 4)
            For i As Integer = 0 To 3
                slotsTable.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25.0F))
            Next
            slotsTable.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            For i As Integer = 0 To 3
                Dim pnl As New Panel()
                pnl.Margin = New Padding(4)
                pnl.Dock = DockStyle.Fill
                pnl.BackColor = Color.FromArgb(34, 36, 42)
                pnl.BorderStyle = BorderStyle.FixedSingle
                pnl.AllowDrop = True
                pnl.Tag = i
                AddHandler pnl.DragEnter, AddressOf SlotDragEnter
                AddHandler pnl.DragDrop, AddressOf SlotDragDrop
                Dim lbl As New Label()
                lbl.Dock = DockStyle.Fill
                lbl.TextAlign = ContentAlignment.MiddleCenter
                lbl.ForeColor = Color.FromArgb(160, 160, 170)
                lbl.AutoEllipsis = True
                lbl.Text = "视频" & (i + 1).ToString() & Convert.ToChar(10) & "（拖入或浏览）"
                AddHandler lbl.DragEnter, AddressOf SlotDragEnter
                AddHandler lbl.DragDrop, AddressOf SlotDragDrop
                lbl.Tag = i
                Dim btn As New Button()
                btn.Dock = DockStyle.Bottom
                btn.Height = 26
                btn.Text = "浏览…"
                btn.FlatStyle = FlatStyle.Flat
                btn.FlatAppearance.BorderSize = 0
                btn.BackColor = Color.FromArgb(52, 56, 64)
                btn.ForeColor = Color.FromArgb(200, 200, 210)
                btn.Tag = i
                AddHandler btn.Click, AddressOf SlotBrowseClick
                pnl.Controls.Add(btn)
                pnl.Controls.Add(lbl)
                slotsTable.Controls.Add(pnl, i, 0)
                _slotLabels(i) = lbl
            Next

            _preview.Dock = DockStyle.Fill
            _preview.BackColor = Color.FromArgb(16, 16, 18)
            _preview.AllowDrop = True
            AddHandler _preview.Paint, AddressOf PreviewPaint
            AddHandler _preview.DragEnter, AddressOf SlotDragEnter
            AddHandler _preview.DragDrop, AddressOf PreviewDragDrop

            Dim leftArea As New Panel()
            leftArea.Dock = DockStyle.Fill
            leftArea.BackColor = Color.FromArgb(24, 24, 28)
            leftArea.Controls.Add(_preview)
            leftArea.Controls.Add(slotsTable)

            ' 右侧：输出选项
            Dim rightPanel As New Panel()
            rightPanel.Dock = DockStyle.Right
            rightPanel.Width = 260
            rightPanel.BackColor = Color.FromArgb(30, 30, 36)
            rightPanel.Padding = New Padding(14, 12, 14, 10)

            Dim y As Integer = 10
            y = AddLabel(rightPanel, "输出选项", y, 18, True, Color.FromArgb(235, 235, 235))
            y = AddLabel(rightPanel, "输出大小", y, 20, False, Color.FromArgb(170, 170, 180))
            _cmbSize.DropDownStyle = ComboBoxStyle.DropDownList
            _cmbSize.Items.AddRange(New Object() {"3840x2160", "2560x1440", "1920x1080", "1280x720", "960x540"})
            _cmbSize.SelectedIndex = 0
            y = AddControl(rightPanel, _cmbSize, y, 28)
            AddHandler _cmbSize.SelectedIndexChanged, AddressOf OptionsChanged

            y = AddLabel(rightPanel, "缩放算法", y + 6, 20, False, Color.FromArgb(170, 170, 180))
            _cmbScale.DropDownStyle = ComboBoxStyle.DropDownList
            _cmbScale.Items.AddRange(New Object() {"lanczos", "bicubic", "bilinear", "spline"})
            _cmbScale.SelectedIndex = 0
            y = AddControl(rightPanel, _cmbScale, y, 28)
            AddHandler _cmbScale.SelectedIndexChanged, AddressOf OptionsChanged

            y = AddLabel(rightPanel, "分割线宽度（像素）", y + 6, 20, False, Color.FromArgb(170, 170, 180))
            _numLine.Minimum = 1
            _numLine.Maximum = 32
            _numLine.Value = 4
            _numLine.BackColor = Color.FromArgb(45, 48, 56)
            _numLine.ForeColor = Color.FromArgb(220, 220, 220)
            _numLine.BorderStyle = BorderStyle.FixedSingle
            y = AddControl(rightPanel, _numLine, y, 26)
            AddHandler _numLine.ValueChanged, AddressOf OptionsChanged

            y = AddLabel(rightPanel, "分割线颜色", y + 6, 20, False, Color.FromArgb(170, 170, 180))
            _btnColor.FlatStyle = FlatStyle.Flat
            _btnColor.FlatAppearance.BorderSize = 0
            _btnColor.Height = 30
            _btnColor.Width = 120
            _btnColor.TextAlign = ContentAlignment.MiddleCenter
            _btnColor.BackColor = Color.White
            _btnColor.ForeColor = Color.FromArgb(30, 30, 30)
            AddHandler _btnColor.Click, AddressOf ColorClick
            y = AddControl(rightPanel, _btnColor, y, 30)

            y = AddLabel(rightPanel, "排版方式", y + 6, 20, False, Color.FromArgb(170, 170, 180))
            _cmbLayout.DropDownStyle = ComboBoxStyle.DropDownList
            y = AddControl(rightPanel, _cmbLayout, y, 30)
            AddHandler _cmbLayout.SelectedIndexChanged, AddressOf OptionsChanged

            _btnOutput.Text = "输出"
            _btnOutput.FlatStyle = FlatStyle.Flat
            _btnOutput.FlatAppearance.BorderSize = 0
            _btnOutput.BackColor = Color.FromArgb(64, 140, 96)
            _btnOutput.ForeColor = Color.White
            _btnOutput.Height = 40
            _btnOutput.Width = 200
            _btnOutput.Left = 14
            _btnOutput.Top = rightPanel.Height - 60
            _btnOutput.Anchor = AnchorStyles.Left Or AnchorStyles.Bottom
            AddHandler _btnOutput.Click, AddressOf OutputClick
            rightPanel.Controls.Add(_btnOutput)

            Dim lblHint As New Label()
            lblHint.Text = "至少导入 2 个视频；" & Convert.ToChar(10) & "1 个视频无法输出。" & Convert.ToChar(10) & Convert.ToChar(10) & "2 个：上下/左右排版" & Convert.ToChar(10) & "3 个：1+1+2 布局（可选 2 个视频所在的一侧）" & Convert.ToChar(10) & "4 个：2×2 四宫格"
            lblHint.AutoSize = False
            lblHint.Width = 232
            lblHint.Height = 110
            lblHint.Left = 14
            lblHint.Top = 300
            lblHint.ForeColor = Color.FromArgb(130, 130, 140)
            rightPanel.Controls.Add(lblHint)

            _lblStatus.Dock = DockStyle.Bottom
            _lblStatus.Height = 30
            _lblStatus.ForeColor = Color.FromArgb(150, 200, 160)
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft
            _lblStatus.Text = "就绪"

            Controls.Add(leftArea)
            Controls.Add(rightPanel)
            Controls.Add(_lblStatus)

            UpdateLayoutCombo()
            UpdateColorButton()
        End Sub

        Private Function AddLabel(parent As Control, text As String, y As Integer, height As Integer, bold As Boolean, color As Color) As Integer
            Dim lbl As New Label()
            lbl.Text = text
            lbl.AutoSize = False
            lbl.Width = 230
            lbl.Height = height
            lbl.Left = 0
            lbl.Top = y
            lbl.ForeColor = color
            If bold Then
                lbl.Font = New Font(Font.FontFamily, 11.0F, FontStyle.Bold)
            End If
            parent.Controls.Add(lbl)
            Return y + height
        End Function

        Private Function AddControl(parent As Control, c As Control, y As Integer, height As Integer) As Integer
            c.Left = 0
            c.Top = y
            c.Width = 232
            c.Height = height
            parent.Controls.Add(c)
            Return y + height
        End Function
        ' ────────────────────────── 拖放 / 浏览 ──────────────────────────

        Private Shared Function IsVideoFile(path As String) As Boolean
            Dim ext = System.IO.Path.GetExtension(path).ToLowerInvariant()
            Select Case ext
                Case ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".flv", ".ts", ".m2ts", ".webm", ".mpg", ".mpeg", ".m4v", ".3gp", ".vob"
                    Return True
                Case Else
                    Return False
            End Select
        End Function

        Private Sub SlotDragEnter(sender As Object, e As DragEventArgs)
            If e.Data.GetDataPresent(DataFormats.FileDrop) Then
                e.Effect = DragDropEffects.Copy
            Else
                e.Effect = DragDropEffects.None
            End If
        End Sub

        Private Sub SlotDragDrop(sender As Object, e As DragEventArgs)
            Dim files = TryCast(e.Data.GetData(DataFormats.FileDrop), String())
            If files Is Nothing OrElse files.Length = 0 Then
                Return
            End If
            Dim target = -1
            Dim ctl = TryCast(sender, Control)
            If ctl IsNot Nothing AndAlso ctl.Tag IsNot Nothing Then
                Integer.TryParse(ctl.Tag.ToString(), target)
            End If
            If target < 0 OrElse target > 3 Then
                target = FirstEmptySlot()
            End If
            If target < 0 Then
                SetStatusText("四个槽位已满", True)
                Return
            End If
            SetVideo(target, files(0))
        End Sub

        Private Sub PreviewDragDrop(sender As Object, e As DragEventArgs)
            Dim files = TryCast(e.Data.GetData(DataFormats.FileDrop), String())
            If files Is Nothing OrElse files.Length = 0 Then
                Return
            End If
            Dim target = FirstEmptySlot()
            If target < 0 Then
                SetStatusText("四个槽位已满", True)
                Return
            End If
            SetVideo(target, files(0))
        End Sub

        Private Sub SlotBrowseClick(sender As Object, e As EventArgs)
            Dim idx = -1
            Dim ctl = TryCast(sender, Control)
            If ctl IsNot Nothing AndAlso ctl.Tag IsNot Nothing Then
                Integer.TryParse(ctl.Tag.ToString(), idx)
            End If
            If idx < 0 OrElse idx > 3 Then
                Return
            End If
            Using dlg As New OpenFileDialog()
                dlg.Title = "选择视频 " & (idx + 1).ToString()
                dlg.Filter = "视频文件|*.mp4;*.mkv;*.mov;*.avi;*.wmv;*.flv;*.ts;*.m2ts;*.webm;*.mpg;*.mpeg;*.m4v;*.3gp|所有文件|*.*"
                dlg.CheckFileExists = True
                If dlg.ShowDialog(Me) = DialogResult.OK Then
                    SetVideo(idx, dlg.FileName)
                End If
            End Using
        End Sub

        Private Function FirstEmptySlot() As Integer
            For i As Integer = 0 To 3
                If String.IsNullOrWhiteSpace(_videos(i)) Then
                    Return i
                End If
            Next
            Return -1
        End Function

        Private Sub SetVideo(idx As Integer, path As String)
            If String.IsNullOrWhiteSpace(path) OrElse Not File.Exists(path) Then
                SetStatusText("文件不存在：" & If(path, ""), True)
                Return
            End If
            If Not IsVideoFile(path) Then
                SetStatusText("不是支持的视频格式：" & System.IO.Path.GetFileName(path), True)
                Return
            End If
            _videos(idx) = path
            Dim name = System.IO.Path.GetFileName(path)
            If name.Length > 26 Then
                name = name.Substring(0, 24) & "…"
            End If
            _slotLabels(idx).Text = "视频" & (idx + 1).ToString() & Convert.ToChar(10) & name
            _slotLabels(idx).ForeColor = Color.FromArgb(220, 220, 220)
            Dim old As Image = Nothing
            If _frameCache.TryGetValue(idx, old) Then
                Try : old.Dispose() : Catch : End Try
                _frameCache.Remove(idx)
            End If
            UpdateLayoutCombo()
            _preview.Invalidate()
            LoadFirstFrameAsync(idx, path)
        End Sub

        ' ────────────────────────── 首帧预览 ──────────────────────────

        Private Sub LoadFirstFrameAsync(idx As Integer, path As String)
            Try
                System.Threading.Tasks.Task.Run(New Action(Sub()
                    Dim img = ExtractFirstFrame(path)
                    If img IsNot Nothing Then
                        SyncLock _frameCache
                            Dim old As Image = Nothing
                            If _frameCache.TryGetValue(idx, old) Then
                                Try : old.Dispose() : Catch : End Try
                            End If
                            _frameCache(idx) = img
                            _framePath(idx) = path
                        End SyncLock
                        Try
                            If _preview.IsHandleCreated Then
                                _preview.BeginInvoke(New Action(Sub() _preview.Invalidate()))
                            End If
                        Catch
                        End Try
                    End If
                End Sub))
            Catch
            End Try
        End Sub

        Private Function ExtractFirstFrame(path As String) As Image
            If _ffmpeg = "" Then
                Return Nothing
            End If
            Dim positions As Double() = {1.0, 0.0}
            For Each startPos In positions
                Try
                    Dim psi As New ProcessStartInfo()
                    psi.FileName = _ffmpeg
                    psi.UseShellExecute = False
                    psi.CreateNoWindow = True
                    psi.RedirectStandardOutput = True
                    psi.ArgumentList.Add("-v")
                    psi.ArgumentList.Add("error")
                    psi.ArgumentList.Add("-nostdin")
                    psi.ArgumentList.Add("-ss")
                    psi.ArgumentList.Add(startPos.ToString("0.###", CultureInfo.InvariantCulture))
                    psi.ArgumentList.Add("-i")
                    psi.ArgumentList.Add(path)
                    psi.ArgumentList.Add("-frames:v")
                    psi.ArgumentList.Add("1")
                    psi.ArgumentList.Add("-f")
                    psi.ArgumentList.Add("image2pipe")
                    psi.ArgumentList.Add("-c:v")
                    psi.ArgumentList.Add("png")
                    psi.ArgumentList.Add("-")
                    Using p As New Process()
                        p.StartInfo = psi
                        If Not p.Start() Then
                            Continue For
                        End If
                        Dim ms As New MemoryStream()
                        Try
                            Dim copy = p.StandardOutput.BaseStream.CopyToAsync(ms)
                            Dim exited = p.WaitForExit(10000)
                            If Not exited Then
                                Try : p.Kill() : Catch : End Try
                                Try : copy.Wait(1500) : Catch : End Try
                                Continue For
                            End If
                            Try : copy.Wait(1500) : Catch : End Try
                            If ms.Length <= 0 Then
                                Continue For
                            End If
                            ms.Position = 0
                            Using src = Image.FromStream(ms)
                                Return New Bitmap(src)
                            End Using
                        Finally
                            ms.Dispose()
                        End Try
                    End Using
                Catch
                    Continue For
                End Try
            Next
            Return Nothing
        End Function
        ' ────────────────────────── 预览绘制 ──────────────────────────

        Private Sub OptionsChanged(sender As Object, e As EventArgs)
            _preview.Invalidate()
        End Sub

        Private Sub PreviewPaint(sender As Object, e As PaintEventArgs)
            Dim g = e.Graphics
            g.Clear(Color.FromArgb(16, 16, 18))
            Dim inputs = CollectVideos()
            If inputs.Count = 0 Then
                DrawCenteredText(g, "拖入或浏览 1-4 个视频，实时预览四宫格布局", Color.FromArgb(110, 110, 120), 13)
                Return
            End If
            Dim w As Integer = 0
            Dim h As Integer = 0
            ParseSize(w, h)
            If w <= 0 OrElse h <= 0 Then
                Return
            End If
            Dim kind = CurrentKind()
            Dim rects = LayoutRects(kind, w, h)
            Dim pw = _preview.ClientSize.Width
            Dim ph = _preview.ClientSize.Height
            If pw <= 0 OrElse ph <= 0 Then
                Return
            End If
            Dim scale = Math.Min(pw / CDbl(w), ph / CDbl(h))
            Dim ox = (pw - w * scale) / 2.0
            Dim oy = (ph - h * scale) / 2.0

            For i As Integer = 0 To rects.Count - 1
                Dim r = rects(i)
                Dim sx = ox + r.X * scale
                Dim sy = oy + r.Y * scale
                Dim sw = r.Width * scale
                Dim sh = r.Height * scale
                Using brush = New SolidBrush(If(i Mod 2 = 0, Color.FromArgb(30, 32, 38), Color.FromArgb(24, 26, 32)))
                    g.FillRectangle(brush, CSng(sx), CSng(sy), CSng(sw), CSng(sh))
                End Using
                Dim frame As Image = Nothing
                If i < inputs.Count AndAlso _frameCache.TryGetValue(i, frame) AndAlso frame IsNot Nothing Then
                    If String.Equals(_framePath(i), inputs(i), StringComparison.OrdinalIgnoreCase) Then
                        Try
                            g.DrawImage(frame, CSng(sx), CSng(sy), CSng(sw), CSng(sh))
                        Catch
                        End Try
                    End If
                End If
                If i < inputs.Count Then
                    DrawCenteredText(g, System.IO.Path.GetFileName(inputs(i)), Color.FromArgb(235, 235, 235), Math.Max(9, CInt(13 * scale + 2)), CSng(sx), CSng(sy), CSng(sw), CSng(sh))
                End If
            Next

            ' 分割线：实时渲染，宽度随预览缩放等比缩放
            Dim dividerRects = LineRects(kind, w, h, CInt(_numLine.Value))
            Using brush = New SolidBrush(_lineColor)
                For Each r In dividerRects
                    g.FillRectangle(brush, CSng(ox + r.X * scale), CSng(oy + r.Y * scale), CSng(r.Width * scale), CSng(r.Height * scale))
                Next
            End Using
            Using pen = New Pen(Color.FromArgb(70, 70, 80))
                g.DrawRectangle(pen, CSng(ox), CSng(oy), CSng(w * scale), CSng(h * scale))
            End Using
        End Sub

        Private Shared Sub DrawCenteredText(g As Graphics, text As String, color As Color, size As Integer, Optional x As Single = 0, Optional y As Single = 0, Optional w As Single = 0, Optional h As Single = 0)
            If String.IsNullOrWhiteSpace(text) Then
                Return
            End If
            Using font = New Font("Microsoft YaHei UI", size, FontStyle.Regular, GraphicsUnit.Pixel)
                Using brush = New SolidBrush(color)
                    Dim format As New StringFormat()
                    format.Alignment = StringAlignment.Center
                    format.LineAlignment = StringAlignment.Center
                    format.Trimming = StringTrimming.EllipsisCharacter
                    If w > 0 Then
                        Dim rect As New RectangleF(x, y, w, h)
                        g.DrawString(text, font, brush, rect, format)
                    Else
                        Dim sizeF = g.MeasureString(text, font)
                        Dim cx = (g.ClipBounds.Width - sizeF.Width) / 2.0F
                        Dim cy = (g.ClipBounds.Height - sizeF.Height) / 2.0F
                        g.DrawString(text, font, brush, cx, cy, format)
                    End If
                    format.Dispose()
                End Using
            End Using
        End Sub

        ' ────────────────────────── 布局计算 ──────────────────────────

        Private Function CollectVideos() As List(Of String)
            Dim result As New List(Of String)()
            For i As Integer = 0 To 3
                If Not String.IsNullOrWhiteSpace(_videos(i)) Then
                    result.Add(_videos(i))
                End If
            Next
            Return result
        End Function

        Private Sub ParseSize(ByRef w As Integer, ByRef h As Integer)
            Dim text = If(_cmbSize.SelectedItem, "").ToString()
            Dim idx = text.IndexOf("x"c)
            If idx <= 0 Then
                w = 3840
                h = 2160
                Return
            End If
            If Not Integer.TryParse(text.Substring(0, idx), w) Then
                w = 3840
            End If
            If Not Integer.TryParse(text.Substring(idx + 1), h) Then
                h = 2160
            End If
            If w Mod 2 <> 0 Then
                w -= 1
            End If
            If h Mod 2 <> 0 Then
                h -= 1
            End If
            w = Math.Max(320, w)
            h = Math.Max(180, h)
        End Sub

        Private Sub UpdateLayoutCombo()
            Dim inputs = CollectVideos()
            _cmbLayout.Items.Clear()
            Select Case inputs.Count
                Case 0, 1
                    _cmbLayout.Enabled = False
                    _cmbLayout.Items.Add("至少导入 2 个视频")
                    _cmbLayout.SelectedIndex = 0
                Case 2
                    _cmbLayout.Enabled = True
                    _cmbLayout.Items.Add("左右排版")
                    _cmbLayout.Items.Add("上下排版")
                    _cmbLayout.SelectedIndex = 0
                Case 3
                    _cmbLayout.Enabled = True
                    _cmbLayout.Items.Add("2 个在右侧")
                    _cmbLayout.Items.Add("2 个在左侧")
                    _cmbLayout.Items.Add("2 个在上方")
                    _cmbLayout.Items.Add("2 个在下方")
                    _cmbLayout.SelectedIndex = 0
                Case Else
                    _cmbLayout.Enabled = False
                    _cmbLayout.Items.Add("2×2 四宫格")
                    _cmbLayout.SelectedIndex = 0
            End Select
        End Sub

        Private Function CurrentKind() As GridKind
            Dim inputs = CollectVideos()
            Select Case inputs.Count
                Case 2
                    If _cmbLayout.SelectedIndex = 1 Then
                        Return GridKind.TwoRow
                    End If
                    Return GridKind.TwoCol
                Case 3
                    Select Case _cmbLayout.SelectedIndex
                        Case 1 : Return GridKind.TwoLeft
                        Case 2 : Return GridKind.TwoTop
                        Case 3 : Return GridKind.TwoBottom
                        Case Else : Return GridKind.TwoRight
                    End Select
                Case Else
                    Return GridKind.Grid4
            End Select
        End Function

        Private Shared Function LayoutRects(kind As GridKind, w As Integer, h As Integer) As List(Of Rectangle)
            Dim hw = w \ 2
            Dim hh = h \ 2
            Dim result As New List(Of Rectangle)()
            Select Case kind
                Case GridKind.TwoCol
                    result.Add(New Rectangle(0, 0, hw, h))
                    result.Add(New Rectangle(hw, 0, hw, h))
                Case GridKind.TwoRow
                    result.Add(New Rectangle(0, 0, w, hh))
                    result.Add(New Rectangle(0, hh, w, hh))
                Case GridKind.TwoRight
                    result.Add(New Rectangle(0, 0, hw, h))
                    result.Add(New Rectangle(hw, 0, hw, hh))
                    result.Add(New Rectangle(hw, hh, hw, hh))
                Case GridKind.TwoLeft
                    result.Add(New Rectangle(0, 0, hw, hh))
                    result.Add(New Rectangle(0, hh, hw, hh))
                    result.Add(New Rectangle(hw, 0, hw, h))
                Case GridKind.TwoTop
                    result.Add(New Rectangle(0, 0, hw, hh))
                    result.Add(New Rectangle(hw, 0, hw, hh))
                    result.Add(New Rectangle(0, hh, w, hh))
                Case GridKind.TwoBottom
                    result.Add(New Rectangle(0, 0, w, hh))
                    result.Add(New Rectangle(0, hh, hw, hh))
                    result.Add(New Rectangle(hw, hh, hw, hh))
                Case Else
                    result.Add(New Rectangle(0, 0, hw, hh))
                    result.Add(New Rectangle(hw, 0, hw, hh))
                    result.Add(New Rectangle(0, hh, hw, hh))
                    result.Add(New Rectangle(hw, hh, hw, hh))
            End Select
            Return result
        End Function

        Private Shared Function XstackLayout(kind As GridKind) As String
            Select Case kind
                Case GridKind.TwoCol : Return "0_0|w0_0"
                Case GridKind.TwoRow : Return "0_0|0_h0"
                Case GridKind.TwoRight : Return "0_0|w0_0|w0_h0"
                Case GridKind.TwoLeft : Return "0_0|0_h0|w0_0"
                Case GridKind.TwoTop : Return "0_0|w0_0|0_h0"
                Case GridKind.TwoBottom : Return "0_0|0_h0|w0_h0"
                Case Else : Return "0_0|w0_0|0_h0|w0_h0"
            End Select
        End Function

        Private Shared Function LineRects(kind As GridKind, w As Integer, h As Integer, lw As Integer) As List(Of Rectangle)
            Dim hw = w \ 2
            Dim hh = h \ 2
            Dim half = lw \ 2
            Dim result As New List(Of Rectangle)()
            Select Case kind
                Case GridKind.TwoCol
                    result.Add(New Rectangle(hw - half, 0, lw, h))
                Case GridKind.TwoRow
                    result.Add(New Rectangle(0, hh - half, w, lw))
                Case GridKind.TwoRight
                    result.Add(New Rectangle(hw - half, 0, lw, h))
                    result.Add(New Rectangle(hw, hh - half, hw, lw))
                Case GridKind.TwoLeft
                    result.Add(New Rectangle(hw - half, 0, lw, h))
                    result.Add(New Rectangle(0, hh - half, hw, lw))
                Case GridKind.TwoTop
                    result.Add(New Rectangle(0, hh - half, w, lw))
                    result.Add(New Rectangle(hw - half, hh, lw, hh))
                Case GridKind.TwoBottom
                    result.Add(New Rectangle(0, hh - half, w, lw))
                    result.Add(New Rectangle(hw - half, 0, lw, hh))
                Case Else
                    result.Add(New Rectangle(hw - half, 0, lw, h))
                    result.Add(New Rectangle(0, hh - half, w, lw))
            End Select
            Return result
        End Function
        ' ────────────────────────── 颜色 ──────────────────────────

        Private Sub ColorClick(sender As Object, e As EventArgs)
            Try
                Dim dlg As New ModernColorDialog()
                dlg.SelectedColor = _lineColor
                If dlg.ShowDialog(Me) = DialogResult.OK Then
                    _lineColor = dlg.SelectedColor
                    UpdateColorButton()
                    _preview.Invalidate()
                End If
            Catch
                Using dlg As New ColorDialog()
                    dlg.Color = _lineColor
                    If dlg.ShowDialog(Me) = DialogResult.OK Then
                        _lineColor = dlg.Color
                        UpdateColorButton()
                        _preview.Invalidate()
                    End If
                End Using
            End Try
        End Sub

        Private Sub UpdateColorButton()
            _btnColor.BackColor = _lineColor
            _btnColor.Text = "#" & _lineColor.R.ToString("X2") & _lineColor.G.ToString("X2") & _lineColor.B.ToString("X2")
        End Sub

        ' ────────────────────────── 输出 ──────────────────────────

        Private Sub OutputClick(sender As Object, e As EventArgs)
            If _running Then
                Return
            End If
            Dim inputs = CollectVideos()
            If inputs.Count < 2 Then
                MessageBox.Show(Me, "至少需要导入 2 个视频。", "四宫格比对", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If
            Using dlg As New SaveFileDialog()
                dlg.Title = "输出四宫格比对视频"
                dlg.Filter = "MP4 视频 (*.mp4)|*.mp4|MKV 视频 (*.mkv)|*.mkv|所有文件 (*.*)|*.*"
                dlg.FileName = "四宫格比对_" & DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) & ".mp4"
                If dlg.ShowDialog(Me) <> DialogResult.OK Then
                    Return
                End If
                StartEncode(inputs, dlg.FileName)
            End Using
        End Sub

        Private Sub StartEncode(inputs As List(Of String), outputPath As String)
            If _ffmpeg = "" Then
                SetStatusText("未找到 ffmpeg（请先启用 videoenhancer.exe 并配置 core-path）", True)
                Return
            End If
            Dim w As Integer = 0
            Dim h As Integer = 0
            ParseSize(w, h)
            Dim kind = CurrentKind()
            Dim lw = CInt(_numLine.Value)
            Dim algo = If(_cmbScale.SelectedItem, "lanczos").ToString()

            Dim assPath = ""
            Try
                assPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(outputPath), System.IO.Path.GetFileNameWithoutExtension(outputPath) & "_labels.ass")
                System.IO.File.WriteAllText(assPath, BuildAss(inputs, kind, w, h), New UTF8Encoding(False))
            Catch
                assPath = ""
            End Try
            Dim filter = BuildFilter(inputs, kind, w, h, algo, lw, assPath)

            Dim psi As New ProcessStartInfo()
            psi.FileName = _ffmpeg
            psi.UseShellExecute = False
            psi.CreateNoWindow = True
            psi.RedirectStandardOutput = True
            psi.RedirectStandardError = True
            psi.StandardErrorEncoding = Encoding.UTF8
            For Each input In inputs
                psi.ArgumentList.Add("-i")
                psi.ArgumentList.Add(input)
            Next
            psi.ArgumentList.Add("-filter_complex")
            psi.ArgumentList.Add(filter)
            psi.ArgumentList.Add("-map")
            psi.ArgumentList.Add("[final]")
            psi.ArgumentList.Add("-map")
            psi.ArgumentList.Add("0:a?")
            psi.ArgumentList.Add("-c:v")
            psi.ArgumentList.Add("av1_nvenc")
            psi.ArgumentList.Add("-preset")
            psi.ArgumentList.Add("p1")
            psi.ArgumentList.Add("-cq")
            psi.ArgumentList.Add("28")
            psi.ArgumentList.Add("-b:v")
            psi.ArgumentList.Add("0")
            psi.ArgumentList.Add("-pix_fmt")
            psi.ArgumentList.Add("yuv420p10le")
            psi.ArgumentList.Add("-c:a")
            psi.ArgumentList.Add("copy")
            psi.ArgumentList.Add(outputPath)

            _running = True
            _btnOutput.Text = "编码中…"
            _btnOutput.Enabled = False
            SetStatusText("正在编码：xstack 四宫格 → " & System.IO.Path.GetFileName(outputPath), False)
            Try
                _process = New Process()
                _process.StartInfo = psi
                If Not _process.Start() Then
                    _running = False
                    _btnOutput.Enabled = True
                    _btnOutput.Text = "输出"
                    SetStatusText("启动 ffmpeg 失败", True)
                    Return
                End If
                MonitorEncode(_process, outputPath)
            Catch ex As Exception
                _running = False
                _btnOutput.Enabled = True
                _btnOutput.Text = "输出"
                SetStatusText("启动失败：" & ex.Message, True)
            End Try
        End Sub

        Private Sub MonitorEncode(p As Process, outputPath As String)
            Dim captured = p
            Dim capturedOutput = outputPath
            System.Threading.Tasks.Task.Run(New Action(Sub()
                Dim lastLine As String = ""
                Try
                    Dim line As String = Nothing
                    Do
                        line = captured.StandardError.ReadLine()
                        If line Is Nothing Then
                            Exit Do
                        End If
                        If line.Contains("time=") OrElse line.Contains("frame=") OrElse line.Contains("Error") OrElse line.Contains("error") Then
                            lastLine = line
                            UpdateStatusSafe(line)
                        End If
                    Loop
                Catch
                End Try
                Try
                    captured.WaitForExit()
                Catch
                End Try
                Dim code As Integer = -1
                Try
                    code = captured.ExitCode
                Catch
                End Try
                If code = 0 Then
                    UpdateStatusSafe("完成：已输出 " & capturedOutput)
                Else
                    UpdateStatusSafe("失败：ffmpeg 退出码 " & code.ToString() & If(String.IsNullOrWhiteSpace(lastLine), "", "（" & lastLine & "）"), True)
                End If
                Try
                    If _preview.IsHandleCreated Then
                        _preview.BeginInvoke(New Action(Sub()
                            _running = False
                            _process = Nothing
                            _btnOutput.Enabled = True
                            _btnOutput.Text = "输出"
                        End Sub))
                    Else
                        _running = False
                    End If
                Catch
                    _running = False
                End Try
            End Sub))
        End Sub

        Private Sub UpdateStatusSafe(text As String, Optional error_ As Boolean = False)
            Try
                If IsHandleCreated Then
                    BeginInvoke(New Action(Sub() SetStatusText(text, error_)))
                Else
                    SetStatusText(text, error_)
                End If
            Catch
            End Try
        End Sub

        Private Sub SetStatusText(text As String, error_ As Boolean)
            _lblStatus.Text = text
            _lblStatus.ForeColor = If(error_, Color.FromArgb(224, 120, 120), Color.FromArgb(150, 200, 160))
        End Sub
        ' ────────────────────────── ffmpeg 滤镜构建 ──────────────────────────

        Private Function BuildFilter(inputs As List(Of String), kind As GridKind, w As Integer, h As Integer, algo As String, lw As Integer, assPath As String) As String
            Dim sb As New StringBuilder()
            Dim rects = LayoutRects(kind, w, h)
            For i As Integer = 0 To rects.Count - 1
                Dim r = rects(i)
                If i > 0 Then
                    sb.Append(" ")
                End If
                sb.Append("[").Append(i.ToString()).Append(":v] ")
                sb.Append("scale=").Append(w.ToString()).Append(":").Append(h.ToString()).Append(":flags=").Append(algo)
                sb.Append(", crop=").Append(r.Width.ToString()).Append(":").Append(r.Height.ToString())
                sb.Append(":").Append(r.X.ToString()).Append(":").Append(r.Y.ToString())
                sb.Append(", setpts=PTS-STARTPTS [v").Append(i.ToString()).Append("]; ")
            Next
            sb.Append("[v0]")
            For i As Integer = 1 To rects.Count - 1
                sb.Append("[v").Append(i.ToString()).Append("]")
            Next
            sb.Append(" xstack=inputs=").Append(rects.Count.ToString()).Append(":layout=").Append(XstackLayout(kind)).Append(" [out]; ")

            Dim dividerRects = LineRects(kind, w, h, lw)
            Dim colorHex = "0x" & _lineColor.R.ToString("X2") & _lineColor.G.ToString("X2") & _lineColor.B.ToString("X2")
            Dim first As Boolean = True
            For Each r In dividerRects
                If Not first Then
                    sb.Append(", ")
                Else
                    sb.Append("[out] ")
                    first = False
                End If
                sb.Append("drawbox=x=").Append(r.X.ToString()).Append(":y=").Append(r.Y.ToString())
                sb.Append(":w=").Append(r.Width.ToString()).Append(":h=").Append(r.Height.ToString())
                sb.Append(":color=").Append(colorHex)
            Next
            sb.Append(" [lined]; ")

            If Not String.IsNullOrWhiteSpace(assPath) AndAlso File.Exists(assPath) Then
                sb.Append("[lined] subtitles=filename=").Append(EscapeFilterPath(assPath)).Append(" [final]")
            Else
                sb.Append("[lined] null [final]")
            End If
            Return sb.ToString()
        End Function

        Private Shared Function EscapeFilterPath(path As String) As String
            Dim sb As New StringBuilder()
            sb.Append("'")
            For Each c As Char In path
                Select Case c
                    Case "\"c
                        sb.Append("\\")
                    Case ":"c
                        sb.Append("\:")
                    Case "'"c
                        sb.Append("\'")
                    Case Else
                        sb.Append(c)
                End Select
            Next
            sb.Append("'")
            Return sb.ToString()
        End Function

        Private Shared Function BuildAss(inputs As List(Of String), kind As GridKind, w As Integer, h As Integer) As String
            Dim sb As New StringBuilder()
            sb.AppendLine("[Script Info]")
            sb.AppendLine("ScriptType: v4.00+")
            sb.AppendLine("PlayResX: " & w.ToString())
            sb.AppendLine("PlayResY: " & h.ToString())
            sb.AppendLine("ScaledBorderAndShadow: yes")
            sb.AppendLine()
            sb.AppendLine("[V4+ Styles]")
            sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding")
            Dim fontSize = Math.Max(22, w \ 60)
            sb.AppendLine("Style: Default,Microsoft YaHei," & fontSize.ToString() & ",&H00FFFFFF,&H000000FF,&H00101010,&H80000000,-1,0,0,0,100,100,0,0,1,2,1,5,20,20,20,1")
            sb.AppendLine()
            sb.AppendLine("[Events]")
            sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text")
            Dim rects = LayoutRects(kind, w, h)
            For i As Integer = 0 To rects.Count - 1
                Dim r = rects(i)
                Dim cx = r.X + r.Width \ 2
                Dim cy = r.Y + r.Height \ 2
                Dim name = System.IO.Path.GetFileNameWithoutExtension(inputs(i))
                name = SanitizeAssText(name)
                sb.AppendLine("Dialogue: 0,0:00:00:00,99:00:00:00,Default,,0,0,0,,{\an5\pos(" & cx.ToString() & "," & cy.ToString() & ")}" & name)
            Next
            Return sb.ToString()
        End Function

        Private Shared Function SanitizeAssText(text As String) As String
            If String.IsNullOrEmpty(text) Then
                Return text
            End If
            Dim sb As New StringBuilder()
            For Each c As Char In text
                Select Case c
                    Case "{"c, "}"c, "\"c
                        sb.Append(" ")
                    Case ","c
                        sb.Append("，")
                    Case Else
                        sb.Append(c)
                End Select
            Next
            Return sb.ToString()
        End Function

        ' ────────────────────────── ffmpeg 定位 / 关闭 ──────────────────────────

        Private Sub ResolveFfmpeg()
            Try
                Dim exePath = If(_config Is Nothing, "", _config.ExePath)
                Dim exeDir = ""
                If Not String.IsNullOrWhiteSpace(exePath) Then
                    exeDir = System.IO.Path.GetDirectoryName(exePath)
                End If
                If exeDir = "" Then
                    exeDir = Environment.CurrentDirectory
                End If
                Dim core As String = ""
                Try
                    Dim ini = System.IO.Path.Combine(exeDir, "videoenhancer.ini")
                    If File.Exists(ini) Then
                        For Each line In File.ReadAllLines(ini)
                            If line.TrimStart().StartsWith("core-path=", StringComparison.OrdinalIgnoreCase) Then
                                Dim idx = line.IndexOf("="c)
                                If idx >= 0 Then
                                    Dim v = line.Substring(idx + 1).Trim()
                                    v = v.Trim(Convert.ToChar(34))
                                    If v.Length > 0 Then
                                        core = v
                                    End If
                                End If
                            End If
                        Next
                    End If
                Catch
                End Try
                If core = "" Then
                    core = exeDir
                End If
                Dim ff1 = System.IO.Path.Combine(core, "bin", "ffmpeg", "ffmpeg.exe")
                Dim ff2 = System.IO.Path.Combine(core, "bin", "ffmpeg.exe")
                If File.Exists(ff1) Then
                    _ffmpeg = ff1
                ElseIf File.Exists(ff2) Then
                    _ffmpeg = ff2
                ElseIf File.Exists(System.IO.Path.Combine(core, "ffmpeg.exe")) Then
                    _ffmpeg = System.IO.Path.Combine(core, "ffmpeg.exe")
                End If
            Catch
            End Try
        End Sub

        Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
            If _running AndAlso _process IsNot Nothing Then
                Dim answer = MessageBox.Show(Me, "编码正在进行中，关闭窗口将停止编码（已写出的部分可能不完整）。确定关闭？", "制作四宫格比对视频", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                If answer <> DialogResult.Yes Then
                    e.Cancel = True
                    Return
                End If
                Try
                    If _process IsNot Nothing AndAlso Not _process.HasExited Then
                        _process.Kill()
                    End If
                Catch
                End Try
            End If
            For Each img As Image In _frameCache.Values
                Try : img.Dispose() : Catch : End Try
            Next
            _frameCache.Clear()
            MyBase.OnFormClosing(e)
        End Sub

    End Class

End Namespace