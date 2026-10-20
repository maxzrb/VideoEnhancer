VideoEnhancer 手动安装包
========================

本包内的目录结构与 3FUI 安装布局一致：

先完全退出 3FUI。
把解压出的 plugin 文件夹整体复制到 3FUI 根目录（与 FFmpegFreeUI.exe 或 FFmpegFreeUI.x64.exe 同级），
如提示已有同名文件选择覆盖即可。完成后启动 3FUI，在插件里执行一次环境检查。
请保持包内目录结构。插件配置与运行缓存保存在 plugin\videoenhancer 目录内。
教程图片缓存保存在 plugin\videoenhancer\cache\TutorialImages，旧缓存会在加载教程时迁入。
下载和解压的完整错误日志保存在 plugin\videoenhancer\logs\downloads.log。
下载、校验和解压时可点击进度取消；进入后端安装事务后请等待完成。
归档解压与 7z 创建统一使用便携组件 bin\7zip\7za.exe，单独更新运行 EXE 时也会自动释放该组件及许可证、源码。
项目 MIT 许可证在 plugin\videoenhancer\LICENSE.txt；第三方声明及对应许可证在同目录下。

如果从旧版平铺目录升级，可在 3FUI 根目录打开 PowerShell，复制完成后执行：

& .\plugin\videoenhancer\videoenhancer.exe --cleanup-legacy-residue `
  --plugin-root .\plugin `
  --legacy-local-app-data $env:LOCALAPPDATA `
  --legacy-temp-root $env:TEMP

该命令会把旧 plugin\bin、models、python、缓存和更新目录安全合并到
plugin\videoenhancer；同名不同内容不会覆盖，并会保留在原位置供人工处理。
它还会迁移旧 AppData 配置、删除旧 INI 和已知临时更新器残留。

下载功能使用 bin\aria2-next\aria2-next.exe。第三方组件声明位于
THIRD-PARTY-NOTICES.txt，许可证与来源信息位于 licenses 目录。

模型与后端（Python 环境等）体积较大，不在本包内；
装好插件后在模型下载页按需获取。

预览组件由本包独立安装到 bin\fff-native-11。仅运行 EXE 更新会保留已有 DLL；缺少组件时请用完整 ZIP 或安装器修复。FFF.Native/动态依赖的许可证在 licenses\fff-native，对应源码在同版本独立源码包，LakeUI 赞助授权说明在 licenses\LakeUI，Python RVE 集成的 AGPL 许可在 licenses\RVE。自有 C#/VB 代码 MIT 范围见 LICENSE-SCOPE.md。

源码与二进制分开打包：VideoEnhancer-Source.zip 含项目源码及第三方对应源码，安装器与手动安装 ZIP 仅含运行文件、许可证和来源说明。
