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

        ' ── 用户模型导入页 ──
        Private ReadOnly _btnPickImportFile As New ModernButton()
        Private ReadOnly _btnPickImportFolder As New ModernButton()
        Private ReadOnly _btnImportModel As New ModernButton()
        Private ReadOnly _lblImportSource As New HtmlColorLabel()
        Private ReadOnly _lblImportStatus As New HtmlColorLabel()
        Private ReadOnly _importModelList As New UltraDetailListView()
        Private _userModelContextMenu As ModernContextMenu
        Private _contextUserModel As UserModelItem
        Private _importSourcePath As String = ""
        Private _modelImportBusy As Boolean = False
        Private _userModelsLoading As Boolean = False
        Private _importModelListConfigured As Boolean = False
        Private Sub BuildOfficialImporterPage()
            _pageImporter.Dock = DockStyle.Fill
            _pageImporter.BackColor = Color.Transparent
            _pageImporter.Padding = New Padding(0, 8, 0, 0)
            _pageImporter.AllowDrop = True
            AddHandler _pageImporter.DragEnter, AddressOf OnModelImportDragEnter
            AddHandler _pageImporter.DragDrop, AddressOf OnModelImportDragDrop

            Dim root As New ModernGridPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 5,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty,
                .AllowDrop = True
            }
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 56.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 44.0F))
            AddHandler root.DragEnter, AddressOf OnModelImportDragEnter
            AddHandler root.DragDrop, AddressOf OnModelImportDragDrop
            root.AddAt(CreateOfficialSectionHeading(
                "模型导入", "安全预检架构、用途、倍率与后端能力，通过后安装到 models\User"), 0, 0)

            Dim sourceRow As New ModernPanel With {
                .Dock = DockStyle.Fill,
                .BackColor = Color.Transparent,
                .BackColor1 = Color.Transparent,
                .BorderSize = 0,
                .BackgroundSource = ModernPanel1,
                .Margin = Padding.Empty,
                .Padding = New Padding(0, 4, 0, 4)
            }
            _btnPickImportFile.Text = "选择模型或压缩包"
            _btnPickImportFile.Dock = DockStyle.Left
            _btnPickImportFile.Width = 148
            ConfigureOfficialImportButton(_btnPickImportFile)
            AddHandler _btnPickImportFile.Click, AddressOf OnPickImportFile
            _btnPickImportFolder.Text = "选择模型文件夹"
            _btnPickImportFolder.Dock = DockStyle.Left
            _btnPickImportFolder.Width = 148
            ConfigureOfficialImportButton(_btnPickImportFolder)
            AddHandler _btnPickImportFolder.Click, AddressOf OnPickImportFolder
            ' 按当前字体测量文字，给高 DPI 和宿主字体预留按钮两侧空间。
            Dim fitSourceButton As Action(Of ModernButton) =
                Sub(button) button.Width = root.ScaleX(Math.Max(148, MeasureTextWidth96(button.Text, button.Font) + 24))
            AddHandler _btnPickImportFile.FontChanged, Sub(sender, args) fitSourceButton(_btnPickImportFile)
            AddHandler _btnPickImportFolder.FontChanged, Sub(sender, args) fitSourceButton(_btnPickImportFolder)
            AddHandler _btnPickImportFile.DpiChangedAfterParent, Sub(sender, args) fitSourceButton(_btnPickImportFile)
            AddHandler _btnPickImportFolder.DpiChangedAfterParent, Sub(sender, args) fitSourceButton(_btnPickImportFolder)
            fitSourceButton(_btnPickImportFile)
            fitSourceButton(_btnPickImportFolder)
            _lblImportSource.Text = "<font color=#888888>尚未选择；也可以拖入文件、文件夹或压缩包</font>"
            _lblImportSource.AutoSize = False
            _lblImportSource.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            Dim sourceValueBox = CreateOfficialValueBox(_lblImportSource)
            sourceValueBox.Margin = Padding.Empty
            sourceValueBox.Dock = DockStyle.Fill
            Dim sourceGap1 As New ModernPanel With {
                .Dock = DockStyle.Left, .Width = UiColumnGap, .BackColor = Color.Transparent,
                .BackColor1 = Color.Transparent, .BorderSize = 0, .BackgroundSource = ModernPanel1
            }
            Dim sourceGap2 As New ModernPanel With {
                .Dock = DockStyle.Left, .Width = UiColumnGap, .BackColor = Color.Transparent,
                .BackColor1 = Color.Transparent, .BorderSize = 0, .BackgroundSource = ModernPanel1
            }
            ' 按 3FUI Designer 的 Dock 顺序：Fill 先加，其他控件从右向左加入。
            sourceRow.Controls.Add(sourceValueBox)
            sourceRow.Controls.Add(sourceGap2)
            sourceRow.Controls.Add(_btnPickImportFolder)
            sourceRow.Controls.Add(sourceGap1)
            sourceRow.Controls.Add(_btnPickImportFile)
            root.AddAt(sourceRow, 0, 1)

            Dim formats As New HtmlColorLabel With {
                .Dock = DockStyle.Fill,
                .Margin = New Padding(0, 8, 0, 4),
                .Padding = New Padding(10, 0, 10, 0),
                .BackColor1 = UiSurface,
                .BorderSize = 0,
                .BorderRadius = UiCornerRadius,
                .AutoSize = False,
                .LineSpacing = 5,
                .TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft,
                .Text = "<font color=#DCDCDC><b>支持格式</b></font>　" &
                        "<font color=#A8A8A8>PTH / PT / CKPT / SAFETENSORS / ONNX / NCNN PARAM+BIN / ZIP / 7Z / RAR</font><br/>" &
                        "<font color=#888888>补帧仅接受能识别为 RIFE、GMFSS 或 GIMM 的权重；双击用户模型可修正能力，选中后按 Delete 或右键可删除。</font>"
            }
            root.AddAt(formats, 0, 2)

            ConfigureImportModelList()
            root.AddAt(_importModelList, 0, 3)

            Dim actionRow As New ModernPanel With {
                .Dock = DockStyle.Fill,
                .BackColor = Color.Transparent,
                .BackColor1 = Color.Transparent,
                .BorderSize = 0,
                .BackgroundSource = ModernPanel1,
                .Margin = Padding.Empty,
                .Padding = New Padding(0, 8, 0, 8)
            }
            _btnImportModel.Dock = DockStyle.Right
            _btnImportModel.Width = 148
            _btnImportModel.Text = "预检并导入模型"
            ConfigureOfficialImportButton(_btnImportModel, UiSuccess)
            AddHandler _btnImportModel.Click, AddressOf OnImportModelClick
            _lblImportStatus.Text = "<font color=#888888>等待选择模型…</font>"
            _lblImportStatus.AutoSize = False
            _lblImportStatus.Dock = DockStyle.Fill
            _lblImportStatus.Margin = Padding.Empty
            _lblImportStatus.Padding = New Padding(0, 0, 18, 0)
            _lblImportStatus.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            actionRow.Controls.Add(_lblImportStatus)
            actionRow.Controls.Add(_btnImportModel)
            root.AddAt(actionRow, 0, 4)
            _pageImporter.Controls.Add(root)
        End Sub

        Private Shared Sub ConfigureOfficialImportButton(button As ModernButton, Optional textColor As Color = Nothing)
            ' 严格对齐 3FUI 官方 Designer 的 ModernButton 用法，不叠加渐变或子控件。
            button.AnimationDuration = 0
            button.BackColor = Color.Transparent
            button.BackColor1 = Color.FromArgb(40, 220, 220, 220)
            button.BackColor2 = Color.Transparent
            button.HoverBackColor1 = Color.FromArgb(60, 220, 220, 220)
            button.HoverBackColor2 = Color.Transparent
            button.PressedBackColor1 = Color.FromArgb(80, 220, 220, 220)
            button.PressedBackColor2 = Color.Transparent
            button.BorderRadius = UiCornerRadius
            button.BorderSize = 0
            button.Margin = New Padding(2)
            button.Padding = Padding.Empty
            button.Icon = Nothing
            button.SubText = ""
            button.TextAlign = ModernButton.TextAlignEnum.Center
            button.ForeColor = If(textColor = Nothing, UiText, textColor)
        End Sub

        Private Sub ConfigureImportModelList()
            If _importModelListConfigured Then Return
            _importModelListConfigured = True
            _importModelList.Dock = DockStyle.Fill
            _importModelList.Margin = New Padding(0, 10, 0, 6)
            _importModelList.AutoScroll = False
            _importModelList.Font = New Font("Microsoft YaHei UI", 9.0F)
            ConfigureTransparentListAppearance(_importModelList)
            _importModelList.HeaderVisible = True
            _importModelList.HeaderHeight = 30
            _importModelList.AllowColumnResize = True
            _importModelList.MultiSelect = False
            _importModelList.AllowDragReorder = False
            _importModelList.ItemPadding = New Padding(10, 5, 8, 5)
            _importModelList.ItemSpacing = 2
            _importModelList.ContentPadding = New Padding(0, 4, 0, 4)
            _importModelList.Columns.AddRange(New UltraDetailListView.ListColumn() {
                New UltraDetailListView.ListColumn("用户模型（双击修正 / Delete 删除）", 300),
                New UltraDetailListView.ListColumn("架构", 150),
                New UltraDetailListView.ListColumn("用途", 110),
                New UltraDetailListView.ListColumn("原生倍率", 80),
                New UltraDetailListView.ListColumn("后端", 210),
                New UltraDetailListView.ListColumn("格式", 100)
            })
            AddHandler _importModelList.ItemDoubleClick, AddressOf OnImportModelDoubleClick
            AddHandler _importModelList.KeyDown, AddressOf OnImportModelListKeyDown
            AddHandler _importModelList.PreviewKeyDown, AddressOf OnImportModelListPreviewKeyDown
            AddHandler _importModelList.MouseDown, AddressOf OnImportModelListMouseDown
            ConfigureDpiListColumns(_importModelList, 210)
        End Sub

        Private Async Sub LoadUserModels()
            If _userModelsLoading Then Return
            Dim exePath = PluginConfig.ResolveInstalledExePath()
            _importModelList.Items.Clear()
            If String.IsNullOrWhiteSpace(exePath) OrElse Not File.Exists(exePath) Then
                AddImportModelMessage("找不到 videoenhancer.exe，请先在超分工作台指定处理程序")
                Return
            End If
            _userModelsLoading = True
            AddImportModelMessage("正在读取用户模型能力清单…")
            Try
                Dim models = Await Task.Run(Function() ModelCatalogClient.RunUserModelList(exePath))
                _importModelList.Items.Clear()
                If models.Count = 0 Then
                    AddImportModelMessage("尚未导入用户模型；可从上方选择文件、文件夹或压缩包")
                    Return
                End If
                For Each model In models
                    Dim item = New UltraDetailListView.ListItem(New UltraDetailListView.ListSubItem() {
                        New UltraDetailListView.ListSubItem(model.DisplayName),
                        New UltraDetailListView.ListSubItem(model.Architecture),
                        New UltraDetailListView.ListSubItem(DisplayUserModelPurpose(model)),
                        New UltraDetailListView.ListSubItem(If(model.Scale > 0, model.Scale.ToString() & "x", "-")),
                        New UltraDetailListView.ListSubItem(String.Join(" / ", model.Backends)),
                        New UltraDetailListView.ListSubItem(model.Format.ToUpperInvariant())
                    }) With {.Tag = model}
                    item.SubItems(0).ForeColor = UiText
                    item.SubItems(4).ForeColor = UiAccent
                    _importModelList.Items.Add(item)
                Next
            Catch ex As Exception
                _importModelList.Items.Clear()
                AddImportModelMessage("能力清单读取失败：" & ex.Message)
            Finally
                _userModelsLoading = False
            End Try
        End Sub

        Private Sub AddImportModelMessage(message As String)
            _importModelList.Items.Add(New UltraDetailListView.ListItem(New UltraDetailListView.ListSubItem() {
                New UltraDetailListView.ListSubItem(message, Nothing, UiTextMuted),
                New UltraDetailListView.ListSubItem(""), New UltraDetailListView.ListSubItem(""),
                New UltraDetailListView.ListSubItem(""), New UltraDetailListView.ListSubItem(""),
                New UltraDetailListView.ListSubItem("")
            }))
        End Sub

        Private Shared Function DisplayUserModelPurpose(model As UserModelItem) As String
            Select Case model.Task.ToLowerInvariant()
                Case "interpolation" : Return "补帧"
                Case "restoration" : Return "修复"
                Case Else : Return If(model.Purpose.Equals("Restoration", StringComparison.OrdinalIgnoreCase), "修复", "超分")
            End Select
        End Function

        Private Sub OnImportModelDoubleClick(sender As Object, e As UltraDetailListView.ListItemEventArgs)
            If e.Item Is Nothing Then Return
            Dim model = TryCast(e.Item.Tag, UserModelItem)
            If model Is Nothing Then Return
            ShowUserModelCapabilityEditor(model)
        End Sub

        Private Sub OnImportModelListPreviewKeyDown(sender As Object, e As PreviewKeyDownEventArgs)
            If e.KeyCode = Keys.Delete Then e.IsInputKey = True
        End Sub

        Private Sub OnImportModelListKeyDown(sender As Object, e As KeyEventArgs)
            If e.KeyCode <> Keys.Delete OrElse e.Modifiers <> Keys.None Then Return
            e.Handled = True
            e.SuppressKeyPress = True
            Dim item = _importModelList.SelectedItem
            Dim model = TryCast(If(item Is Nothing, Nothing, item.Tag), UserModelItem)
            If model Is Nothing Then Return
            DeleteUserModelWithConfirmation(model)
        End Sub

        Private Sub OnImportModelListMouseDown(sender As Object, e As MouseEventArgs)
            If e.Button <> MouseButtons.Right OrElse _modelImportBusy Then Return
            Dim item = _importModelList.GetItemAt(e.X, e.Y)
            Dim model = TryCast(If(item Is Nothing, Nothing, item.Tag), UserModelItem)
            If model Is Nothing Then
                CloseUserModelContextMenu()
                Return
            End If
            Dim index = _importModelList.Items.IndexOf(item)
            If index >= 0 Then _importModelList.SelectedIndex = index
            ShowUserModelContextMenu(model, e.Location)
        End Sub

        Private Sub CloseUserModelContextMenu()
            Dim menu = _userModelContextMenu
            _userModelContextMenu = Nothing
            _contextUserModel = Nothing
            If menu IsNot Nothing Then
                Try
                    menu.Close()
                Catch
                End Try
            End If
        End Sub

        Private Sub ShowUserModelContextMenu(model As UserModelItem, location As Point)
            If model Is Nothing Then Return
            CloseUserModelContextMenu()
            Dim menu As New ModernContextMenu()
            ConfigureModelMenu(menu, reserveIconColumn:=False)
            Dim deleteItem As New ModernContextMenu.ModernMenuItem("删除用户模型") With {
                .CloseOnClick = True,
                .ForeColor = UiDanger
            }
            AddHandler deleteItem.Click,
                Sub(sender As Object, e As EventArgs)
                    Dim target = _contextUserModel
                    CloseUserModelContextMenu()
                    DeleteUserModelWithConfirmation(target)
                End Sub
            If model.Task <> "interpolation" Then
                Dim inspectItem As New ModernContextMenu.ModernMenuItem("重新检测原生能力…") With {.CloseOnClick = True}
                AddHandler inspectItem.Click,
                    Sub(sender, e)
                        Dim target = _contextUserModel
                        CloseUserModelContextMenu()
                        ReinspectUserModel(target)
                    End Sub
                menu.Items.Add(inspectItem)
            End If
            menu.Items.Add(deleteItem)
            _contextUserModel = model
            _userModelContextMenu = menu
            menu.Show(_importModelList, location)
        End Sub

        Private Async Sub ReinspectUserModel(model As UserModelItem)
            If model Is Nothing OrElse _modelImportBusy Then Return
            _modelImportBusy = True
            _lblImportStatus.Text = "<font color=#479CFF>正在重新检测权重原生能力…</font>"
            Try
                Dim detected = Await Task.Run(Function() ReadUserModelInspection(model))
                Dim differences = "架构：" & model.Architecture & " → " & detected.Architecture & Environment.NewLine &
                    "原生倍率：" & model.Scale.ToString() & "x → " & detected.Scale.ToString() & "x" & Environment.NewLine &
                    "输入尺寸倍数：" & model.InputMultiple.ToString() & " → " & detected.InputMultiple.ToString() & Environment.NewLine &
                    "用途：" & model.Purpose & " → " & detected.Purpose & Environment.NewLine &
                    "后端：" & String.Join(" / ", model.Backends) & " → " & String.Join(" / ", detected.Backends)
                _lblImportStatus.Text = "<font color=#3FCD87>重新检测完成；现有清单尚未修改</font>"
                If ShowLakeConfirm(Me, differences & Environment.NewLine & Environment.NewLine &
                    "是否将检测值载入修正窗口？点击保存后才写入清单。", "重新检测结果", defaultYes:=False) Then
                    ShowUserModelCapabilityEditor(detected)
                End If
            Catch ex As Exception
                _lblImportStatus.Text = "<font color=#EB5D5D>重新检测失败：" & EscapeHtml(ex.Message) & "</font>"
            Finally
                _modelImportBusy = False
            End Try
        End Sub

        Private Shared Function ReadUserModelInspection(model As UserModelItem) As UserModelItem
            Dim psi As New ProcessStartInfo With {
                .FileName = PluginConfig.ResolveInstalledExePath(), .UseShellExecute = False,
                .RedirectStandardOutput = True, .RedirectStandardError = True, .CreateNoWindow = True,
                .StandardOutputEncoding = Encoding.UTF8, .StandardErrorEncoding = Encoding.UTF8
            }
            PortableRuntime.ConfigureProcess(psi)
            psi.ArgumentList.Add("--inspect-upscale-model")
            psi.ArgumentList.Add(Path.Combine(PluginConfig.ApplicationRoot, "models", model.RelativePath))
            Using child = Diagnostics.Process.Start(psi)
                If child Is Nothing Then Throw New InvalidOperationException("无法启动模型检测")
                Dim stdoutTask = child.StandardOutput.ReadToEndAsync()
                Dim stderrTask = child.StandardError.ReadToEndAsync()
                child.WaitForExit()
                Dim output = stdoutTask.GetAwaiter().GetResult()
                Dim errorText = stderrTask.GetAwaiter().GetResult()
                If child.ExitCode <> 0 Then Throw New InvalidOperationException(LastNonEmptyLine(If(String.IsNullOrWhiteSpace(errorText), output, errorText)))
                Dim line = output.Split(New Char() {Convert.ToChar(10)}, StringSplitOptions.RemoveEmptyEntries).
                    Last(Function(value) value.Trim().StartsWith("{"c))
                Dim options As New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True}
                Dim detected = JsonSerializer.Deserialize(Of UserModelItem)(line, options)
                detected.Id = model.Id
                detected.DisplayName = model.DisplayName
                detected.RelativePath = model.RelativePath
                detected.Task = model.Task
                detected.Sha256 = model.Sha256
                detected.Size = model.Size
                detected.ImportedAtUtc = model.ImportedAtUtc
                Return detected
            End Using
        End Function

        Private Async Sub DeleteUserModelWithConfirmation(model As UserModelItem)
            If model Is Nothing OrElse _modelImportBusy Then Return
            Dim question = "确定删除用户模型“" & model.DisplayName & "”？" & Environment.NewLine &
                "将删除 models\\User 中的安装文件/目录和能力清单记录。" & Environment.NewLine &
                "删除后无法通过本页恢复。" & Environment.NewLine & Environment.NewLine &
                "路径：" & model.RelativePath
            If Not ShowLakeConfirm(Me, question, "删除用户模型", defaultYes:=False) Then Return

            Dim exePath = PluginConfig.ResolveInstalledExePath()
            If String.IsNullOrWhiteSpace(exePath) OrElse Not File.Exists(exePath) Then
                _lblImportStatus.Text = "<font color=#EB5D5D>删除失败：找不到 videoenhancer.exe</font>"
                Return
            End If
            _modelImportBusy = True
            _lblImportStatus.Text = "<font color=#479CFF>正在删除用户模型…</font>"
            Try
                Dim errorText = Await Task.Run(Function() RunUserModelDelete(exePath, model.Id))
                If errorText.Length > 0 Then
                    _lblImportStatus.Text = "<font color=#EB5D5D>删除失败：" & EscapeHtml(errorText) & "</font>"
                    ShowStatus("用户模型删除失败：" & errorText, True)
                    Return
                End If
                RefreshModels()
                LoadUserModels()
                _lblImportStatus.Text = "<font color=#3FCD87>已删除用户模型，并刷新工作台模型列表</font>"
                ShowStatus("已删除用户模型：" & model.DisplayName, False)
            Catch ex As Exception
                _lblImportStatus.Text = "<font color=#EB5D5D>删除失败：" & EscapeHtml(ex.Message) & "</font>"
                ShowStatus("用户模型删除失败：" & ex.Message, True)
            Finally
                _modelImportBusy = False
            End Try
        End Sub

        Private Shared Function RunUserModelDelete(exePath As String, id As String) As String
            Try
                Dim psi As New ProcessStartInfo With {
                    .FileName = exePath, .UseShellExecute = False, .RedirectStandardOutput = True,
                    .RedirectStandardError = True, .CreateNoWindow = True,
                    .StandardOutputEncoding = Encoding.UTF8, .StandardErrorEncoding = Encoding.UTF8
                }
                PortableRuntime.ConfigureProcess(psi)
                psi.ArgumentList.Add("--delete-user-model")
                psi.ArgumentList.Add(id)
                Using child = Diagnostics.Process.Start(psi)
                    If child Is Nothing Then Return "无法启动用户模型删除进程"
                    Dim stdout = child.StandardOutput.ReadToEnd()
                    Dim stderr = child.StandardError.ReadToEnd()
                    If Not child.WaitForExit(30000) Then
                        Try
                            child.Kill(entireProcessTree:=True)
                        Catch
                        End Try
                        Return "用户模型删除进程超时"
                    End If
                    If child.ExitCode <> 0 Then Return LastNonEmptyLine(If(String.IsNullOrWhiteSpace(stderr), stdout, stderr))
                End Using
                Return ""
            Catch ex As Exception
                Return ex.Message
            End Try
        End Function

        Private Sub ShowUserModelCapabilityEditor(model As UserModelItem)
            Using dialog As New Form With {
                .AutoScaleMode = AutoScaleMode.None,
                .Text = "修正模型能力 - " & model.DisplayName,
                .StartPosition = FormStartPosition.CenterParent,
                .FormBorderStyle = FormBorderStyle.None,
                .MaximizeBox = False,
                .MinimizeBox = False,
                .ShowInTaskbar = False,
                .BackColor = Color.FromArgb(24, 24, 24),
                .ForeColor = UiText,
                .ClientSize = New Size(820, 660),
                .Font = New Font("Microsoft YaHei UI", 9.0F)
            }
                dialog.SuspendLayout()
                Dim chrome As New ThisIsYourWindow With {
                    .BorderColor = Color.FromArgb(72, 72, 72),
                    .BorderSize = 1,
                    .CaptionBackColor = Color.FromArgb(30, 30, 30),
                    .CaptionOverlayColor = Color.Transparent,
                    .TitleForeColor = UiText,
                    .CaptionHeight = 34,
                    .ShowFullScreenButton = False
                }
                Dim grid As New ModernGridPanel With {
                    .Dock = DockStyle.Fill,
                    .ColumnCount = 2,
                    .RowCount = 11,
                    .Padding = New Padding(20, 16, 20, 16),
                    .BackColor = Color.Transparent,
                    .BackColor1 = Color.Transparent,
                    .BackgroundSource = ModernPanel1,
                    .BorderSize = 0
                }
                grid.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 180.0F))
                grid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
                ' 固定保留长字段、说明和按钮高度，避免剩余空间不足时裁切。
                Dim rowHeights = New Single() {96, 96, 42, 42, 42, 42, 42, 42, 96, 56, 60}
                For Each rowHeight As Single In rowHeights
                    grid.RowStyles.Add(New RowStyle(SizeType.Absolute, rowHeight))
                Next

                Dim addCaption As Action(Of String, Integer) =
                    Sub(text, row)
                        Dim label = CreateTextLabel(text, 9.0F, FontStyle.Regular, UiTextSecondary)
                        label.Dock = DockStyle.None
                        label.Margin = Padding.Empty
                        label.TextAlign = ContentAlignment.MiddleLeft
                        grid.AddAt(label, 0, row)
                    End Sub
                Dim readonlyValue As Func(Of String, LakeTextLabel) =
                    Function(text) CreateTextLabel(text, 9.0F, FontStyle.Regular, UiTextMuted)

                ' 长路径和连续校验值使用字符换行的只读文本框，完整原文可选择复制。
                Dim readonlyDetails As Func(Of String, ModernTextBox) =
                    Function(text)
                        Dim box As New ModernTextBox With {.Text = text}
                        ConfigureOfficialTextBox(box, "")
                        box.Font = New Font("Microsoft YaHei UI", 9.0F)
                        box.Margin = New Padding(0, 4, 0, 4)
                        box.Padding = New Padding(8, 4, 8, 4)
                        box.ReadOnly = True
                        box.MultiLine = True
                        box.WordWrap = True
                        Return box
                    End Function

                Dim architectureBox As New ModernTextBox With {.Text = model.Architecture}
                Dim purposeBox As New ModernTextBox With {.Text = model.Purpose}
                ConfigureOfficialTextBox(architectureBox, "模型架构")
                ConfigureOfficialTextBox(purposeBox, "模型用途")

                Dim scaleBox As New ModernNumericUpDown With {
                    .Minimum = 1D,
                    .Maximum = 16D,
                    .Value = Math.Max(1, Math.Min(16, model.Scale)),
                    .Increment = 1D,
                    .DecimalPlaces = 0,
                    .Editable = True,
                    .Dock = DockStyle.Left,
                    .Width = 150,
                    .Height = 34,
                    .BackColor1 = UiSurfaceRaised,
                    .ForeColor = UiText,
                    .BorderColor = Color.Transparent,
                    .BorderSize = 0,
                    .BorderRadius = 8
                }
                Dim multipleBox As New ModernNumericUpDown With {
                    .Minimum = 1D,
                    .Maximum = 1024D,
                    .Value = Math.Max(1, Math.Min(1024, model.InputMultiple)),
                    .Increment = 1D,
                    .DecimalPlaces = 0,
                    .Editable = True,
                    .Dock = DockStyle.Left,
                    .Width = 150,
                    .Height = 34,
                    .BackColor1 = UiSurfaceRaised,
                    .ForeColor = UiText,
                    .BorderColor = Color.Transparent,
                    .BorderSize = 0,
                    .BorderRadius = 8
                }
                If model.Task.Equals("interpolation", StringComparison.OrdinalIgnoreCase) OrElse
                    model.Task.Equals("restoration", StringComparison.OrdinalIgnoreCase) Then scaleBox.Enabled = False

                Dim backendPanel As New ModernPanel With {
                    .Dock = DockStyle.Fill,
                    .BackColor = Color.Transparent,
                    .BackColor1 = Color.Transparent,
                    .BackgroundSource = ModernPanel1,
                    .BorderSize = 0,
                    .LayoutMode = ModernPanel.LayoutModeEnum.Flow,
                    .FlowDirection = ModernPanel.FlowDirectionEnum.LeftToRight,
                    .WrapContents = True,
                    .ScrollBarMode = ModernPanel.ScrollMode.None,
                    .Padding = New Padding(0, 2, 0, 2)
                }
                Dim backendChecks As New List(Of ModernCheckBox)()
                Dim backendNames = New String() {"ncnn", "cuda", "tensorrt", "onnx", "flashvsr", "basicvsrpp"}
                For Each backendName In backendNames
                    Dim check = New ModernCheckBox With {
                        .Text = backendName,
                        .Checked = model.Backends.Contains(backendName, StringComparer.OrdinalIgnoreCase),
                        .AutoSize = False,
                        .Width = 150,
                        .Height = 32,
                        .Margin = New Padding(0, 2, 8, 2),
                        .ForeColor = UiText,
                        .BackColor = Color.Transparent,
                        .BackgroundSource = ModernPanel1,
                        .ClickAnywhere = True
                    }
                    backendChecks.Add(check)
                    backendPanel.Controls.Add(check)
                Next

                addCaption("模型文件", 0) : grid.AddAt(readonlyDetails(model.RelativePath), 1, 0)
                addCaption("格式 / SHA-256", 1) : grid.AddAt(readonlyDetails(model.Format.ToUpperInvariant() & "  ·  " & model.Sha256), 1, 1)
                addCaption("任务类别（只读）", 2) : grid.AddAt(readonlyValue(DisplayUserModelPurpose(model) & "  [" & model.Task & "]"), 1, 2)
                addCaption("架构", 3) : grid.AddAt(architectureBox, 1, 3)
                addCaption("用途", 4) : grid.AddAt(purposeBox, 1, 4)
                addCaption("原生倍率", 5) : grid.AddAt(scaleBox, 1, 5)
                addCaption("输入尺寸倍数", 6) : grid.AddAt(multipleBox, 1, 6)
                Dim sizeRequirement = If(model.MinimumSize > 0, "最小 " & model.MinimumSize.ToString() & " px", "无额外最小值") &
                    If(model.Square, "；要求正方形", "") & If(String.IsNullOrWhiteSpace(model.Tiling), "", "；切片 " & model.Tiling)
                addCaption("其他尺寸要求（只读）", 7) : grid.AddAt(readonlyValue(sizeRequirement), 1, 7)
                addCaption("可用后端", 8) : grid.AddAt(backendPanel, 1, 8)

                Dim hint = readonlyValue("此处修正模型原生能力；目标输出倍率请在工作台设置。可用重新检测核对权重。")
                hint.ForeColor = UiTextMuted
                grid.AddAt(hint, 0, 9)
                grid.SetColumnSpan(hint, 2)
                Dim buttons As New ModernPanel With {
                    .Dock = DockStyle.Fill,
                    .BackColor = Color.Transparent,
                    .BackColor1 = Color.Transparent,
                    .BackgroundSource = ModernPanel1,
                    .BorderSize = 0,
                    .LayoutMode = ModernPanel.LayoutModeEnum.Flow,
                    .FlowDirection = ModernPanel.FlowDirectionEnum.LeftToRight,
                    .WrapContents = False
                }
                Dim saveButton As New ModernButton With {.Text = "保存修正", .Size = New Size(130, 40), .Margin = New Padding(8, 4, 0, 4)}
                Dim cancelButton As New ModernButton With {.Text = "取消", .Size = New Size(100, 40), .Margin = New Padding(8, 4, 0, 4)}
                ConfigurePrimaryButton(saveButton)
                ConfigureSecondaryButton(cancelButton)
                AddHandler cancelButton.Click, Sub(sender, args) dialog.Close()
                AddHandler saveButton.Click,
                    Sub(sender, args)
                        Dim selectedBackends = backendChecks.Where(Function(check) check.Checked).
                            Select(Function(check) check.Text).ToArray()
                        Dim errorText = UpdateUserModelCapabilities(model.Id, architectureBox.Text, purposeBox.Text,
                            CInt(Math.Round(scaleBox.Value)), CInt(Math.Round(multipleBox.Value)), selectedBackends)
                        If errorText.Length > 0 Then
                            ShowLakeInfo(dialog, errorText, "能力修正失败")
                            Return
                        End If
                        dialog.DialogResult = DialogResult.OK
                        dialog.Close()
                    End Sub
                buttons.Controls.Add(saveButton)
                buttons.Controls.Add(cancelButton)
                grid.AddAt(buttons, 0, 10)
                grid.SetColumnSpan(buttons, 2)
                ' 小屏可滚动查看全部内容；窗口高度包含自绘标题栏占用。
                Dim content As New ModernPanel With {
                    .Dock = DockStyle.Fill,
                    .BackColor = Color.Transparent,
                    .BackColor1 = Color.Transparent,
                    .BackgroundSource = ModernPanel1,
                    .BorderSize = 0,
                    .ScrollBarMode = ModernPanel.ScrollMode.Vertical
                }
                grid.Dock = DockStyle.Top
                grid.Height = CInt(rowHeights.Sum()) + grid.Padding.Vertical
                content.Controls.Add(grid)
                dialog.Controls.Add(content)
                dialog.AutoScaleMode = AutoScaleMode.Dpi
                dialog.AutoScaleDimensions = New SizeF(96.0F, 96.0F)
                dialog.ResumeLayout(True)
                chrome.Attach(dialog)
                Dim fitDialog As Action =
                    Sub()
                        If dialog.IsDisposed OrElse dialog.Disposing Then Return
                        Dim workingArea = Screen.FromControl(dialog).WorkingArea
                        Dim chromeHeight = CInt(Math.Round((chrome.CaptionHeight + chrome.BorderSize * 2) * dialog.DeviceDpi / 96.0R))
                        dialog.ClientSize = New Size(Math.Min(grid.ScaleX(820), workingArea.Width - grid.ScaleX(16)),
                            Math.Min(grid.Height + Math.Max(chromeHeight, dialog.Padding.Vertical), workingArea.Height - grid.ScaleY(16)))
                    End Sub
                fitDialog()
                AddHandler dialog.DpiChanged, Sub(sender, args) dialog.BeginInvoke(fitDialog)
                Try
                    If dialog.ShowDialog(Me) = DialogResult.OK Then
                        LoadUserModels()
                        RefreshModels()
                        _lblImportStatus.Text = "<font color=#3FCD87>已保存能力修正，并刷新工作台模型列表</font>"
                    End If
                Finally
                    chrome.Detach(dialog)
                End Try
            End Using
        End Sub
        Private Function UpdateUserModelCapabilities(id As String, architecture As String, purpose As String,
                                                     scale As Integer, inputMultiple As Integer,
                                                     backends As String()) As String
            Dim exePath = PluginConfig.ResolveInstalledExePath()
            If String.IsNullOrWhiteSpace(exePath) OrElse Not File.Exists(exePath) Then Return "找不到 videoenhancer.exe"
            Try
                Dim psi As New ProcessStartInfo With {
                    .FileName = exePath, .UseShellExecute = False, .RedirectStandardOutput = True,
                    .RedirectStandardError = True, .CreateNoWindow = True,
                    .StandardOutputEncoding = Encoding.UTF8, .StandardErrorEncoding = Encoding.UTF8
                }
                PortableRuntime.ConfigureProcess(psi)
                Dim arguments = New String() {"--json", "--update-user-model", id, "--user-architecture", architecture,
                    "--user-purpose", purpose, "--user-scale", scale.ToString(), "--user-input-multiple",
                    inputMultiple.ToString(), "--user-backends", String.Join(",", backends)}
                For Each argument In arguments : psi.ArgumentList.Add(argument) : Next
                Using child = Diagnostics.Process.Start(psi)
                    If child Is Nothing Then Return "无法启动能力清单更新进程"
                    Dim stdout = child.StandardOutput.ReadToEnd()
                    Dim stderr = child.StandardError.ReadToEnd()
                    child.WaitForExit(30000)
                    If child.ExitCode <> 0 Then Return LastNonEmptyLine(If(String.IsNullOrWhiteSpace(stderr), stdout, stderr))
                End Using
                Return ""
            Catch ex As Exception
                Return ex.Message
            End Try
        End Function

        Private Sub OnPickImportFile(sender As Object, e As EventArgs)
            If _modelImportBusy Then Return
            Using dialog As New OpenFileDialog With {
                .Title = "选择要预检并导入的模型",
                .Filter = "支持的模型|*.pth;*.pt;*.pkl;*.ckpt;*.safetensors;*.onnx;*.param;*.bin;*.zip;*.7z;*.rar;*.tar;*.gz;*.xz;*.zst|所有文件|*.*",
                .CheckFileExists = True,
                .Multiselect = False
            }
                If dialog.ShowDialog(Me) = DialogResult.OK Then SetImportSource(dialog.FileName)
            End Using
        End Sub

        Private Sub OnPickImportFolder(sender As Object, e As EventArgs)
            If _modelImportBusy Then Return
            Using dialog As New FolderBrowserDialog With {.Description = "选择模型文件夹或 NCNN param/bin 目录"}
                If dialog.ShowDialog(Me) = DialogResult.OK Then SetImportSource(dialog.SelectedPath)
            End Using
        End Sub

        Private Sub OnModelImportDragEnter(sender As Object, e As DragEventArgs)
            If e.Data IsNot Nothing AndAlso e.Data.GetDataPresent(DataFormats.FileDrop) Then
                e.Effect = DragDropEffects.Copy
            Else
                e.Effect = DragDropEffects.None
            End If
        End Sub

        Private Sub OnModelImportDragDrop(sender As Object, e As DragEventArgs)
            If _modelImportBusy Then Return
            Dim paths = TryCast(If(e.Data Is Nothing, Nothing, e.Data.GetData(DataFormats.FileDrop)), String())
            If paths IsNot Nothing AndAlso paths.Length > 0 Then SetImportSource(paths(0))
        End Sub

        Private Sub SetImportSource(path As String)
            _importSourcePath = If(path, "").Trim()
            _lblImportSource.Text = "<font color=#D0D0D0>" & EscapeHtml(_importSourcePath) & "</font>"
            _lblImportStatus.Text = "<font color=#888888>准备进行元数据与兼容性预检</font>"
        End Sub

        Private Async Sub OnImportModelClick(sender As Object, e As EventArgs)
            If _modelImportBusy Then Return
            If String.IsNullOrWhiteSpace(_importSourcePath) Then
                _lblImportStatus.Text = "<font color=#E0A45C>请先选择要导入的模型、文件夹或压缩包</font>"
                Return
            End If
            If Not File.Exists(_config.ExePath) Then
                ShowStatus("请先指定 videoenhancer.exe", True)
                Return
            End If
            _modelImportBusy = True
            _btnImportModel.Text = "正在预检并导入…"
            _lblImportStatus.Text = "<font color=#479CFF>正在安全读取模型元数据并验证能力…</font>"
            Try
                Dim psi As New ProcessStartInfo With {
                    .FileName = _config.ExePath,
                    .UseShellExecute = False,
                    .RedirectStandardOutput = True,
                    .RedirectStandardError = True,
                    .CreateNoWindow = True,
                    .StandardOutputEncoding = Encoding.UTF8,
                    .StandardErrorEncoding = Encoding.UTF8
                }
                PortableRuntime.ConfigureProcess(psi)
                psi.ArgumentList.Add("--json")
                psi.ArgumentList.Add("--import-model")
                psi.ArgumentList.Add(_importSourcePath)
                Using child = Diagnostics.Process.Start(psi)
                    If child Is Nothing Then Throw New InvalidOperationException("无法启动模型导入进程")
                    Dim stdoutTask As Task(Of String) = child.StandardOutput.ReadToEndAsync()
                    Dim stderrTask As Task(Of String) = child.StandardError.ReadToEndAsync()
                    Await child.WaitForExitAsync()
                    Dim stdout = Await stdoutTask
                    Dim stderr = Await stderrTask
                    Dim jsonLine = stdout.Replace(Convert.ToChar(13).ToString(), "").
                        Split(New Char() {Convert.ToChar(10)}, StringSplitOptions.RemoveEmptyEntries).
                        LastOrDefault(Function(line) line.Trim().StartsWith("["c))
                    Dim results As List(Of ModelImportResponse) = Nothing
                    If Not String.IsNullOrWhiteSpace(jsonLine) Then
                        results = JsonSerializer.Deserialize(Of List(Of ModelImportResponse))(jsonLine.Trim(),
                            New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True})
                    End If
                    Dim resultItems = If(results, New List(Of ModelImportResponse)())
                    Dim succeeded = resultItems.Where(Function(item) item.Success).Count()
                    Dim failed = resultItems.Where(Function(item) Not item.Success).Count()
                    If succeeded > 0 Then
                        RefreshModels()
                        LoadUserModels()
                        Dim first = results.First(Function(item) item.Success)
                        _lblImportStatus.Text = "<font color=#3FCD87>已导入 " & succeeded.ToString() & " 个模型：" &
                            EscapeHtml(first.Architecture) & "，可用后端 " & EscapeHtml(String.Join(" / ", first.Backends)) & "</font>"
                        ShowStatus("模型已安装到 models\User，并刷新工作台模型列表", False)
                    End If
                    If failed > 0 OrElse succeeded = 0 Then
                        Dim failure = If(results, New List(Of ModelImportResponse)()).FirstOrDefault(Function(item) Not item.Success)
                        Dim message = If(failure IsNot Nothing, failure.Error, LastNonEmptyLine(stderr))
                        _lblImportStatus.Text = "<font color=#EB5D5D>预检未通过：" & EscapeHtml(message) & "</font>"
                        ShowStatus("模型导入失败：" & message, True)
                    End If
                End Using
            Catch ex As Exception
                _lblImportStatus.Text = "<font color=#EB5D5D>导入失败：" & EscapeHtml(ex.Message) & "</font>"
                ShowStatus("模型导入失败：" & ex.Message, True)
            Finally
                _modelImportBusy = False
                _btnImportModel.Text = "预检并导入模型"
            End Try
        End Sub
    End Class

End Namespace
