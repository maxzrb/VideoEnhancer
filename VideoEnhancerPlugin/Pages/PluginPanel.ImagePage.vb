Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports System.Reflection
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports LakeUI

Namespace videoenhancer

    Public Partial Class PluginPanel

        ' ── 图片超分页（独立选项卡，沿用超分工作台的超分引擎与模型）──
        Private ReadOnly _pageImage As New ModernPanel()
        Private _imageRoot As DpiLayoutPanel
        Private ReadOnly _btnImageFiles As New ModernButton()
        Private ReadOnly _btnImageFolder As New ModernButton()
        Private ReadOnly _btnImageOutput As New ModernButton()
        Private ReadOnly _btnImageStart As New ModernButton()
        Private ReadOnly _btnImageClear As New ModernButton()
        Private ReadOnly _switchImageOriginal As New LakeUI.BooleanSwitch()
        Private ReadOnly _switchImagePng As New LakeUI.BooleanSwitch()
        Private ReadOnly _txtImageOutput As New ModernTextBox()
        Private ReadOnly _cmbImageSuffix As New WheelLockedComboBox()
        Private ReadOnly _cmbImageFormat As New WheelLockedComboBox()
        Private ReadOnly _cmbImageOutputScale As New WheelLockedComboBox()
        Private _imageOutputScaleHint As LakeTextLabel
        Private ReadOnly _lblImageInputs As New HtmlColorLabel()
        Private ReadOnly _lblImageOutput As New HtmlColorLabel()
        Private ReadOnly _lblImageProgress As New HtmlColorLabel()
        Private ReadOnly _imageProgress As New ExcellentProgressBar()
        Private ReadOnly _imageFiles As New List(Of String)()
        Private ReadOnly _imageFolders As New List(Of String)()
        Private _imageProcess As Process
        Private _imageRunning As Boolean
        Private _imageCompleteReceived As Boolean
        Private Const ImageContentHeight As Integer = 272
        ' ────────────────────────── 图片超分页 ──────────────────────────

        Private Sub BuildOfficialImagePage()
            _pageImage.Dock = DockStyle.Fill
            _pageImage.LayoutMode = ModernPanel.LayoutModeEnum.Absolute
            Dim root As New DpiLayoutPanel With {
                .Dock = DockStyle.None,
                .Anchor = AnchorStyles.Top Or AnchorStyles.Left,
                .AutoSize = False,
                .MinimumSize = New Size(0, ImageContentHeight),
                .Height = ImageContentHeight,
                .BackColor = Color.Transparent,
                .BackColor1 = Color.Transparent,
                .LayoutMode = ModernPanel.LayoutModeEnum.Absolute,
                .ScrollBarMode = ModernPanel.ScrollMode.None,
                .BorderSize = 0,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            _imageRoot = root
            AddHandler _pageImage.ClientSizeChanged, Sub(sender, e) SyncImageRootBounds()
            AddHandler _pageImage.SizeChanged, Sub(sender, e) SyncImageRootBounds()

            AddWorkbenchRow(root, CreateOfficialSectionHeading(
                "图片超分", "沿用超分工作台的超分引擎与模型，可选择文件、文件夹或直接拖入"), 8, 32)

            Dim imageInputRow As New ModernHorizontalPanel(
                112.0F, CSng(UiColumnGap), 132.0F, CSng(UiColumnGap), 96.0F, CSng(UiColumnGap), -1.0F) With {
                .AllowDrop = True
            }
            ConfigureImageButton(_btnImageFiles, "选择图片", 112)
            ConfigureImageButton(_btnImageFolder, "选择文件夹", 132)
            ConfigureImageButton(_btnImageClear, "移除所有", 96)
            _btnImageFiles.Dock = DockStyle.Fill
            _btnImageFolder.Dock = DockStyle.Fill
            _btnImageFiles.Margin = New Padding(0, 4, 0, 4)
            _btnImageFolder.Margin = New Padding(0, 4, 0, 4)
            _btnImageClear.Dock = DockStyle.Fill
            _btnImageClear.Margin = New Padding(0, 4, 0, 4)
            AddHandler _btnImageFiles.Click, AddressOf OnPickImageFiles
            AddHandler _btnImageFolder.Click, AddressOf OnPickImageFolder
            AddHandler _btnImageClear.Click, AddressOf OnClearImages
            _lblImageInputs.AutoSize = False
            _lblImageInputs.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _lblImageInputs.Text = "<font color=#888888>尚未选择图片</font>"
            imageInputRow.AddColumn(_btnImageFiles, 0)
            imageInputRow.AddColumn(_btnImageFolder, 2)
            imageInputRow.AddColumn(_btnImageClear, 4)
            imageInputRow.AddColumn(CreateOfficialValueBox(_lblImageInputs), 6)
            AddHandler imageInputRow.DragEnter, AddressOf OnImageDragEnter
            AddHandler imageInputRow.DragDrop, AddressOf OnImageDragDrop
            AddWorkbenchRow(root, imageInputRow, 44, UiRowHeight)

            Dim imageOutputRow As New ModernHorizontalPanel(132.0F, CSng(UiColumnGap), -1.0F)
            ConfigureImageButton(_btnImageOutput, "选择输出目录", 132)
            _btnImageOutput.Dock = DockStyle.Fill
            _btnImageOutput.Margin = New Padding(0, 4, 0, 4)
            AddHandler _btnImageOutput.Click, AddressOf OnPickImageOutput
            ConfigureOfficialTextBox(_txtImageOutput, "留空即输出到源目录")
            Dim initialOutput = If(_config.ImageOutputOriginal, "", _config.ImageOutput)
            _txtImageOutput.Text = initialOutput
            _config.ImageOutput = initialOutput
            _config.ImageOutputOriginal = String.IsNullOrWhiteSpace(initialOutput)
            AddHandler _txtImageOutput.TextChanged, AddressOf OnImageOutputTextChanged
            imageOutputRow.AddColumn(_btnImageOutput, 0)
            imageOutputRow.AddColumn(_txtImageOutput, 2)
            AddWorkbenchRow(root, imageOutputRow, 84, UiRowHeight)

            Dim imageOptionsRow As New ModernHorizontalPanel(
                72.0F, 170.0F, 16.0F, 72.0F, 170.0F, -1.0F, 12.0F, 132.0F)

            Dim suffixLabel = CreateOfficialCaption("命名方式")
            suffixLabel.TextAlign = ContentAlignment.MiddleLeft
            _cmbImageSuffix.Items.Add("处理时间戳")
            _cmbImageSuffix.Items.Add("模型名称")
            _cmbImageSuffix.SelectedIndex = If(String.Equals(_config.ImageSuffix, "model", StringComparison.OrdinalIgnoreCase), 1, 0)
            _cmbImageSuffix.WaterText = "选择命名方式…"
            ConfigureCombo(_cmbImageSuffix)
            _cmbImageSuffix.Editable = False
            _cmbImageSuffix.Dock = DockStyle.Fill
            _cmbImageSuffix.Margin = New Padding(0, 4, 0, 4)
            AddHandler _cmbImageSuffix.SelectedIndexChanged, AddressOf OnImageSuffixChanged

            Dim formatLabel = CreateOfficialCaption("输出格式")
            formatLabel.TextAlign = ContentAlignment.MiddleLeft
            _cmbImageFormat.Items.Add("无损 PNG")
            _cmbImageFormat.Items.Add("保留源格式")
            _cmbImageFormat.SelectedIndex = If(_config.ImagePng, 0, 1)
            _cmbImageFormat.WaterText = "选择输出格式…"
            ConfigureCombo(_cmbImageFormat)
            _cmbImageFormat.Editable = False
            _cmbImageFormat.Dock = DockStyle.Fill
            _cmbImageFormat.Margin = New Padding(0, 4, 0, 4)
            AddHandler _cmbImageFormat.SelectedIndexChanged, AddressOf OnImageFormatChanged

            _btnImageStart.Text = "开始增强"
            _btnImageStart.Dock = DockStyle.Fill
            _btnImageStart.Margin = New Padding(0, 4, 0, 4)
            ConfigurePrimaryButton(_btnImageStart)
            AddHandler _btnImageStart.Click, AddressOf OnStartImageProcessing

            imageOptionsRow.AddColumn(suffixLabel, 0)
            imageOptionsRow.AddColumn(_cmbImageSuffix, 1)
            imageOptionsRow.AddColumn(formatLabel, 3)
            imageOptionsRow.AddColumn(_cmbImageFormat, 4)
            imageOptionsRow.AddColumn(_btnImageStart, 7)
            AddWorkbenchRow(root, imageOptionsRow, 128, UiRowHeight)

            ConfigureOutputScaleCombo(_cmbImageOutputScale)
            Dim scaleField = CreateOfficialField("输出倍率", _cmbImageOutputScale)
            _imageOutputScaleHint = CreateOfficialCaption("原生推理倍率", UiTextMuted)
            _imageOutputScaleHint.TextAlign = ContentAlignment.MiddleLeft
            _imageOutputScaleHint.Padding = New Padding(0, UiFieldEditorTop, 0, 5)
            AddWorkbenchControl(root, scaleField, 172, UiFieldHeight, 0.0F, 0.28F, 0, -UiColumnGap)
            AddWorkbenchControl(root, _imageOutputScaleHint, 172, UiFieldHeight, 0.28F, 1.0F)
            SyncOutputScaleControls()

            Dim progressRow As New ModernHorizontalPanel(-1.0F, 16.0F, 300.0F)
            _imageProgress.Minimum = 0
            _imageProgress.Maximum = 1000
            _imageProgress.Dock = DockStyle.Fill
            _imageProgress.Margin = New Padding(0, 10, 0, 10)
            _imageProgress.TrackColor = Color.FromArgb(40, 220, 220, 220)
            _imageProgress.FillColor = UiAccent
            _imageProgress.FillGradientColor = Color.FromArgb(120, 204, 255)
            _imageProgress.FillGradientMode = ExcellentProgressBar.FillGradientModeEnum.WithinProgress
            _imageProgress.BackColor1 = Color.Transparent
            _imageProgress.BorderColor = Color.Transparent
            _imageProgress.BorderSize = 0
            _imageProgress.BorderRadius = 8
            _lblImageProgress.AutoSize = False
            _lblImageProgress.Dock = DockStyle.Fill
            _lblImageProgress.Margin = Padding.Empty
            _lblImageProgress.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            _lblImageProgress.Text = "<font color=#888888>等待开始</font>"
            progressRow.AddColumn(_imageProgress, 0)
            progressRow.AddColumn(_lblImageProgress, 2)
            AddWorkbenchRow(root, progressRow, 232, 28)
            _pageImage.Controls.Add(root)
            BindScrollableGpuBackgroundSources(root, ModernPanel1)
            SyncImageRootBounds()
        End Sub

        Private Sub SyncImageRootBounds()
            Dim root = _imageRoot
            If root Is Nothing OrElse root.IsDisposed OrElse
               _pageImage Is Nothing OrElse _pageImage.IsDisposed Then Return
            Dim width = Math.Max(0, _pageImage.ClientSize.Width - root.ScaleX(_pageImage.ScrollBarWidth + 2))
            Dim contentHeight = root.ScaleY(ImageContentHeight)
            Dim rootLeft = If(_pageImage.HorizontalScrollOffset > 0, root.Left, 0)
            Dim rootTop = If(_pageImage.VerticalScrollOffset > 0, root.Top, 0)
            If root.Left <> rootLeft OrElse root.Top <> rootTop OrElse root.Width <> width OrElse root.Height <> contentHeight Then
                root.SetBounds(rootLeft, rootTop, width, contentHeight)
            End If
        End Sub

        Private Shared Sub ConfigureImageButton(button As ModernButton, text As String, width As Integer)
            button.Text = text
            button.Size = New Size(width, 36)
            ConfigureSecondaryButton(button)
        End Sub

        Private Sub OnPickImageFiles(sender As Object, e As EventArgs)
            Using dialog As New OpenFileDialog With {
                .Title = "选择要超分的图片", .Multiselect = True,
                .Filter = "图片文件|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.tif;*.tiff;*.avif|所有文件|*.*"
            }
                If dialog.ShowDialog() = DialogResult.OK Then AddImagePaths(dialog.FileNames)
            End Using
        End Sub

        Private Sub OnPickImageFolder(sender As Object, e As EventArgs)
            Using dialog As New FolderBrowserDialog With {.Description = "选择图片文件夹（将递归处理子目录）", .ShowNewFolderButton = False}
                If dialog.ShowDialog() = DialogResult.OK Then AddImagePaths(New String() {dialog.SelectedPath})
            End Using
        End Sub

        Private Sub OnPickImageOutput(sender As Object, e As EventArgs)
            Using dialog As New FolderBrowserDialog With {.Description = "选择图片输出文件夹", .ShowNewFolderButton = True}
                Dim currentOutput = _txtImageOutput.Text.Trim()
                If Directory.Exists(currentOutput) Then dialog.SelectedPath = currentOutput
                If dialog.ShowDialog() = DialogResult.OK Then
                    _txtImageOutput.Text = dialog.SelectedPath
                End If
            End Using
        End Sub

        Private Sub OnImageOutputTextChanged(sender As Object, e As EventArgs)
            Dim outputPath = _txtImageOutput.Text.Trim()
            _config.ImageOutput = outputPath
            _config.ImageOutputOriginal = String.IsNullOrWhiteSpace(outputPath)
            _config.Save()
        End Sub

        Private Sub OnImageDragEnter(sender As Object, e As DragEventArgs)
            If e.Data.GetDataPresent(DataFormats.FileDrop) Then e.Effect = DragDropEffects.Copy
        End Sub

        Private Sub OnImageDragDrop(sender As Object, e As DragEventArgs)
            Dim paths = TryCast(e.Data.GetData(DataFormats.FileDrop), String())
            If paths IsNot Nothing Then AddImagePaths(paths)
        End Sub

        Private Sub AddImagePaths(paths As IEnumerable(Of String))
            Dim supported = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tif", ".tiff", ".avif"}
            For Each path In paths
                If Directory.Exists(path) Then
                    If Not _imageFolders.Contains(path, StringComparer.OrdinalIgnoreCase) Then _imageFolders.Add(path)
                ElseIf File.Exists(path) AndAlso supported.Contains(IO.Path.GetExtension(path)) Then
                    If Not _imageFiles.Contains(path, StringComparer.OrdinalIgnoreCase) Then _imageFiles.Add(path)
                End If
            Next
            _lblImageInputs.Text = "<font color=#DCDCDC>已选择 " & _imageFiles.Count & " 个文件、" & _imageFolders.Count & " 个递归文件夹</font>"
        End Sub

        Private Sub OnClearImages(sender As Object, e As EventArgs)
            If _imageRunning Then
                ShowStatus("图片任务运行中，暂不能清空输入", True)
                Return
            End If
            _imageFiles.Clear()
            _imageFolders.Clear()
            _lblImageInputs.Text = "<font color=#888888>尚未选择图片</font>"
        End Sub

        Private Sub OnImageOriginalChanged(sender As Object, e As EventArgs)
            _config.ImageOutputOriginal = _switchImageOriginal.Checked
            _config.Save()
            RefreshImageOutputLabel()
        End Sub

        Private Sub OnImagePngChanged(sender As Object, e As EventArgs)
            _config.ImagePng = _switchImagePng.Checked
            _config.Save()
        End Sub

        Private Sub OnImageSuffixChanged(sender As Object, e As EventArgs)
            _config.ImageSuffix = If(_cmbImageSuffix.SelectedIndex = 1, "model", "timestamp")
            _config.Save()
        End Sub

        Private Sub OnImageFormatChanged(sender As Object, e As EventArgs)
            _config.ImagePng = _cmbImageFormat.SelectedIndex <> 1
            _config.Save()
        End Sub

        Private Sub RefreshImageOutputLabel()
            _btnImageOutput.Enabled = Not _switchImageOriginal.Checked
            Dim text = If(_switchImageOriginal.Checked, "原图片所在目录", If(String.IsNullOrWhiteSpace(_config.ImageOutput), "尚未指定输出文件夹", _config.ImageOutput))
            _lblImageOutput.Text = If(String.IsNullOrWhiteSpace(_config.ImageOutput) AndAlso Not _switchImageOriginal.Checked,
                "<font color=#888888>" & EscapeHtml(text) & "</font>",
                "<font color=#DCDCDC>" & EscapeHtml(text) & "</font>")
        End Sub

        Private Sub OnStartImageProcessing(sender As Object, e As EventArgs)
            If _imageRunning Then Return
            If _config.Backend = "rtxvsr" Then
                ShowStatus("RTX VSR 只处理视频；图片增强请选择其他超分后端。", True)
                Return
            End If
            If _imageFiles.Count = 0 AndAlso _imageFolders.Count = 0 Then
                ShowStatus("请先选择或拖入图片/文件夹", True) : Return
            End If
            If Not File.Exists(_config.ExePath) Then
                ShowStatus("请先指定有效的 videoenhancer.exe", True) : Return
            End If
            If String.IsNullOrWhiteSpace(_config.Model) Then
                ShowStatus("请先在上方选择放大模型", True) : Return
            End If
            Dim outputPath = _txtImageOutput.Text.Trim()
            _config.ImageOutput = outputPath
            _config.ImageOutputOriginal = String.IsNullOrWhiteSpace(outputPath)
            _config.ImageSuffix = If(_cmbImageSuffix.SelectedIndex = 1, "model", "timestamp")
            _config.ImagePng = _cmbImageFormat.SelectedIndex <> 1
            _config.Save()

            Dim args As New List(Of String)()
            For Each path In _imageFiles : args.Add("--image-input") : args.Add(path) : Next
            For Each path In _imageFolders : args.Add("--image-folder") : args.Add(path) : Next
            If String.IsNullOrWhiteSpace(outputPath) Then
                args.Add("--image-output-original")
            Else
                args.Add("--image-output") : args.Add(outputPath)
            End If
            args.Add("--image-suffix") : args.Add(_config.ImageSuffix)
            args.Add(If(_config.ImagePng, "--image-png", "--image-source-format"))
            args.Add("-backend") : args.Add(_config.Backend)
            args.Add("-modelpath") : args.Add(_config.Model)
            If _config.OutputScale > 0 Then
                args.Add("-output-scale") : args.Add(_config.OutputScale.ToString())
            End If
            args.Add("-upscale-precision") : args.Add(If(_config.UpscaleHalfPrecision, "auto", "float32"))

            Dim psi As New ProcessStartInfo With {
                .FileName = _config.ExePath, .WorkingDirectory = Path.GetDirectoryName(_config.ExePath),
                .UseShellExecute = False, .CreateNoWindow = True,
                .RedirectStandardOutput = True, .RedirectStandardError = True,
                .StandardOutputEncoding = Encoding.UTF8, .StandardErrorEncoding = Encoding.UTF8,
                .Arguments = String.Join(" ", args.Select(Function(value) QuoteCommandArgument(value)))
            }
            PortableRuntime.ConfigureProcess(psi)
            _imageProcess = New Process With {.StartInfo = psi, .EnableRaisingEvents = True}
            Dim errors As New StringBuilder()
            AddHandler _imageProcess.OutputDataReceived, Sub(s, ev) If ev.Data IsNot Nothing Then HandleImageProgressLine(ev.Data)
            AddHandler _imageProcess.ErrorDataReceived, Sub(s, ev) If ev.Data IsNot Nothing Then SyncLock errors : errors.AppendLine(ev.Data) : End SyncLock
            _imageRunning = True
            _imageCompleteReceived = False
            _btnImageStart.Enabled = False
            _imageProgress.Value = 0
            _lblImageProgress.Text = "<font color=#D8D8D8>正在加载模型…</font>"
            Try
                _imageProcess.Start()
                _imageProcess.BeginOutputReadLine()
                _imageProcess.BeginErrorReadLine()
                Task.Run(Sub()
                    _imageProcess.WaitForExit()
                    Dim code = _imageProcess.ExitCode
                    Dim errorText As String
                    SyncLock errors : errorText = errors.ToString() : End SyncLock
                    If IsHandleCreated Then BeginInvoke(New Action(Sub()
                        _imageRunning = False
                        _btnImageStart.Enabled = True
                        If code = 0 OrElse _imageCompleteReceived Then
                            _imageProgress.Value = 1000
                            _lblImageProgress.Text = "<font color=#96D2A0>处理完成</font>"
                        Else
                            _lblImageProgress.Text = "<font color=#E07878>处理失败：" & EscapeHtml(LastNonEmptyLine(errorText)) & "</font>"
                        End If
                    End Sub))
                End Sub)
            Catch ex As Exception
                _imageRunning = False
                _btnImageStart.Enabled = True
                _lblImageProgress.Text = "<font color=#E07878>启动失败：" & EscapeHtml(ex.Message) & "</font>"
            End Try
        End Sub

        Private Sub HandleImageProgressLine(line As String)
            If line.StartsWith("IMAGE_COMPLETE|", StringComparison.Ordinal) Then
                _imageCompleteReceived = True
                Return
            End If
            If Not line.StartsWith("IMAGE_PROGRESS|", StringComparison.Ordinal) Then Return
            Dim parts = line.Split("|"c)
            If parts.Length < 6 Then Return
            Dim current, total As Integer
            Dim elapsed, eta As Double
            If Not Integer.TryParse(parts(1), current) OrElse Not Integer.TryParse(parts(2), total) OrElse total <= 0 Then Return
            Double.TryParse(parts(3), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, elapsed)
            Double.TryParse(parts(4), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, eta)
            If IsHandleCreated Then BeginInvoke(New Action(Sub()
                _imageProgress.Value = Math.Max(0, Math.Min(1000, CInt(current * 1000.0 / total)))
                _lblImageProgress.Text = "<font color=#D8D8D8>" & current & "/" & total & "　已用 " & FormatDuration(elapsed) & "　ETA " & FormatDuration(eta) & "</font>"
            End Sub))
        End Sub

        Private Shared Function FormatDuration(seconds As Double) As String
            Dim value = TimeSpan.FromSeconds(Math.Max(0, seconds))
            Return value.ToString(If(value.TotalHours >= 1, "hh\:mm\:ss", "mm\:ss"))
        End Function

        Private Shared Function QuoteCommandArgument(value As String) As String
            If value Is Nothing Then value = ""
            Return """" & value.Replace(""""c, "\""") & """"
        End Function

    End Class

End Namespace
