# VideoEnhancer Releases

VideoEnhancer 的公开更新源，仅用于分发插件运行文件，不包含模型、Python 环境或 PotPlayer。

- 版本检查以 GitHub `maxzrb/VideoEnhancer` 的 Release 为首选标准；GitHub 不可达时本数据集提供 `stable.json` 和更新包兜底。
- `stable.json`：稳定通道结构化更新清单（与 GitHub Release 附带的清单资产内容一致）。
- `releases/<version>/VideoEnhancer-<version>-win-x64.exe`：经大小与 SHA-256 校验的单文件更新资产，内嵌对应版本的插件 DLL。
- 更新器等待 3FUI 退出后替换 EXE，并从新 EXE 释放 `videoenhancer.3fui.dll`；布局 JSON 已嵌入 DLL，不再单独发布。

项目采用独立 SemVer；上游版本仅作为同步基线记录，不参与自动更新比较。
