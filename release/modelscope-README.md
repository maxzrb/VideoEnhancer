# VideoEnhancer Releases

VideoEnhancer 独立维护版的公开更新源，仅用于分发插件运行文件，不包含模型、Python 环境或 PotPlayer。

- 版本检查以 GitHub `maxzrb/VideoEnhancer` 的 Release 为唯一标准；本数据集是更新包的首选下载镜像。
- `stable.json`：稳定通道结构化更新清单（与 GitHub Release 附带的清单资产内容一致）。
- `releases/<version>/VideoEnhancer-<version>-win-x64.zip`：经 SHA-256 校验的三文件更新包。
- ZIP 内 `package.json`：逐文件大小与 SHA-256，更新器只允许替换 `videoenhancer.exe`、`videoenhancer.3fui.dll` 和 `videoenhancer-layout.json`。

项目采用独立 SemVer；上游版本仅作为同步基线记录，不参与自动更新比较。
