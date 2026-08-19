import io

path = 'README.md'
with io.open(path, encoding='utf-8') as f:
    src = f.read()

old = "| lblMaster | 130x34（Padding 14） | `_lblMaster`（HtmlColorLabel） |"
new = "| lblMaster | 589x34（Padding 14） | `_lblMaster`（HtmlColorLabel，文案「插件总开关 关闭此开关时，超分主页面功能不生效」） |"
assert old in src, 'lblMaster row not found'
src = src.replace(old, new)

anchor = "## 新增控件的工作流"
insert = """## 同步状态

- 2026-08-18：`PluginLayoutForm` / `PreviewLayoutForm` 的最新坐标已同步回
  `VideoEnhancerPlugin\\PluginPanel.vb`（`BuildUpscalePage` / `BuildPreviewPage`）：
  底部状态栏与选项卡 Dock 顺序已修正（状态栏不再被选项卡覆盖，预览页底栏可正常显示）；
  插件总开关文案与宽度 589 同步；实时预览页左侧 30px 边距同步。

""" + anchor
assert anchor in src, 'workflow anchor not found'
src = src.replace(anchor, insert, 1)

with io.open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(src)
print('README designer updated')
