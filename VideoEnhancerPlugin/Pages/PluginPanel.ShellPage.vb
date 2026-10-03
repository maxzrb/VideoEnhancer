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

        ' ── 资源管理器右键超分页 ──
        Private ReadOnly _cmbShellBackend As New WheelLockedComboBox()
        Private ReadOnly _cmbShellModel As New WheelLockedComboBox()
        Private ReadOnly _btnShellAdd As New ModernButton()
        Private ReadOnly _btnShellClear As New ModernButton()
        Private ReadOnly _btnShellApply As New ModernButton()
        Private ReadOnly _btnShellRemove As New ModernButton()
        Private ReadOnly _shellModelList As New UltraDetailListView()
        Private ReadOnly _shellCatalog As New List(Of ModelCatalogItem)()
        Private Sub BuildOfficialShellPage()
            _pageShell.Dock = DockStyle.Fill
            _pageShell.LayoutMode = ModernPanel.LayoutModeEnum.Absolute
            Dim root As New DpiLayoutPanel With {
                .Dock = DockStyle.Fill, .BackColor = Color.Transparent, .BackColor1 = Color.Transparent,
                .LayoutMode = ModernPanel.LayoutModeEnum.Absolute, .BorderSize = 0
            }
            AddWorkbenchRow(root, CreateOfficialSectionHeading(
                "资源管理器右键超分", "当前用户生效：图片右键 → 超分辨率 → 模型；输出固定为无损 PNG"), 8, 32)

            _cmbShellBackend.WaterText = "选择图片后端…"
            ConfigureCombo(_cmbShellBackend)
            For Each item In New String() {"NCNN (Vulkan)", "CUDA (PyTorch)", "TensorRT (NVIDIA)", "ONNX Runtime", "FlashVSR (NVIDIA)", "BasicVSR++ (NVIDIA)"}
                _cmbShellBackend.Items.Add(item)
            Next
            _cmbShellBackend.SelectedIndex = 0
            AddHandler _cmbShellBackend.SelectedIndexChanged, AddressOf OnShellBackendSelected
            _cmbShellModel.WaterText = "选择要加入菜单的模型…"
            ConfigureCombo(_cmbShellModel)
            Dim backendField = CreateOfficialField("推理后端", _cmbShellBackend)
            Dim modelField = CreateOfficialField("模型", _cmbShellModel)
            AddWorkbenchControl(root, backendField, 44, UiFieldHeight, 0.0F, 0.36F, 0, -UiColumnGap)
            AddWorkbenchControl(root, modelField, 44, UiFieldHeight, 0.36F, 1.0F)

            Dim actionRow As New ModernHorizontalPanel(132.0F, CSng(UiColumnGap), 132.0F, -1.0F)
            ConfigureSecondaryButton(_btnShellAdd) : _btnShellAdd.Text = "添加当前模型"
            ConfigureSecondaryButton(_btnShellClear) : _btnShellClear.Text = "清空模型列表"
            _btnShellAdd.Dock = DockStyle.Fill : _btnShellAdd.Margin = New Padding(0, 4, 0, 4)
            _btnShellClear.Dock = DockStyle.Fill : _btnShellClear.Margin = New Padding(0, 4, 0, 4)
            AddHandler _btnShellAdd.Click, AddressOf OnShellAdd
            AddHandler _btnShellClear.Click, AddressOf OnShellClear
            actionRow.AddColumn(_btnShellAdd, 0)
            actionRow.AddColumn(_btnShellClear, 2)
            AddWorkbenchRow(root, actionRow, 108, UiRowHeight)

            ConfigureShellModelList()
            AddWorkbenchRow(root, _shellModelList, 152, 150)

            Dim applyRow As New ModernHorizontalPanel(112.0F, CSng(UiColumnGap), 132.0F, -1.0F)
            ConfigurePrimaryButton(_btnShellApply) : _btnShellApply.Text = "应用设置"
            ConfigureSecondaryButton(_btnShellRemove) : _btnShellRemove.Text = "移除右键菜单"
            _btnShellApply.Dock = DockStyle.Fill : _btnShellApply.Margin = New Padding(0, 4, 0, 4)
            _btnShellRemove.Dock = DockStyle.Fill : _btnShellRemove.Margin = New Padding(0, 4, 0, 4)
            AddHandler _btnShellApply.Click, AddressOf OnShellApply
            AddHandler _btnShellRemove.Click, AddressOf OnShellRemove
            applyRow.AddColumn(_btnShellApply, 0)
            applyRow.AddColumn(_btnShellRemove, 2)
            AddWorkbenchRow(root, applyRow, 310, UiRowHeight)
            _pageShell.Controls.Add(root)
            RefreshShellSummary()
        End Sub

        Private Sub OnShellBackendSelected(sender As Object, e As EventArgs)
            LoadShellModels()
        End Sub

        Private Sub LoadShellModels()
            If Not File.Exists(_config.ExePath) OrElse _cmbShellBackend.SelectedItem Is Nothing Then Return
            Dim backend = BackendValue(_cmbShellBackend.SelectedItem)
            _cmbShellModel.Items.Clear()
            _shellCatalog.Clear()
            _cmbShellModel.WaterText = "正在读取模型列表…"
            Task.Run(Sub()
                Dim catalog = ModelCatalogClient.RunModelCatalog(_config.ExePath, "--list-model-catalog", "-backend", backend)
                If IsHandleCreated Then BeginInvoke(New Action(Sub()
                    _shellCatalog.Clear() : _shellCatalog.AddRange(catalog)
                    _cmbShellModel.Items.Clear()
                    For Each entry In catalog
                        _cmbShellModel.Items.Add(If(String.IsNullOrWhiteSpace(entry.DisplayName), entry.Id, entry.DisplayName))
                    Next
                    If _cmbShellModel.Items.Count > 0 Then _cmbShellModel.SelectedIndex = 0
                    _cmbShellModel.WaterText = If(catalog.Count = 0, "当前后端没有可用模型", "选择模型…")
                End Sub))
            End Sub)
        End Sub

        Private Sub OnShellAdd(sender As Object, e As EventArgs)
            If _cmbShellModel.SelectedIndex < 0 OrElse _cmbShellModel.SelectedIndex >= _shellCatalog.Count Then
                ShowStatus("请先选择一个模型", True) : Return
            End If
            If _config.ShellModels Is Nothing Then _config.ShellModels = New List(Of ShellUpscaleModel)()
            Dim entry = _shellCatalog(_cmbShellModel.SelectedIndex)
            Dim backend = BackendValue(_cmbShellBackend.SelectedItem)
            If Not _config.ShellModels.Any(Function(item) String.Equals(item.Backend, backend, StringComparison.OrdinalIgnoreCase) AndAlso String.Equals(item.Model, entry.Id, StringComparison.OrdinalIgnoreCase)) Then
                _config.ShellModels.Add(New ShellUpscaleModel With {.Backend = backend, .Model = entry.Id, .DisplayName = If(String.IsNullOrWhiteSpace(entry.DisplayName), entry.Id, entry.DisplayName)})
                _config.Save()
            End If
            RefreshShellSummary()
        End Sub

        Private Sub OnShellClear(sender As Object, e As EventArgs)
            If _config.ShellModels Is Nothing Then _config.ShellModels = New List(Of ShellUpscaleModel)()
            _config.ShellModels.Clear() : _config.Save() : RefreshShellSummary()
        End Sub

        Private Sub ConfigureShellModelList()
            If _shellModelList.Columns.Count > 0 Then Return
            _shellModelList.Dock = DockStyle.Fill
            _shellModelList.Margin = Padding.Empty
            _shellModelList.AutoScroll = False
            _shellModelList.Font = New Font("Microsoft YaHei UI", 9.2F)
            ConfigureTransparentListAppearance(_shellModelList)
            _shellModelList.HeaderVisible = True
            _shellModelList.HeaderHeight = 30
            _shellModelList.MultiSelect = False
            _shellModelList.AllowDragReorder = False
            _shellModelList.ItemPadding = New Padding(10, 5, 8, 5)
            _shellModelList.Columns.AddRange(New UltraDetailListView.ListColumn() {
                New UltraDetailListView.ListColumn("模型", 560),
                New UltraDetailListView.ListColumn("后端", 130),
                New UltraDetailListView.ListColumn("操作", 100)
            })
            AddHandler _shellModelList.ItemClick, AddressOf OnShellModelItemClick
            ConfigureDpiListColumns(_shellModelList, 260)
        End Sub

        Private Sub OnShellModelItemClick(sender As Object, e As UltraDetailListView.ListItemEventArgs)
            If e Is Nothing OrElse e.ColumnIndex <> 2 OrElse e.Item Is Nothing Then Return
            Dim target = TryCast(e.Item.Tag, ShellUpscaleModel)
            If target Is Nothing OrElse _config.ShellModels Is Nothing Then Return
            _config.ShellModels.Remove(target)
            _config.Save()
            RefreshShellSummary()
            ShowStatus("已从右键超分列表删除模型；点击「应用设置」后更新资源管理器菜单", False)
        End Sub

        Private Sub OnShellApply(sender As Object, e As EventArgs)
            Try
                ShellUpscaleMenu.Apply(_config.ExePath, If(_config.ShellModels, New List(Of ShellUpscaleModel)()))
                ShowStatus("右键超分菜单已应用；资源管理器刷新后生效", False)
            Catch ex As Exception
                ShowStatus("应用右键菜单失败：" & ex.Message, True)
            End Try
        End Sub

        Private Sub OnShellRemove(sender As Object, e As EventArgs)
            Try
                ShellUpscaleMenu.Remove()
                ShowStatus("已移除右键超分菜单", False)
            Catch ex As Exception
                ShowStatus("移除右键菜单失败：" & ex.Message, True)
            End Try
        End Sub

        Private Sub RefreshShellSummary()
            Dim entries = If(_config.ShellModels, New List(Of ShellUpscaleModel)())
            _shellModelList.Items.Clear()
            If entries.Count = 0 Then
                _shellModelList.Items.Add(New UltraDetailListView.ListItem(New UltraDetailListView.ListSubItem() {
                    New UltraDetailListView.ListSubItem("尚未添加模型。请从上方添加。", Nothing, UiTextMuted),
                    New UltraDetailListView.ListSubItem(""),
                    New UltraDetailListView.ListSubItem("")
                }))
                Return
            End If
            For Each entry In entries
                _shellModelList.Items.Add(New UltraDetailListView.ListItem(New UltraDetailListView.ListSubItem() {
                    New UltraDetailListView.ListSubItem(entry.DisplayName, Nothing, UiText),
                    New UltraDetailListView.ListSubItem(entry.Backend, Nothing, UiAccent),
                    New UltraDetailListView.ListSubItem("删除", Nothing, UiDanger)
                }) With {.Tag = entry})
            Next
        End Sub

    End Class

End Namespace
