# PluginLayoutDesigner — 插件页面布局设计器

用于在 Visual Studio 设计器里**图形化**调整「视频超分」插件页面的控件位置，
再把手动排版的结果移植回 `VideoEnhancerPlugin\PluginPanel.vb`。

## 为什么需要它

`PluginPanel.vb` 的 UI 是 100% 代码创建的（没有 `.Designer.vb`），且用了 LakeUI
自绘控件（`ModernComboBox` / `BooleanSwitch` / `HtmlColorLabel`），VS 设计器无法直接打开。
本工程用**标准 WinForms 控件**（`ComboBox` / `CheckBox` / `Label` / `Button`）按相同尺寸
摆出同样的页面，保证设计器可以正常打开、拖拽，坐标与真实代码一一对应。

## 打开方式

1. 用 Visual Studio 打开 `PluginLayoutDesigner.vbproj`（需要 .NET 10 SDK 与
   “使用 Windows 窗体”工作负载；VS 2022 17.14+ 或 VS 2026）。
2. 双击 `PluginLayoutForm.vb` 或 `PreviewLayoutForm.vb` 进入**设计视图**，直接拖动控件、在属性窗口改
   `Location / Size / Text / Padding`。
3. `F5` 运行仅用于查看布局，**不提供任何运行时调整功能**；
   所有控件调整都在 VS 设计视图里完成（见上一步）。

## 控件名 → 真实字段映射

| 设计器控件 | 大小(初始) | 对应真实代码 |
| --- | --- | --- |
| pnlMaster 行 | 900x50 | `sectionMaster` |
| chkMaster | 66x34 | `_switchMaster`（BooleanSwitch） |
| lblMaster | 589x34（Padding 14） | `_lblMaster`（HtmlColorLabel，文案「插件总开关 关闭此开关时，超分主页面功能不生效」） |
| pnlUpscale 行 | 900x56 | `sectionUpscale` |
| chkUpscale | 66x34 | `_switchUpscale` |
| lblUpscale | 120x34（Padding 14） | `_lblSwitch` |
| lblUpscaleModel | 110x34 | 「放大模型」标签 |
| cmbModel | 456x25* | `_cmbModel`（ModernComboBox，真实高 40） |
| pnlBackend 行 | 900x50 | `sectionBackend` |
| lblBackend | 130x36 | `_lblBackend` |
| cmbBackend | 220x25* | `_cmbBackend`（真实高 36） |
| pnlInterp 行 | 900x56 | `sectionInterp` |
| chkInterp | 66x34 | `_switchInterp` |
| lblInterp | 80x34（Padding 14） | `_lblSwitchInterp` |
| lblInterpModel | 110x34 | 「补帧模型」标签 |
| cmbInterp | 300x25* | `_cmbInterp`（真实高 40） |
| lblFactor | 76x34 | `_lblFactor` |
| cmbFactor | 90x25* | `_cmbFactor`（真实高 40） |
| pnlExe 行 | 900x44 | `sectionExe` |
| lblExe | 780x32 | `_lblExe` |
| btnExe | 110x32 | `_btnPickExe`（ModernButton） |
| pnlStatus 区 | 900x214 | `sectionStatus` |
| lblStatus | 900x40 | `_lblStatus` |

> \* WinForms 的 `ComboBox` 显示高度被系统固定（约 25px），**宽度才是有效值**；
> 真实 LakeUI `ModernComboBox` 高度为 36/40，移植时保持宽度、改回高度即可。

## 实时预览页设计器（PreviewLayoutForm）

同一工程里还包含 `PreviewLayoutForm.vb`（实时预览页布局），用标准控件摆出真实预览页：

```text
┌ pnlTitle（Dock=Top, 36）──────────────────────────────┐
│ lblTitle（Fill）实时预览    预览超分/编码完成的帧        │
├ pnlTask（Dock=Top, 36）───────────────────────────────┤
│ lblTask(Left,96) 预览任务   cmbTask(Left,300) 任务下拉  │
├ lblStatus（Dock=Top, 26）─────────────────────────────┤
│ 等待编码队列任务…                                       │
├ picPreview（Dock=Fill，原生 PictureBox，SizeMode=Zoom）┤
├ pnlBottom（Dock=Bottom, 46）──────────────────────────┤
│ lblNote(Fill) 处理速度较慢时…  lblRate(Right,90) 切换频率│
│ cmbRate(Right,150) 0.5秒/1秒/2秒/3秒/关键帧模式         │
└───────────────────────────────────────────────────────┘
```

- `PreviewLayoutForm` 对应 `PluginPanel.vb` 的 `BuildPreviewPage()`（原生 PictureBox 预览，
  任务选择 ModernComboBox、切换频率下拉、底部说明）。
- `F5` 运行时会同时打开两个设计窗体（`PluginLayoutForm` + `PreviewLayoutForm`），
  仅用于查看布局效果；调整请在 VS 设计视图中进行。
- 两个窗体全部使用**绝对坐标**（`Location / Size`，无 `Dock`），因此
  VS 设计视图可以直接拖动每个控件（Dock 布局在 VS 中只能停靠、无法自由拖动）。
- 在设计器中调整后，把「控件名 (x, y) 宽x高」发回，我会按同样坐标更新
  `PluginPanel.vb` 的 Dock/Width 值并重新编译。

## 坐标与 DPI 说明

- 本窗体 `AutoScaleMode = None`，`Location / Size` 就是**逻辑像素**，与
  `PluginPanel.vb` 里的数值一致；3FUI 宿主会按 DPI 自动缩放插件页面，
  **不需要按屏幕分辨率/缩放比例换算**。
- 行面板（pnl*）之间的垂直间距：master 0–50、upscale 50–106、backend 106–156、
  interp 156–212、exe 212–256、status 256–470；行内控件 y 表示相对行顶的偏移。
- 页面可用宽度约 850–900（真实宿主左侧导航 + 右侧内容区）。若控件超出 900，
  说明在真实页面会贴边或截断，请左移。

## 同步状态

- 2026-08-18：`PluginLayoutForm` / `PreviewLayoutForm` 的最新坐标已同步回
  `VideoEnhancerPlugin\PluginPanel.vb`（`BuildUpscalePage` / `BuildPreviewPage`）：
  底部状态栏与选项卡 Dock 顺序已修正（状态栏不再被选项卡覆盖，预览页底栏可正常显示）；
  插件总开关文案与宽度 589 同步；实时预览页左侧 30px 边距同步。

## 新增控件的工作流

1. 在对应行面板里拖入新控件（工具箱：Label / ComboBox / CheckBox / Button / Panel…）；
2. 属性窗口里把 `Name` 改成与真实字段对应的名字（如 `cmbSomething`），记下
   `Location / Size`；
3. 把「控件名 (x, y) 宽x高」发给我（或让开发人员读取 `PluginLayoutForm.Designer.vb`
   的 diff），我再移植进 `PluginPanel.vb`（LakeUI 控件 + Dock 布局）并编译验证；
4. 需要新行时：把窗体拉高，拖入一个新 `Panel`（参考现有 pnl* 的行高），
   在行里摆放控件即可。

## 构建（命令行）

```powershell
dotnet build .\PluginLayoutDesigner.vbproj -c Release
```

产物：`bin\Release\net10.0-windows\PluginLayoutDesigner.exe`。

## JSON 图形化布局模式

现在可以直接运行：

```powershell
.\bin\Release\net10.0-windows\PluginLayoutDesigner.exe --studio
```

该模式不依赖 LakeUI 运行时，左侧画布可拖动标签、按钮、下拉框、开关和面板；右侧属性区可编辑名称、类型、文字、中心点 `CenterX / CenterY`、宽度和高度。保存出的 JSON 可直接交给插件开发流程读取，坐标定义为画布左上角为原点的逻辑像素。

示例结构：

```json
{
  "CanvasWidth": 900,
  "CanvasHeight": 620,
  "Controls": [
    { "Name": "lblTitle", "Type": "Label", "Text": "实时预览", "CenterX": 180, "CenterY": 50, "Width": 180, "Height": 36 }
  ]
}
```
