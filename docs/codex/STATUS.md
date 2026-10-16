# Project Status

Last updated: 2026-10-16 21:53
Updated by: Codex

本文件是唯一 AI 操作状态来源。历史原文已冻结归档至 [记录归档](../archive/records-2026-10-01/README.md)，仅查历史时读取；归档中的版本、远端、环境和 TODO 不代表当前状态。

## Current Snapshot

- 2026-10-16 21:53：用户授权将本轮完整实时预览修复提交maxzrb上游PR，取代之前不提交/推送限制；不授权合并、发行、部署或改版本。目标maxzrb/VideoEnhancer:main，源user-Wing/VideoEnhancer-fork:fix/realtime-preview-20261016，上游472d62f未变、当前无对应OPEN PR。提交前托管构建0警告0错误、25项预览基础回归、18项倍率UI及LakeUI5.112实际GPU换图重验；前轮5.110、真实4K原片和纯压制日志保留。只提交本轮10项源码/测试/文档，不纳入用户问题说明、媒体、提取宿主DLL及产物。版本仍1.3.13；准备普通提交和推送fork，不直接推origin/main。

- 2026-10-16 21:42：**针对纯压制预览继续补修：音频/探测阶段等待视频，阶段/进程/时间回退及输出截断重置游标，使用当前步骤临时视频输出；失败关键帧保留重试。**已确认用户安装的是上轮修复DLL（4F40…），不能归咎旧版未替换。本轮新DLL CBDCE1173744CD436FA50007CB4840B2CA1EB4444C926D03E28D58D0AE1B93C1尚未部署。原安装DLL在新增音频阶段测试失败；修订版25项基础检查通过。用户OP原片为3840×2160/约48fps/AV1/yuv444p12le，3秒p6/CRF12禁音频纯压制144包/3.003秒成功，模拟音频→真实视频编码期间收到6张不同预览。原片四档也持续换图，但解码使实际间隔约1.26/1.32/4.01/6.12秒，不能宣称精确达到选项周期。原片8秒无损截段未封口写入可更新三个关键帧；两版LakeUI GPU表面读回确认实际插件回调红→蓝正常，没有修改LakeUI控件。真实安装宿主分步成员契约与18项倍率UI通过；完整宿主实际任务/全部用户预设可视验收仍待替换候选DLL后确认。版本1.3.13保持，普通发行构建FFF下载限制保持，未提交/推送/PR/发布/改安装或用户原视频；工作树非干净，建议确认后提交。

- 2026-10-16 20:56：**本机源码已同步最新origin/main 472d62f（1.3.13），实时预览两项修复已完成本地回归，未安装/提交/推送/发布。**从最新上游建立fix/realtime-preview-20261016，旧分支和用户未跟踪videoenhancer pr.txt保留；PR11当前已MERGED，不再沿用旧OPEN状态。关键帧/输入帧率探测均异步、单航班、2500ms超时终止进程；关键帧改-read_intervals窗口包PTS/flags，不解码全量帧，独立记录探测大小，支持PTS0。定时模式按上次抽帧进度而非上一Tick判断、取消0.25秒内容门槛，调度100ms，切换模式/任务/隐藏/停止取消旧工作并拒绝旧画面。旧代码两项失败复现；新增慢探测/UI心跳/超时/去重/取消和真实媒体回归通过，0.5/1/2/3秒实测约0.53/1.10/2.08/3.04秒，未封口AV1/yuv444p12le样本持续显示多个新关键帧且写入进程正常完成。托管DLL构建0警告0错误；普通构建受FFF固定上游下载失败阻挡，使用DesignTimeBuild=true跳过依赖下载进行编译，不能称完整发行构建通过。38项Python、18项倍率UI、宿主契约和LakeUI5.110/5.112预览回调通过。现版本保持1.3.13，原GB级原片/实际3FUI可视交互仍需用户复验；建议提交保存本轮修复后再切换工具。

- 2026-10-06 20:36：**1.3.13已正式发行并完成验收**：[GitHub Release](https://github.com/maxzrb/VideoEnhancer/releases/tag/v1.3.13)五项正式资产，main/发行标签源码b97622a。四宫格三文件及按钮/窗体/回调/释放/专属DPI残留安全剥离，当前实时预览双LakeUI回调通过。完整对应源码合为一个独立ZIP仅GitHub；ModelScope仅本版三个运行附件与stable/说明，Models备用EXE同步，API列表确认无源码附件。10项远端实下载大小/SHA通过。完整安装载荷已安全部署本机，45文件与ZIP一致、FFF10DLL齐全，配置/用户模型/aria2/7zip/RTX保持，未启动宿主。后端2026.09.30.1无变化，不重发Python/模型/RTX。必要本地门禁通过，6GB/1080p/4x保存OOM及同条件历史速度差异仍保留。本条收尾记录待普通提交推送，最后核对干净工作树与远端SHA。

- 2026-10-06 20:30：1.3.13最终构建与必要门禁通过，四宫格专属代码已剥离；修正安装/Burn检查的旧Artifacts路径误读。后端UNCHANGED，准备冻结并上传五项GitHub资产/运行镜像，源码ZIP仅GitHub。提交3747d0a已保存，检查脚本和本条待保存，正式发布仍待完成。

- 2026-10-06 20:20：继续发行1.3.13，新增四宫格残留安全剥离范围；完成引用检查并删除专属三文件/入口残留/DPI测试，实时预览兼容层保留，构建与发行门禁待执行。

- 2026-10-06 19:48：用户明确授权发布1.3.13，并指定项目/第三方对应源码合并为一个独立ZIP，仅上传GitHub Release，不上传ModelScope。Git启动已同步且干净，main15e4139，v1.3.13未存在。版本源同步1.3.13，发布脚本分离GitHub源码目录与ModelScope运行白名单暂存目录，来源说明/README/发布流程同步；旧版本记录移历史并补1.3.12历史，1.3.13当前为发布中。准备本体构建、既有门禁、后端逐文件审计与远端哈希核对；不追加长速度测试，保留6GB/1080p/4x保存问题。

- 2026-10-06 19:34：用户授权安全合并PR10/11，已完整合入main：PR11 eb51739，PR10 c3f667d，合并前审核恢复点81be99d/codex/pre-pr10-pr11-20261006。产品代码无冲突，两份记录冲突已保留双方会话且逐块校验。组合版在Artifacts/pr10-pr11-merged完成publish、110项打包/真实项目内安装及FFF API11、LakeUI5.110/5.112实际回调、FPS和倍率UI18项检查；转换器内嵌资源/源码ZIP SHA一致。源码ZIP约60MiB独立，运行ZIP约17MiB，包内无源码归档。根据用户“无必要就不发”核对LGPL2.1第4条、MS-RL3(A)与GPL2第3条：相关第三方完整对应源码须可获取，ZIP形式非强制；当前仓库自动源码不含完整FriBidi/构建补丁/WiX源码，因此保留PR10独立source.zip作为后续发行的履约方案，旧1.3.12不补发、不上传Release、不改版本、不部署。两PR远端均MERGED，origin/main已包含两个合并提交；本条收尾记录将提交并普通推送，完成后核对工作树与远端SHA。原速度反馈同条件A/B及6GB/1080p/4x保存OOM仍未解决，不恢复长测速。

- 2026-10-06 19:32：用户授权安全合并PR10/11，已完整合入main：PR11 eb51739，PR10 c3f667d，合并前审核恢复点81be99d/codex/pre-pr10-pr11-20261006。产品代码无冲突，两份记录冲突已保留双方会话且逐块校验。组合版在Artifacts/pr10-pr11-merged完成publish、110项打包/真实项目内安装及FFF API11、LakeUI5.110/5.112实际回调、FPS和倍率UI18项检查；转换器内嵌资源/源码ZIP SHA一致。源码ZIP约60MiB独立，运行ZIP约17MiB，包内无源码归档。根据用户“无必要就不发”核对LGPL2.1第4条、MS-RL3(A)与GPL2第3条：相关第三方完整对应源码须可获取，ZIP形式非强制；当前仓库自动源码不含完整FriBidi/构建补丁/WiX源码，因此保留PR10独立source.zip作为后续发行的履约方案，旧1.3.12不补发、不上传Release、不改版本、不部署。本地合并完成，准备普通推送origin/main，远端PR状态待核对。原速度反馈同条件A/B及6GB/1080p/4x保存OOM仍未解决，不恢复长测速。

- 2026-10-06 18:00：PR #10/#11 审核完成，建议接纳，未合并。固定头 ea3776f / 6f0756b，共同基线 main f261e37；报告 docs/pr10-pr11-review.md。PR11 在 LakeUI5.110/5.112 实际回调均通过，未修复插件在5.112复现同一 set_Image 异常；此前AVV3图内缩放已生效，本PR主要补内嵌转换脚本与FPS统计，不能据作者异机/改SVT参数结果宣称原9→3.9反馈全部解决。6GB/1080p/4x保存校验OOM仍未处理，未重跑大尺寸/长测速。PR10完整publish、40归档与110打包检查通过；取消RAR支持。PR11两构建、CPU/CUDA2项、FPS与倍率UI18项通过。两PR产品代码自动合并，STATUS/中文进度冲突需保留双方；无实际组合运行。主工作区保留原记录改动，仅新增审核报告并更新两份记录，非干净，无提交/推送/部署/发布。

- 2026-10-06 13:51：**本轮修复已提交上游[PR11](https://github.com/maxzrb/VideoEnhancer/pull/11)，未合并/发布。**源码提交4583bbd6d278b8605c214cb9ea3dcdb211d7e465，源user-Wing/VideoEnhancer-fork:fix/trt-native-lakeui-20261006，目标maxzrb/VideoEnhancer:main；普通OPEN PR（非草稿），回读mergeStateStatus=CLEAN，尚无CI检查，不能写成CI通过。21项源码/测试/文档仅包含本轮修复，版本1.3.12保持，无视频/权重/Engine/安装二进制；PR已附当前聊天。构建和定向回归均通过，性能边界已列入说明；收尾记录随PR分支同步，不直接修改origin/main或fork/main。

- 2026-10-06 13:49：用户已授权将本轮AVV3/LakeUI/FPS修复及测速记录提交maxzrb上游PR，覆盖前述仅本地不推送的限制，但未授权合并/发布/改版本。当前fix/trt-native-lakeui-20261006基于origin/main f261e37，上游已最新；目标maxzrb/VideoEnhancer:main，源user-Wing/VideoEnhancer-fork同名分支，尚无对应PR。提交前复验build0警告0错误、38项Python、18项倍率UI、FPS/ETA、LakeUI5.110/5.112实际预览回调、宿主契约均通过。全部dirty文件均为本轮已记录的源码/测试/文档，准备显式暂存、提交和推送；视频/Engine/安装包/本机缓存不纳入，版本1.3.12保持。

- 2026-10-06 13:44：**MyGO / AVV3 TensorRT 2x / 用户新SVT参数实际约三分钟测试通过，未持续掉到4fps。**输入01.mkv为1080p/23.976，复用1080p/scale2缓存、FP16无分块，p6/CRF12/yuv444p10le、禁音频；新串tune0/CDEF-1/scm3/QM2及4/variance1/filmgrain5非adaptive/AC1/lp4完整保持。编码启动后181.69秒正常停止，分段超分7.821→8.196→8.694fps，编码7.330→8.499→8.882fps；积压约140帧后缩至约100帧、编码工作集约9–10GB、GPU大多高负载，无采样热降频标记。输出1575视频包/65.691秒，AV1/3840×2160/yuv444p10le，仅视频；编码末frame1575/progress=end、首帧解码通过，不宣称全片逐帧解码。证据.work/verification/20261006-mygo-avv3-svt，[报告](../trt-preview-performance-20261006.md)已追加。只改诊断脚本/文档，不改预设/产品/版本/安装，1.3.12保持；仍未提交/推送，整集持续性及旧版历史差异待验证。

- 2026-10-06 11:14：用户明确“不必测了，等pr”，已终止当前冷却复测进程树，不再追加测试，等待贡献者 UI/AVV3 PR。1.3.12发布/本机/隔离EXE SHA一致；1080p/FP16/60帧空输出已有2x 3.984FPS、3x 3.643FPS，4x诊断3.574FPS（原版保存校验在6GB显存OOM，仅测试改CPU数值比较后构建）。4x回传后缩小的路径对照2x 3.536FPS、3x 3.544FPS；仅路径模拟，不是旧发行版同机A/B。GPU明确SW Power Cap/SW Thermal Slowdown Active，当前55W/默认80W、纯Engine测时577–900MHz，不能把9→3.9全部归因代码或宣称已确认完整性能回退。此前修复已生效，但小尺寸验收未证明恢复9FPS。产品源码/版本/安装/发布均不改，完整证据Artifacts/avv3-speed-audit。

- 2026-10-06 10:07：**SVT参数逐项短测确认主要降速项为显式 enable-cdef=1。**直接压用户Rebellion2160p原片前240帧，不超分、不跑完整片，p6/CRF12/yuv444p10le固定。完整串4.726fps，快速组合11.808fps；完整串只去掉CDEF为10.002fps，只改为用户追加要求的enable-cdef=-1为10.689fps；快速组合仅加CDEF1降到4.877fps。AC0/DLF1/QM0/TF1/variance0及删DLF均约4.9–5.2fps，没有单独恢复。11组串行，速度按240/benchmark rtime，包含短片启动/排空，不称全片稳定值；画质/码率等价性和1.3.0历史差异仍未验证。新增release/tests/MeasureSvtParameters.ps1及[排查报告](../svt-parameter-ablation-20261006.md)，证据.work/verification/20261006-svt-ablation。版本仍1.3.12，本轮不改产品代码/安装/预设，不关闭已打开宿主，不commit/push/PR/Release；保留之前所有未提交修改。

- 2026-10-06 09:34：用户授权仅将SVT preset6改10作持续对照，其余参数/模型/片段/运行环境保持，不写回预设。**能跟上约9fps，但请求p10被SVT4.2.0的4K Random Access限制为实际p9。**960帧渲染106.72秒（9.00fps），处理中约9.3–9.5fps，未降到4；GPU多约90%–97%、编码工作集约12GB、PPCS156（p6为300）。ffprobe完整确认AV1/3840×2160/yuv444p10le/960帧；配置SHA FDEAC061…保持，无代码/版本/安装/发布更改。日志fixed-janai-svt-p10-960.*及复验报告已追加；旧版p6能9fps的差异仍未定位，不能将快速preset测试当作旧版回归已修复。

- 2026-10-06 09:27：**本地 TensorRT / LakeUI 修复已部署，保持 1.3.12，不发布远端。**已从 maxzrb 的 origin/main `f261e37` 建立 `fix/trt-native-lakeui-20261006`，旧 feature/backup 分支保留；origin=maxzrb/VideoEnhancer，fork=user-Wing/VideoEnhancer-fork，不能沿用下文旧会话远端命名。AVV3 显示 `realesr-animevideov3 2/3/4x`，用户确认保留官方 4x 权重、GPU 图内 2x/3x 输出；转换脚本内嵌随 CLI 同步，4x 与 >4x 策略保持。实时预览/四宫格兼容 LakeUI Image/Source，修正 FPS 首条进度帧偏移；没有调整 UI 布局或画质参数。
- 持续降速已按用户 SVT-AV1 参数复现：OP.mkv 前 960 帧、AnimeJaNai 原生 2x、TRT FP16、无分块，NVENC p1/CQ30 为96.81秒（9.92fps），用户 libsvtav1 preset6/CRF12/yuv444p10le/完整 svtav1-params 为256.29秒（3.75fps）。143秒附近超分803帧但编码337帧，编码工作集约23–25GB，GPU多次1%–3%，无热降频标记，后期GPU完成后继续编码排空；瓶颈在SVT编码反压，不宣称TRT已将同参数整链恢复9fps。尚未做1.3.0同环境历史对照。证据见[复验记录](../trt-preview-performance-20261006.md)。
- 验证：LakeUI5.110/5.112实际插件帧回调、38项Python、18项倍率UI、FPS偏移/暂停/ETA、host契约、安装/自更新/回滚通过；AVV3新建2/3/4x Engine输出160×128/240×192/320×256，6x复用4x输出480×384，均12帧。已用官方自更新入口部署，本机EXE/DLL/转换脚本与产物一致，配置SHA256 FDEAC061…保持；备份 `.work/backups/20261006-before-trt-preview-fix`。未启动3FUI，实际可视预览待用户复验。源码含本轮未提交修改，无提交/推送/PR/Release操作。

- 2026-10-05 18:04：本轮四项已完成，源码提交 649aeb9 已推送；[PR #10](https://github.com/maxzrb/VideoEnhancer/pull/10) 已创建（OPEN、非草稿、可合并），[issue #6](https://github.com/maxzrb/VideoEnhancer/issues/6) 已附核查证据并以 completed 关闭。MIT 原创 C#/VB、RVE Python AGPL-3.0-only、已确认 LakeUI 赞助授权及全部第三方许可分界已落实；FFF API 11 独立构建获取/分发，移除 SharpCompress并统一 7za，源码与二进制分包。最终 publish 0 警告/0 错误，40归档/110打包/8下载/6后端更新/5门禁及安装/更新/回滚通过。分支 fix/dependency-licenses-native-archives；收尾记录提交后同步并复核干净工作树，未合并 PR、改版本、发行或部署。以下记录均为历史。

- 2026-10-05 15:15：用户反馈 RTX 报 `The first backend release writes MP4 output.：rawvideo`，已通过源码历史定位为旧组件请求校验：ea16ce4 删除 MP4-only 限制，c83df0f 加入原始帧管道；当前2db5b02不含该错误，CLI明确传rawvideo/framePipePath。判断为新版CLI调用旧RTX组件；用户实际EXE/hash未取得，尚待核对更新及旧路径残留。优先加载bin/rtx-video/vsr_backend.exe，其次runtime/vsr_backend.exe，前者旧副本可能遮蔽后者。仅诊断与记录，无代码修改、部署或发布。

- 2026-10-05 15:08：本轮跨设备 Codex 续接，整改第三方许可、FFF.Native 独立分发及统一 7za 解压，完成后向 maxzrb/VideoEnhancer 提交 PR 并核查 issue #6。用户已确认取得 LakeUI 赞助许可证；VideoEnhancer 自有源码保留 MIT。当前主线 f261e37/1.3.12，以下旧暂停与候选快照均为历史，发行收尾已见最新日志。

- 2026-10-04 23:49：**老视频 RTX 再排查修复完成，仅本地候选。**真实奇数尺寸 MPEG-4 Visual 343×259 复现 D3D11 上传缓冲失败（报 Cannot allocate memory）；内部 NV12/P010 缓冲偶数化+边缘补齐，保持可见尺寸，完整 CLI 4x 输出1372×1036/4帧通过。补 RGB/PAL/灰度/打包YUV软解帧转换，原BGR24/gray的 format_unsupported 已消除。11老视频样本、五解码及五黑边回归、19C++单元通过；RV40仅四包视频前缀，不含完整RMVB/COOK验收。报告 docs/rtx-legacy-decode-audit.md，综合候选 patch 基于6afb9a8并包含黑边修复，不能与 visible-rect patch 叠加。候选SHA 690b0801859eb625fb32b7706d378046777c6fe5363433983eac522413396983，替代上条黑边候选SHA；用户原片/日志/RTX是否更新未知。继续暂停上传、推送和本机部署。

- 2026-10-04 23:01：**RTX黑边已构造复现并完成本地修复验证，未部署/发布。**342×258可见画面/384×288编码画布的合法crop样本，原组件4x输出右/底黑边283274像素，2x也复现70646像素；源片正常解码无黑边。根因D3D11源矩形禁用，采样隐藏编码填充；硬解需显式可见矩形+crop偏移，软解保留并完整应用crop避免对齐残边。修复后五裁剪/非对齐输入黑像素0，既有五解码及16C++测试通过。反馈原片未知，不能断言完全同根因。报告docs/rtx-black-border-audit.md；候选RTX hash04fd467035699b97022ef955d4e593c2fb37297ca03bc011dc8b48dcc4fca83f。

- 用户“先不急发布”暂停后续发行；组件识别修订1.3.11源码/标签9989899及GitHub/两处MS资产在暂停到达前已上传，尚未回读修订11项远端hash/本机覆盖。暂停后未继续上传、推送或部署；黑边修复与同日期RTX严查SHA补充仍本地未提交。

- 2026-10-04 22:28：用户“这些做完后覆盖11”明确授权同版本修订1.3.11，取代临时1.3.12开发分流，版本源已恢复1.3.11。通用Bin组件SHA256安装标记、旧安装版本待核验、同路径旧完整包/旧续传缓存失效已实现。组件状态22场景、真实下载/标记及旧缓存替换通过；0警告0错误。先前1.3.11五资产留存Artifacts/release-1.3.11-before-component，WiX已clean避免同版本载荷缓存；修订发行构建/门禁进行中，随后覆盖源码标签/GitHub/MS及本机。RTX独立包仍2026.10.04.1，不覆盖其权重/运行库。

- 2026-10-04 22:19：**1.3.11正式发布完成**：[GitHub v1.3.11](https://github.com/maxzrb/VideoEnhancer/releases/tag/v1.3.11)、ModelScope Releases及Models备用EXE同步；源码/标签7ffeea2。RTX2026.10.04.1同步Models，源码6afb9a8/tag已推送。11本体远端实际下载hash+RTX归档hash+最新包筛选通过，双源故障回退通过；真实CLI覆盖旧RTX并记录版本通过。本机正式升级1.3.11及RTX，配置/用户清单/aria2/7zip保持，宿主未启动。用户新增要求：其他组件也须更新识别，随后独立实现，不修改已发布1.3.11资产。

- 2026-10-04 22:12：用户已授权发布1.3.11，覆盖之前仅本机试用限制。版本源与分类说明已更新；补RTX安装版本标记及下载页“可更新”入口，旧用户升级本体后刷新列表即可下载覆盖组件。RTX独立版本2026.10.04.1，源提交6afb9a8。发行五资产已冻结，本地publish/安装与回滚/自更新/Burn/队列/宿主/滚动/Python11项/组件状态4场景通过；Python独立归档仍2026.09.30.1，29,709文件审计UNCHANGED，尺寸脚本作为CLI内嵌资源同步，不上传新Python包。即将提交推送和发布双源，远端回读及本机正式升级尚待完成。

- 2026-10-04 21:47：AVV3官方PTH确认网络4x，目标2x来自后缩放；官方示例用CPU Lanczos4，项目TRT沿用图内双三次。更正NCNN“原生2/3x”：本项目2/3x图仍48通道卷积+PixelShuffle4，末尾Interp0.5/0.75，属于2/3x输出图，不是独立2/3x训练网络；待复核界面/清单用词。仅文档纠正，不修改已部署修复。

- 2026-10-04 21:30：**用户授权部署供3FUI测试，已备份并部署到本机Plugin目录。CLI/插件DLL/RTX组件与配套尺寸脚本共16项hash核对通过，配置/用户清单/aria2/7zip保持。真实安装CLI对FMP4样本RTX2x输出1280x720/4帧成功；宿主未启动，等待用户测试。仍1.3.10本地试用，不发布远端。**

- 2026-10-04 21:21：**模型目标尺寸与RTX解码回退修复完成，隔离构建/实机定向验收通过。TRT低倍率恢复图内双三次，CUDA及BasicVSR++张量侧Lanczos4留在GPU；NCNN/ONNX/Flash现有CPU输出在下游前调整。六后端、图片、两种补帧顺序、跨后端及TRT分块/高倍率通过；RTX五输入和完整CLI FMP4输出4帧通过，双显卡能力探测同步选择NVIDIA。源码差异及候选资产可审查，未部署/发布/改版本/提交/推送。**

- 2026-10-04 19:47：MPEG-4 Visual对照已复现RTX普通尺寸解码失败：FMP4 640x360 RTX首包Invalid argument/0帧；同源NCNN2x输出1280x720/4帧成功，低尺寸192x128 RTX软解输出384x256/4帧成功。三FourCC样本FFmpeg/OpenCV通过。sidecar仅按低尺寸软解，缺按编码能力回退；待确认用户失败后端/日志，详见[定位报告](../mpeg4-visual-decode-audit.md)，未实施修复。

- 2026-10-04 19:40：全后端补查完成。CUDA/NCNN/ONNX低目标同样原生推理后FFmpeg缩小，相比旧RVE队列前OpenCV缩小改变管道量/算法；先超后补可一直按原生高尺寸运行。FlashVSR2/4x直接推理，BasicVSR++官方4x仍后缩放。统一方案扩大至全部后端，详见审计报告；未实施产品修复或测速。

- 2026-10-04 19:37：全模型倍率路径审计完成，见[审计报告](../model-output-scale-path-audit.md)。98项清单中41项TRT（11项4x、28项2x、2项1x）共用通用转换入口，低倍率输出风险不限AVV3；视频/图片、分块及组合补帧须整体处理。FlashVSR显式2/4x保留，其他后端原生后缩放不能直接等同TRT旧版回归。尚未实施产品修复或逐模型速度测试。

- 2026-10-04 19:33：用户确认AVV3为TRT。发现旧版按后缀传目标倍率到convert_tensorrt.py，FinalOutputScale将4x后bicubic缩小编入GPU引擎；新版固定4x Engine→4x帧回传/管道→FFmpeg Lanczos缩小，存在可解释的性能退化路径，尚未同参数计时。FFmpeg本机包含RM/RV10–40；官方RMVB样本前缀4帧RGB解码通过，OpenCV首帧可读，但帧数报告偏大。四宫格漏rm/rmvb扩展名，RVE元数据依赖OpenCV，需定向修复；本轮未改产品代码。

- 2026-10-04 19:21：三条用户反馈完成代码核查：普通RVE由后端启动FFmpeg默认CPU解码并管道送帧，分段同样软解；RTX为独立sidecar，历史低分辨率软解已有专项修复，不能据此宣称所有编码都可回退。AVV3内置NCNN有原生2/3/4x，PTH登记4x；目标2x在固定4x模型后缩放。已询问失败后端/日志、模型格式及前后版本，未修改产品代码或发布。

- 2026-10-04 11:35：**1.3.10正式发布完成：[GitHub v1.3.10](https://github.com/maxzrb/VideoEnhancer/releases/tag/v1.3.10)、ModelScope Releases及Models备用EXE同步；源码/注释标签9bfba97已推送。PR9合并fb19abe，PR8仅aria2选择性提交5f1c9c0，原PR8已关闭。11远端实际下载hash、双源故障回退通过；本机正式升级1.3.10，配置/用户清单保持，aria2-next2.8.3。Backend继续2026.09.30.1、不动模型权重；发行收尾文档提交推送后核对干净工作树。**

- 2026-10-04 11:25：1.3.10主线整合与最终门禁完成，五项资产已冻结。PR8仅aria2选择性提交5f1c9c0，PR9合并fb19abe；build/publish0警告0错误、六类插件UI/宿主契约、下载队列5场景、Python路径16项、发布5/后端更新6、内外层安装回滚/自更新通过。后端逐文件UNCHANGED，准备推送main/v1.3.10并发布双源。

- 2026-10-04 11:15：用户反馈试用功能合理，明确授权选择性合并PR8/PR9并发布1.3.10；覆盖先前不正式合并/不发布限制。PR8仅aria2提交5f1c9c0，PR9原版ae94b26无冲突合入；保留倍率语义和WiX。准备正式发行门禁、双源发布与本机升级。

- 2026-10-04 10:46：用户已升级3FUI至6.2.35/LakeUI5.110；PR8仅aria2-next2.8.3版本/固定哈希/源码与许可打包选择性移植到main，不合并倍率或安装交互。PR9精确ae94b26在Artifacts/pr9-review隔离组合publish通过，已备份后部署；EXE/DLL/aria2哈希一致，配置/用户能力清单保持。PR9未合并，未发布/提交，等待实际试用反馈。详见[审核报告](../pr8-pr9-review.md)。

- 2026-10-01 17:54：**1.3.9已按授权替换为8x修订版**。目标范围1–8，旧9–16x配置载入为8；源码与v1.3.9标签`b1cebcb`已推送，五资产/稳定清单/分类正文及两处ModelScope同步。11项实际下载哈希、故障回退通过，本机同版本替换成功且配置/用户能力清单/aria2保持。已安装旧1.3.9可等后续版本正常更新；修订及收尾记录已推送，工作树干净。

- 2026-10-01 17:47：8x范围修订及旧配置兼容通过定向CLI/15项UI检查；最终build/publish0警告0错误、安装/自更新及ZIP/清单哈希通过，Backend审计仍UNCHANGED。冻结修订资产，开始按授权提交并替换1.3.9标签/GitHub与ModelScope资产。

- 2026-10-01 17:41：用户明确授权将目标输出倍率上限改8x并替换已发布1.3.9；CLI/插件/图片桥与旧配置载入同步调整，原生能力不变。原发行资产本地保留，按同版本覆盖流程重新构建、验证、更新标签及双源资产，正在实施。

- 2026-10-01 17:30：**1.3.9正式发布完成**：[GitHub v1.3.9](https://github.com/maxzrb/VideoEnhancer/releases/tag/v1.3.9)、ModelScope Releases及Models备用EXE已同步；源码/注释标签`0f38b64`已推送。五项资产双源及Models共11实际下载大小/SHA256通过，双源故障回退通过；本机正式升级1.3.9，配置/用户清单/aria2保持，未启动宿主。独立Backend继续2026.09.30.1。收尾文档提交推送后核对干净工作树。

- 2026-10-01 17:22：1.3.9本地构建和版本/清单/ZIP哈希通过，内层安装器与自更新隔离通过，Python33/33、倍率参数、UI12/悬停23、发布门禁5/后端更新6通过；独立Backend审计UNCHANGED。准备提交推送、标签和双源发布。

- 2026-10-01 17:17：用户明确授权正式发布1.3.9；已同步Git、确认凭据和版本未复用，版本源/发行说明/版本记录更新。构建、后端逐文件审计及最终版本门禁进行中，之后提交推送并发布双源；不重复完整GPU矩阵。

- 2026-10-01 17:01：用户截图反馈长路径/SHA-256仍裁切，已改为可选择复制的LakeUI只读多行文本框，字符自动换行并保留完整原文。两项长字段96高，实际落实固定说明/按钮行与小屏滚动；17项布局/字符覆盖检查通过，最终构建与本地部署哈希一致，配置保持。

- 2026-10-01 16:42：修复能力编辑弹窗底部裁切，路径/校验值、说明与按钮预留独立行，小屏可滚动。导入选择按钮按实际字体测量宽度。9项布局检查、编译/publish及diff检查通过，已部署本机，配置哈希不变。

- 2026-10-01 16:24：修复模型悬停提示窗释放后的复用与已显示状态阻止再次显示；提示关联当前菜单弹窗。介绍依据公开作者资料重写，纠正实拍/动漫定位、具体修复用途和过时限制，来源见[模型介绍依据](../model-introduction-sources.md)。23项提示/文案检查及12项倍率UI检查通过，已部署本机。

- 2026-10-01 15:54：用户选择/重新选择超分模型即恢复原生输出倍率(OutputScale=0)，两页同步；清单刷新与补帧选择保留目标。12项UI检查通过并部署本机。缩放算法保留Lanczos，本机mpv HQ放大ewa_lanczossharp、缩小catmull_rom，仅核对比较未移植。

- 2026-10-01 15:45：旧导入倍率已确认会传给RVE输出缩放覆盖；TRT用户改2x/3x可用源于真实推理后缩放，旧字段混合了两种倍率语义。

- 2026-10-01 15:30：修复切换模型后倍率提示滞后；菜单保存新模型ID后刷新，旧列表入口也同步刷新工作台/图片提示。8项UI检查通过，完整构建并再次部署本机，DLL/EXE哈希一致，未自动启动宿主。

- 2026-10-01 15:18：按用户要求已将模型架构/输出倍率改动部署到本机3FUI测试；正式自更新返回UPDATE_COMPLETE|1.3.8，DLL/EXE与完整构建产物哈希一致，现有插件配置哈希未变，未自动启动宿主。

- 2026-10-01 15:10：模型架构家族统一与原生/输出倍率分离已完成；98项逐项审计，修正42项内置架构/输入约束/多倍率声明。详见[审计报告](../model-capability-audit.md)与[逐项证据](../model-capability-audit.json)。代码尚未提交；随后按用户要求已部署本机测试，版本号保持1.3.8，不发布远端。
- 新增 `-output-scale 1–8` 与工作台/图片页选择，默认原生；固定权重先原生推理再Lanczos缩放，FlashVSR可直接2x/4x。TensorRT缓存按推理倍率复用。旧用户路径、ID与能力记录不自动改写；重新检测先展示差异、载入修正窗口后由用户保存。

- 上一正式版本 **1.3.8 / 2026-10-01**：GitHub [v1.3.8](https://github.com/maxzrb/VideoEnhancer/releases/tag/v1.3.8)、ModelScope Releases及Models备用EXE已发布，标签指向源码提交 `2895ce8`；未覆盖旧版本。
- 本日下载/解压/缓存/WiX改动已纳入1.3.8；本地构建与门禁、11项双源文件实际下载哈希、双源故障回退均通过。宿主退出后经正式自更新入口部署本机，备份与EXE/DLL哈希已核对；未自动启动宿主，重启后的真实窗口观察仍由用户完成。
- Python Backend沿用 **2026.09.30.1**：29,709文件审计UNCHANGED，远端channel仍保留2条历史补丁，不发布空更新。RTX runtime、模型、配置与独立组件未更换。
- 教程缓存位于 `Plugin/videoenhancer/cache/TutorialImages`，迁移已知旧图片缓存，保留冲突/无关文件。下载完整异常进 `logs/downloads.log`，文件部署失败进 `logs/installer.log`；错误提示样式与五秒显示时长保持。
- 7z 使用便携 7-Zip 26.03 x64 多线程解压，SharpCompress 先做归档路径/链接预检，原生 CRC 保留；单运行 EXE 内含工具、许可证和对应源码。界面仅显示解压百分比，不显示文件名；取消轮询只在任务有标记时启用。
- 下载全部先单独完成后端安装，再按列表顺序最多三个模型任务并行、持续补位。取消或普通失败不阻断队列；离线/认证问题停止补位并等当前任务结束。手动重试加入队尾，重复点击去重，有空位立即开始，按文件最新状态统计；不自动重试。
- 下载全部运行时可点击停止全部，停止补位并取消当前可取消任务；后端替换事务不强杀，安全结束后不启动模型队列。分类下载采用相同队列规则。Windows Job 负责宿主退出后的下载子进程清理。
- 保留现有 WiX Burn 6.0.2 窗口；链仅含 Permanent EXE，Cache=remove，默认 Burn 日志关闭，/log 诊断保留。实测正常成功/正常失败后无本次载荷缓存、Bundle 缓存、临时卸载和依赖登记。不是 MSI；旧缓存及异常强杀/断电、Windows 自身记录不保证清零。

## Active TODO

- [x] 正式发行1.3.13并剥离四宫格专属代码；全部必要门禁、十项远端下载hash、单GitHub源码与本机完整升级验收通过。

- [x] PR #10/#11 审核及定向隔离验证完成，见 docs/pr10-pr11-review.md；此前图内缩放已生效，PR11补脚本分发及统计。已按用户授权完整合并，两份进度记录冲突均保留双方历史解决。
- [ ] 6GB/1080p/4x Engine保存校验OOM仍未解决；原9→3.9反馈缺同条件旧版A/B。用户停止测速的指令保持，本轮发行不追加长测速，后续大尺寸/长测速待明确指示。
- [x] 实时预览LakeUI兼容已合并PR11；组合版实际回调在5.110/5.112通过，未修复对照复现反馈异常。完整宿主绘制仍待实际使用验收；1.3.13已剥离四宫格专属代码。

- [x] 本轮实施与定向回归：补全许可/源码、FFF DLL 构建获取及独立分发、移除 SharpCompress、统一 7za；真实 API 11、ZIP/安装载荷、aria2 下载回归通过。
- [x] 本轮收尾：最终打包检查通过，649aeb9 提交推送，PR #10 已创建；issue #6 已附实下载源码/许可及回归证据后关闭。后续由维护者审查合并；本轮不发布或部署。

- [ ] 倍率术语复核：区分学习网络倍率与已编译图输出倍率；AVV3 NCNN2/3x实际为4x网络+图内缩小，之前“原生2/3x”表述不准确。现有图输出尺寸/缓存行为保持，尚未改产品元数据或界面。

- [x] 用户本轮 AVV3 TensorRT 倍率选择、GPU 图内输出与新版 LakeUI 预览入口兼容修复、FPS 初始帧偏移：实现、回归和本机部署完成。
- [ ] 本轮实际宿主可视预览复验；若继续追查相对1.3.0的差异或优化SVT吞吐，需同环境历史基线/单独资源配置对照，不擅自降低画质或改用户预设。

- [ ] 倍率术语复核：区分学习网络倍率与已编译图输出倍率；AVV3 NCNN2/3x实际为4x网络+图内缩小，之前“原生2/3x”表述不准确。现有图输出尺寸/缓存行为保持；2026-10-06已明确本轮TRT文案，其他后端术语仍待复核。

- [x] 用户确认RTX超分；已修复解码能力判断、硬解初始化/首帧失败回退、平面YUV转换及双显卡能力误判。RTX五输入及完整CLI FMP4通过；用户原片仍未取得，不扩大为所有老编码变体验收。

- [x] 全后端模型目标尺寸修复：TRT图内输出/缓存、CUDA/BasicVSR++原设备Lanczos4、CPU封装结果缩放、视频/图片/两种补帧顺序/跨后端/分块同步完成并定向验证。未做全模型逐项或旧新整链性能比较。
- [x] 正式分发1.3.11：本体双源/RTX独立包、远端hash、更新回退、本机正式升级完成。
- [x] 暂停已由用户发布1.3.12授权解除：黑边/老视频上传及SHA识别整合1.3.12，双源回读与本机更新通过；1.3.11修订不再另行覆盖或挪动标签。
- [x] RTX黑边构造复现与本地修复：可见矩形/偏移/软解完整crop五样本通过；原用户片未知，继续保留实际反馈核对。
- [ ] 老视频入口后续工作：四宫格补.rm/.rmvb、RVE元数据改可靠FFmpeg探测；不属于已确认RTX MPEG-4 Visual解码修复，尚未实施。

- [x] PR9+aria2组合已本机部署，用户反馈功能合理并授权合并/发布；不把该反馈扩大为全部GPU或历史专项验收。

- [ ] 本机重启3FUI测试能力修正弹窗/导入按钮宽度和模型菜单重复打开/切换分组后的悬停介绍、模型选择恢复原生倍率及导入重新检测；本轮已部署且哈希核验通过，未自动启动宿主。
- [ ] 维护清理：本机旧版三份载荷缓存约 59 MB；项目三份完整解压验证输出约 18 GB。后者曾两次被自动审批以 blocked by policy 拒绝，未绕过。确认归属与路径后再单独处理，勿清系统共享缓存。
- [ ] 历史发布验证补充：1.3.7 的 GitHub 大资产此前 CDN 下载超时，仅 API digest/大小核对；ModelScope 实际下载哈希通过。网络恢复后可补 GitHub 大资产实际下载校验。
- [ ] 长期可选工作：发行自动化与上游选择性同步清单；模型镜像逐文件来源/授权审计。项目自身 MIT 已落实，不沿用旧的“项目许可证未定”说法。
- [ ] 遗留专项验收记录：高 DPI/模型菜单/预览压力/四宫格完整交互、真实 TensorRT 缓存及组合视频专项在旧记录中未全部补最终验收；后续相关修改时定向复验。本次用户反馈仅关闭本轮下载交互验收，不代表全部历史专项完成。

旧预览目录、已发布 1.1.0、已合并 PR 和已被新后端取代的验证任务已退出当前 TODO，原始上下文保留在归档；不得按旧记录恢复到预览主线或旧后端。

## Recently Completed

- 2026-10-06 20:36：1.3.13正式发行、四宫格清理、单GitHub源码附件、运行镜像及本机完整升级全部完成，详见最新会话。

- 2026-10-05 17:55：第三方许可/固定对应源码与原创 MIT 范围、独立 FFF DLL、统一 7za、安全归档预检、独立源码 ZIP、安装器及 ZIP 一致性已完成。40 归档/110 打包/8 下载/6 后端事务/5 发行门禁与安装/自更新回滚通过。

- 2026-09-30：正式发布 1.3.7，教程图片滚动缓存优化；GitHub/ModelScope 发布记录已提交并推送。
- 2026-10-01：便携教程缓存、原生解压、取消和日志、WiX 安装残留收敛、解压百分比显示、持续补位与手动重试入队完成；本机已更新，用户反馈无明显问题。
- 2026-10-01：当前操作记录压缩、完整原文归档、导航更新；归档入Git后的字节/SHA256再次核对一致。
- 2026-10-01：正式发布1.3.8及1.3.9，均完成双源回读、故障回退和本机正式自更新；1.3.9包含模型能力/倍率/介绍与导入界面专项。

## Decisions

- 2026-10-06用户覆盖此前双源源码镜像约定：完整对应源码统一合并为一个独立 VideoEnhancer-<version>-source.zip，仅上传GitHub Release；ModelScope只上传运行文件、清单和说明，来源说明指向GitHub同版源码附件。旧发行资产不删除。

- 后续正式发行保留独立 VideoEnhancer-<version>-source.zip，供受GPL/LGPL/MS-RL约束的随包组件对应源码与构建材料获取；ZIP本身不是许可强制格式，若改用其他等效完整源码渠道，必须同步所有来源说明和发布检查。普通用户无需下载，运行EXE/ZIP/安装器不含第三方源码归档。本轮不补发旧发行、不上传附件，不改正式版本。

- 2026-10-05 17:55：用户已确认 LakeUI 赞助授权；按用户要求源码与二进制分包，保留必要 Python 运行资源和许可声明，不把第三方源码归档放入运行 EXE/安装器。C#/VB 原创为 MIT，RVE Python 内容为 AGPL-3.0-only。
- 2026-10-05 17:55：实际 FFF 8.20 DLL 为 API 12，纠正启动阶段按源码推定 API 11，锁定 8.19/c43614ca（真实 API 11）；独立 LGPL DLL 允许替换。7za Extra 不支持 RAR，支持列表与资源后缀同步。
- 2026-10-05 17:55：WiX 6.0.2 保持，补 MS-RL、OSMFEULA、源提交与完整源码。不假定营收或维护费授权状态，发布者按实际营收核对。用户授权 PR/issue，不扩大为新发行或部署。

- 2026-10-04 21:30：用户“部署到3fui我测试”明确授权本机覆盖部署，取代21:21未部署状态；不扩大为远端发布。先备份、检查进程退出，再部署并hash核对，保留配置/模型/独立下载组件。

- 2026-10-04 21:21：用户授权执行全部模型后端与RTX修复，并明确尽可能保持GPU处理。GPU Lanczos通过张量运算实现，不将CUDA一概改成双三次；TRT低倍率沿用旧转换图内双三次。保留真实权重倍率、Flash真实2/4x及分段独立倍率语义。NCNN/ONNX封装、Flash分块合并现有CPU边界如实保留。

- 2026-10-04 11:35：用户“功能合理，选择性安全合并并发布1.3.10”授权正式合并/推送/发行，取代此前仅本机试用限制。PR8保留aria2部分、排除倍率预设和控制台安装逻辑；PR9已合并。运行要求LakeUI5.110+（5.x），旧aria2自动更新时保持，需要安装器/手动包升级独立组件。

- 2026-10-04 10:46：用户授权仅选择性移植PR8的aria2升级并部署PR9组合，不整包合并PR8、不正式合并PR9，版本保持1.3.9、不发布远端。

- 2026-10-01 17:56：用户确认1.3.9刚发布，同版本替换影响小；旧1.3.9用户可等待下一版本正常更新，无需当前手动替换，不增加同版本自动更新机制。

- 当前根目录 main 是唯一开发主线；origin 为独立维护仓库，upstream 仅供选择性移植。不沿用旧 fork/origin 名称或 preview 工作目录。
- 保留 LakeUI 界面和当前 WiX 安装器；用户明确拒绝已有便携自解包交互窗口。
- 后端体积敏感，先单独安装；模型数量敏感，使用三并发滑动窗口。单项取消与停止全部分开，失败/取消不自动重试，手动重试可加入运行队列。
- 错误完整信息写插件日志；不改五秒错误提示，不展示解压文件名。减法针对运行期查询/轮询/输出开销，不宣称安装包更小。
- 保留后端增量路由、完整修复确认和事务回滚；半成品不标为安装成功，取消保留可重试缓存/断点。
- 项目 MIT 与第三方各自许可证并存；Aria2 Next Release 源码资产供许可证履行，普通用户不用另外安装该源码包。
- 跨后端/模型能力、转场阈值和分块语义以当前源码为准；历史被推翻的决定仅查归档，不从旧快照恢复默认值。

## Risks And Blockers

- 2026-10-05 17:55：私有 ModelScope 使用生产客户端与本机模拟服务/虚构令牌验证认证、401、拒绝重定向、原子临时文件；真实公共 HTTPS 已通过。未登录实际私有数据集，未完成本设备宿主/GPU预览。WiX 官方工具营收维护费条件已列入声明和发布门禁，用户尚未说明授权状态。
- 2026-10-05 17:55：旧安装需要完整 ZIP/安装器补齐独立 FFF DLL；单 EXE 更新仅保留既有独立组件。源码内嵌及体积的旧记录不再适用新布局；固定 SHA256 的 FFF 许可原文以 .gitattributes 保留字节。

- 1.3.8双源发布、实际下载和故障回退已验证；原始IO/end失败根因仍未确证，后续复发读取插件日志。
- 用户最初的 99% 五分钟后 IO/end 类失败未复现；原实现同包最终成功，不能把根因写成已确证。再次发生时读取插件日志。
- 原生工具/源码内嵌增加运行 EXE 约 2 MB；优化结果是解压耗时与运行开销降低，不是文件体积下降。
- 历史“无 NVIDIA/缺宿主程序集/只隐藏控制面板”等过时阻塞已移出当前状态；当前设备曾完成真实 GPU 验证，本轮也已有可用构建引用。换设备后仍须重查。
- 旧清理拒绝和 GitHub CDN 验证限制见 Active TODO；不将历史注册残留推测当实测事实。

## Environment Notes

- 2026-10-06 20:36当前maxzr设备/工作区C:/Codex Program/3fui plugin；正式安装C:/Program portable/3FUI/3FUI已为1.3.13，完整载荷补齐FFF独立DLL且保护项hash保持。备份C:\Codex Program\3fui plugin\Artifacts\.refactor-tmp\backup-before-release-1.3.13-20261006-203252；所有发布检查临时目录设在项目Artifacts/release-1.3.13/temp。本条优先于以下历史设备/安装记录。

- 2026-10-05 17:55：当前 Steve/Windows11、SDK10.0.401/runtime10.0.12；官方便携 Python3.12.10 在忽略目录 Artifacts/python-tests。生成 .NET 许可在 cli/obj，清除本轮误写源码目录的三个生成文件。没有沿用旧机器宿主/GPU路径。

- 2026-10-05 15:08当前设备为 Steve/Windows 11，工作区 E:/DesktopPlus/Works/Code/VideoEnhancer，.NET SDK 10.0.401、pwsh 7、Node、GitHub CLI 可用，Python 启动器未找到安装解释器。不得沿用原 maxzr 设备的安装/GPU路径。GitHub 凭据有效（sandbox 内误报，授权网络下复核成功）。

- 2026-10-04 21:30当前安装为1.3.10模型/RTX修复本地试用：CLI 25d20aba6475092b57fa267fbeab7aac873429f5f8ba63d54a390aecdb76b296，插件DLL 7398496759b5d9fa7eadff2b4fa4f5edec63281cce706c6f6a8afffc63eb5392，RTX b64761cd06f58517755964f5b6f797152c4abab14b31329057558028e0bcb844。备份 C:\Codex Program\3fui plugin\Artifacts\.refactor-tmp\backup-before-model-rtx-fix-20261004-212715；证据Artifacts/model-fix-build/local-deployment.json。下方正式版本hash为部署前历史。

- 2026-10-04 21:21本轮继续同设备：.NET10.0.400、MSVC14.51、Visual Studio18生成器；RTX3060 Laptop实机。隔离运行目录Artifacts/model-audit/runtime的backend是实际副本，python解释器及models部分为已核查链接；只更改项目内backend/测试文件，不覆盖真实安装。独立RTX源码位于Artifacts/rtx-backend-fix，基准c83df0f；FFmpeg/RTXSDK/CMake依赖使用本机已有只读位置，下次换设备重查，不作为便携配置。

- 2026-10-04 11:35当前本机正式1.3.10（3FUI6.2.35/LakeUI5.110），EXE hash `ac50014afde5b3e8f0181350f3c4f7ff079d72de15ec40de20d050b539516ac1`，DLL `c7f2fa9988d4941feb29a2b86e3977250c8f50e5032196c421261f96b953e1bd`，aria2-next2.8.3。备份`C:/Codex Program/3fui plugin/Artifacts/.refactor-tmp/backup-before-release-1.3.10-20261004-112800`；证据Artifacts/.refactor-tmp/release-1.3.10/local-deployment.json。以下旧试用/发行环境条目仅为历史参考。

- 2026-10-04 10:46：当前实际宿主6.2.35/LakeUI5.110，本机部署为PR9+aria2-next2.8.3试用1.3.9；下述旧HostBin/发行hash仅为历史。PR9构建无需HostBin。备份`C:/Codex Program/3fui plugin/Artifacts/.refactor-tmp/backup-before-pr9-aria2-20261004-104341`，证据`Artifacts/pr9-review/Artifacts/local-deployment.json`。
- 当前试用EXE SHA256 `2534d4fbb916374b68b262a87124af4c2a987a4264441e5cfdd7b21bb089108c`，DLL `139e74127b08518f3078eacb9c507098b7c9c121dd017e5be8074befb0c83bcb`，aria2 `08afaf2a44811d38e7ce538da719ab06d6925bcaad1231ee7b92c497f58e5aac`。

以下仅为本机已验证路径，不能当作跨设备配置。

- 工作区：`C:/Codex Program/3fui plugin`；Windows / PowerShell，.NET 10，文件读写 UTF-8。
- 本轮构建 HostBin：`C:/Users/maxzr/AppData/Local/Temp/3fui-core-compat-host`，LakeUI 5.9。实际宿主发布根不含可用 FFmpegFreeUI.dll，构建需显式 HostBin。
- 安装位置：`C:/Program portable/3FUI/3FUI`，插件位于其 `Plugin` 下。覆盖前检查 FFmpegFreeUI/videoenhancer 均退出，备份再复制并校验哈希。
- 最新发行部署备份：`C:/Codex Program/3fui plugin/Artifacts/.refactor-tmp/backup-3fui-before-1.3.9-8x-20261001-175231`，含替换前EXE/DLL与插件配置；证据Artifacts/.refactor-tmp/release-1.3.9-8x/local-deployment.json。
- 当前安装正式1.3.9（8x修订）：EXE SHA256 `98519EDFD9F25D25CFE151F38BC6AAD62763905025C0D9E5BDB3AD08CAED84DB`，DLL `1040B389AF9877EF3C6AF3E152B215F436B809B4E6A5DC5363147969685E2998`；升级前文件完整备份。CLI实际位于Plugin/videoenhancer/videoenhancer.exe。
- 构建获取固定版 Aria2 Next 与 7-Zip，校验哈希并打包许可/源码；上游曾返回 502，重试成功。构建脚本用 pwsh，不增加用户运行依赖。
- 历史 GPU 矩阵存在真实 RTX 3060 测试证据；换机器或验证 GPU 专项时重查硬件、驱动与运行库。认证状态也需现查，Token 不进仓库。

## Verification And Commands

- 2026-10-06 20:36：1.3.13必要本地门禁、110包检查、10远端实下载与本机45文件hash通过；Artifacts/release-1.3.13/checks-summary.txt、frozen-package.log、remote-verification.json、local-deployment.json。首次build日志中的安装hash拒绝为旧目录误读，已修正并由installer.log/updater.log/burn-installer.log验证通过。

本轮最终验证：

- dotnet publish VideoEnhancer.slnx -c Release --no-restore：0 警告/0 错误；release/test-third-party-package.ps1 最新 110 项通过（实际 FFF API 11、安装与 ZIP 全部 core 文件哈希、许可和源码分离）。
- release/test-native-archives.ps1 40 项、test-download-integration.ps1 8 项、test-backend-updater.ps1 6 项、test-release-gates.ps1 5 项，以及 test-installer.ps1/test-updater.ps1 安装、更新和回滚通过。真实 ModelScope 公共 HTTPS 内容哈希一致。
- 已发布 1.3.12 的 aria2 对应源码 SHA256=420E31256B5E29DE6AB9B295423ED29431494527B4FDC9AFB3946DFF4A73EAF7，实检完整 third_party 与构建脚本；旧发布资产不修改。验收范围详见 docs/third-party-license-audit.md。


截至本轮已完成的证据（不是每次文档整理都重跑）：

- 原版/新版同 3.07 GB 包解压：245.66s / 65.41s；29,709 文件集合、大小、SHA256 全部相同；原生完整性检查通过。
- 下载取消集成 6/6、原 UI 取消探针 6/6、后端事务 6/6、单 EXE 离线工具释放与 ZIP 许可/源码检查通过。
- 最终下载队列测试 11/11、真实编译插件配假 CLI 界面 5 场景通过；取消→重试→重复点击仅重试一次。
- WiX 真实外层安装正常成功/无效目录失败/故障回滚、默认无临时日志、精确缓存/卸载/依赖状态清理门禁通过；内层目录/许可证/哈希/回滚门禁通过。
- 最终 solution build/publish 0 警告/0 错误，git diff --check 通过；本机部署哈希一致。用户本轮反馈“感觉没啥问题了”。

常用命令（在项目根执行，HostBin 按设备核实）：

```powershell
dotnet publish VideoEnhancer.slnx -c Release "-p:HostBin=<可用宿主程序集目录>"
dotnet run --project release/tests/DownloadQueue/DownloadQueue.vbproj -c Release
dotnet run --project release/tests/DownloadQueueUi/Probe.csproj -c Release -- (Get-Location).Path "<可用宿主程序集目录>"
pwsh -NoProfile -File release/test-installer-burn.ps1
git diff --check
```

文档整理仅验证归档字节/SHA256、UTF-8、链接和记录结构，不重跑程序测试。程序验收与发行详见 `release/发布流程.md`。

## Git Sync

- 2026-10-06 20:36：main与origin/main已推送发行源码b97622a；v1.3.13注释标签解引用同一SHA，GitHub Release五资产正式公开。仅本次文档收尾待普通提交推送，完成后核对git status及远端SHA；不用改发行标签或重发冻结资产。

- 2026-10-06 19:34：两PR远端均MERGED，origin/main已包含两个合并提交；本条收尾记录将提交并普通推送，完成后核对工作树与远端SHA。普通Git操作，无force；原审核记录已提交81be99d，两个PR精确head作为合并父提交保留。验证worktree无跟踪文件变化。

- 2026-10-06 19:32：本地合并完成，准备普通推送origin/main，远端PR状态待核对。普通Git操作，无force；原审核记录已提交81be99d，两个PR精确head作为合并父提交保留。验证worktree无跟踪文件变化。

- 2026-10-06 18:00：git pull --ff-only已最新，main=f261e37与origin/main同步；fetch PR10/11至独立review refs，项目Artifacts内创建两个detached worktree，均无跟踪文件修改。主工作区仅STATUS/中文进度与新增审核报告，非干净；无合并/提交/推送/正式安装/发布。

- 2026-10-06 11:14：main=f261e37与origin/main同步，启动git pull --ff-only已最新；保留启动时两份记录未提交改动，本轮仅更新STATUS/中文进度。产品源码未改，测速/诊断构建在忽略目录Artifacts，未提交/推送/部署/发布。

- 2026-10-05 18:04：源码649aeb9已提交推送至 origin/fix/dependency-licenses-native-archives，目标 PR #10（maxzrb/main）OPEN/MERGEABLE、无托管检查；本地门禁已通过。issue #6 回读为 CLOSED/COMPLETED。记录提交前工作树干净，仅本次 STATUS/中文进度收尾待提交，随后推送复核本地与远端 HEAD 一致；无需在切换工具前再提交源码。

- 2026-10-05 17:55：分支 fix/dependency-licenses-native-archives，初始 git pull --ff-only 已成功，base f261e37；origin=SteveYu000 fork、PR/issue目标=maxzrb。上游 main 再核查仍为 base，所有当前变更均属本轮；准备提交推送，无 main 合并/发行操作。

- 2026-10-05 15:08：git pull --ff-only 已同步，开始时 main=f261e37 且干净；本机 origin=SteveYu000/VideoEnhancer（fork，拥有推送权限），目标 PR/issue=maxzrb/VideoEnhancer（只有读取权限）。本轮分支 fix/dependency-licenses-native-archives。普通 sandbox 不能写 FETCH_HEAD，按授权升级执行后成功。

- 2026-10-04 21:21：git pull --ff-only已最新，main=0664186跟踪origin/main；本轮源码/测试/两份审计报告/RTX补丁/记录未提交，工作树非干净，无push。Artifacts内构建/模型/测试夹具不纳入Git，切换工具或设备前建议考虑提交。

- 2026-10-04 11:35：PR8选择性5f1c9c0、PR9合并fb19abe、发行9bfba97及v1.3.10已推送。PR9状态MERGED，PR8选择性移植后CLOSED；不整包合并PR8。后续仅收尾文档提交，main与tag正常推送、无强推；冻结资产不纳入Git。

- 2026-10-04 10:46：main=5942b48同步origin/main；aria2选择性代码/许可/发布流程及审核记录未提交，工作树非干净。PR9 detached ae94b26仅附加同样aria2改动；未推送。

- Repository: 当前根目录；branch main 跟踪 origin/main。
- 最新发行修订提交：`b1cebcb fix: cap output scale at 8x for 1.3.9`，main及注释标签v1.3.9已同步origin；用户明确同版本替换授权，tag以精确lease更新，main未强推。首次发行0f38b64仍保留历史，收尾文档另作提交。
- origin：`https://github.com/maxzrb/VideoEnhancer.git`；upstream：`https://github.com/user-Wing/VideoEnhancer.git`。
- 所有前序源码、测试和归档已提交；发行产物、上传缓存与验证夹具留在忽略目录，不纳入Git。收尾提交后工作树应干净，切换设备前确认远端同步。

## Session Log

以下前三条是当日重要工作的压缩摘要；逐条原文与此前记录见归档。

### 2026-10-01 11:32 - Codex（归档摘要）

- 教程缓存移入插件，下载异常进插件日志，原生解压与取消完成并部署。同包解压65.41s、29,709哈希一致，原始失败未复现。
- 保留 WiX，Cache=remove、默认日志关闭；真实成功/失败清除临时登记和缓存。旧59MB缓存与约18GB测试输出未清，后者受自动审批拒绝。

### 2026-10-01 12:14 - Codex（归档摘要）

- 按用户要求保留后端先行，模型三并发持续补位；取消和普通失败不终止批量，离线/认证停止补位，新增停止全部及分类计数。
- 队列8/8、界面4场景通过，构建并部署；版本未改、代码未提交。

### 2026-10-01 12:41 - Codex（归档摘要）

- 手动重试加入运行队列尾部，去重与空位立即唤醒；取消/失败计数转回待下载，未执行排队项在结束后恢复下载入口。
- 队列11/11、界面5场景通过，publish0/0；最新DLL/EXE部署哈希一致，候选安装器/ZIP同步重建。未发布、未提交。

### 2026-10-01 12:48 - Codex

- Request/orientation：用户反馈本轮“感觉没啥问题了”并要求压缩归档过期 STATUS/工作进度；记录当前交互验收通过。同工具续作，读取 AGENTS/INDEX/STATUS 和 HandShake；git pull 已最新，保留全部未提交代码。
- Changes：完整冻结旧 STATUS 和工作进度到 docs/archive/records-2026-10-01，当前状态改为有效快照、去重TODO、最新Git/环境及压缩日志；工作进度保留当日成果和当前摘要，INDEX增加按需历史导航。版本迭代记录内容保持不变，无程序或版本修改。
- Verification：归档字节及SHA256与整理前一致，UTF-8可解码、导航链接有效，必须状态章节与时间戳存在；git diff --check通过。未重跑程序测试。
- Next/Git：本轮和前序源码/记录均未提交，main同步origin/main，工作树非干净；建议考虑提交，切换工具/设备前保存。清理与长期专项待办保留，未升级为本轮执行任务。

### 2026-10-01 12:51 - Codex

- 用户追加要求汇报未完成事项；以压缩后的Active TODO归纳：提交/未来发布收尾、历史缓存与测试输出清理、GitHub大资产下载验证、历史专项验收及长期自动化/来源审计。当前下载交互已获用户可用反馈，不重复列为待修复。
- 文档终检：10个本地链接、9个必需状态章节、2份归档SHA256、UTF-8读取均通过；旧原文字节一致、版本记录未变，STATUS与工作进度分别缩减97.7%和98.4%。main同步origin/main，工作树未提交，建议考虑提交。

### 2026-10-01 13:06 - Codex

- 用户授权按发布流程发布1.3.8；同工具续作，读取AGENTS/INDEX/STATUS、HandShake与发布流程，git pull已最新。保留前序改动并纳入本次发行。
- 版本源、分类Release Notes和版本记录更新；build-modelscope-release生成最终EXE/安装器/ZIP/源码/stable，build/publish均0警告0错误。Backend审计29,709文件UNCHANGED、版本保持2026.09.30.1。
- 验证：Python33/33，队列11/11，编译插件UI5场景，发布门禁5/5，后端更新器6/6，历史补丁5/5；内层安装器、自更新隔离、最终WiX外层成功/失败/回滚与精确缓存/登记清理通过。历史测试首次误传多文件apphost缺DLL，改用最终单EXE后通过，非产品失败。ZIP原生工具/源码与EXE哈希、stable大小/hash一致。
- Git：main原HEAD ef7bee8，前序源码和记录尚未提交；下一步提交推送、标记v1.3.8并发布五项资产，同步ModelScope后回读。不重新打包或覆盖1.3.7。

### 2026-10-01 13:15 - Codex：1.3.8发布收尾

- Release：https://github.com/maxzrb/VideoEnhancer/releases/tag/v1.3.8；源码2895ce8已推送main，注释标签v1.3.8同指向，正式稳定版、五项资产、逐行分类正文与本地一致。
- Mirrors：modelscope upload AerithDream/VideoEnhancer-Releases release/dist/modelscope --repo_type dataset（6文件提交、0失败、0删除）；Models同路径备用EXE使用--no-cache上传，未动模型/后端资产。
- 回读：GitHub五文件、ModelScope五文件及Models备用EXE共11项HTTP200/大小/实际下载SHA256全部一致；两份stable清单完全一致，README优先级正确。模型列表96项无重复，Plugin/videoenhancer.exe仅1项、大小正确。Backend通道latestVersion2026.09.30.1、两条补丁保持。
- 环境排障：首次Python requests GitHub证书校验因本机CA集合不足失败，改用已安装truststore读取Windows可信证书后全部通过，未关闭TLS校验；重复gh下载已停止。
- 故障回退：最终编译插件实测GitHub不存在仓库→ModelScope清单1.3.8；ModelScope无效数据集→GitHub包，最终SHA256正确。首个夹具误用无效GitHub配置格式同时影响包URL，改为真实404仓库后验证通过；产品未改。
- 本机：确认FFmpegFreeUI/videoenhancer退出，备份后用最终EXE --apply-update --wait-pid 0执行正式升级，UPDATE_COMPLETE|1.3.8；EXE/DLL哈希与发行产物一致。未自动启动3FUI，未重复GPU处理测试；当前设备历史RTX3060/TensorRT验证见归档。
- 证据：Artifacts/.refactor-tmp/release-1.3.8中的asset-hashes、remote-verification、remote-backend-channel、remote-models、local-deployment与故障回退夹具；本机备份C:/Codex Program/3fui plugin/Artifacts/.refactor-tmp/backup-3fui-before-release-1.3.8-20261001-131000。归档入Git字节/hash一致，冻结目录属性保留原换行/历史空白。
- aria2-next-2.5.6-source.tar.gz: 2493064 bytes / SHA256 `0a1e324cc8ddae583e3d3b18411594ee24f24d210ee7216cf11872e3454ccb72`。
- VideoEnhancer-1.3.8-manual-install.zip: 18601773 bytes / SHA256 `f38631bbe20d6b29afc7480003d3ddb60567630feebd1f7c7c1deed30bdc3128`。
- VideoEnhancer-1.3.8-win-x64.exe: 17303147 bytes / SHA256 `71d3f724fdae745a84d1b97afc5933f432ebe04561b7d04cf6845191bcad7e64`。
- VideoEnhancerInstaller-1.3.8-win-x64.exe: 16079246 bytes / SHA256 `bcbb86c4f5492ca8a55f94a288313e320677c569e814ff7756b49c279152342d`。
- stable.json: 1175 bytes / SHA256 `b2cfb285ff406f3fb6c9d6f367c864f68936222f4a0c71ae9c82ce24ec786d13`。
- Records/Git：STATUS、中文工作进度和版本记录更新；发行发布任务已完成，旧缓存/测试输出清理及历史专项/长期审计仍为独立待办。最后提交推送收尾文档并核对干净工作树。

### 2026-10-01 14:30 - Codex：模型能力专项启动

- 同工具续作，已读取AGENTS/INDEX/STATUS及HandShake；git pull --ff-only已最新，起始main工作树干净。
- 用户确认按真实架构家族归组，覆盖全部超分/修复模型，原生倍率与1–16x目标输出倍率分开；补帧不纳入。
- 本机Torch2.9.0+cu130/CUDA可用，ONNX1.22.0；本机模型只读，审计输出在Artifacts/model-audit。逐项98条读取完成，发现Compact/ESRGAN误归类、SPAN命名与内容不符、RRDB输入倍数不足，以及检测器架构字段为字符串时预检失败。
- 代码实施与定向验证进行中；未发布、未部署、未更改现有用户模型记录。

### 2026-10-01 15:10 - Codex：模型架构与倍率专项收尾

- Changes：统一architectureGroup；修正Compact/ESRGAN/RRDB/HAT/SPANPlus等实际归类与RRDB/DITN/CRAFT输入约束；模型ID、安装路径、文件名和权重保持不变。模型清单仍schemaVersion1，新增字段向后兼容。新增原生/目标输出倍率接口、同步界面、导入重新检测；1x修复模型不再被工作台排除。
- Detection：PTH由真实权重与描述器识别，处理字符串架构和AnimeSR专用类；ONNX架构来自图，22个动态图CPU探测输出倍率，不按文件名猜倍率；NCNN已审计图按param内容SHA256匹配，执行传真实param/bin基名，导入改目录名仍可用。检测缓存按文件路径/大小/修改时间，位于便携cache，旧用户清单不自动写回。
- Scaling：新增-output-scale1–16，默认原生；视频在最终编码滤镜末尾Lanczos缩放、图片保存前缩放；显式目标替代预设-s尺寸。旧-scale只接受真实推理倍率；FlashVSR记录支持2x/4x。图片后端接收原生倍率，不再自行猜已知模型；固定TensorRT权重缓存不因目标倍率新增Engine。RTX与分段继续各自输出规格。
- Files：cli能力清单/分组/检测缓存/NCNN图签名/倍率与参数/嵌入脚本；插件模型DTO/菜单/提示/导入管理/图片与工作台/队列配置；新增docs/model-capability-audit.md和.json、审计与定向运行脚本、C#倍率与UI探针；STATUS及中文工作进度。项目版本未改，版本迭代记录保持不变。
- Verification：98项结构/倍率核对无未识别或预检错误，42项清单修正。37个不同实际运行场景通过（分三轮25/12/8，基础4项复核去重）：CUDA/TRT图片与视频原生/2x/3x/4x、原生4xEngine复用、重命名PTH/ONNX/NCNN、保留旧人工记录、1x修复与TRT、BasicVSR++、FlashVSR直接2x、两种组合顺序与NCNN→CUDA跨后端、现有crop和-s最终输出尺寸。全部模型没有逐个GPU推理；结构审计和代表推理证据分开。
- Commands：python -m unittest discover -s cli/tests -p test_*.py（33/33；异常传播测试的预期traceback不是产品失败）；dotnet run cli/tests/ModelMetadata（倍率范围/滤镜/参数/分组）；dotnet run cli/tests/ModelMetadataUi（6项界面状态，不显示窗口）；solution Release build与最终CLI自包含裁剪publish均0警告0错误；git diff --check通过。最终代码变化只定向重验，不重跑下载/安装器/完整GPU矩阵。
- Environment/evidence：实际Torch2.9.0+cu130/CUDA可用、ONNX1.22.0；HostBin仍用本机已记录temp兼容宿主。隔离测试在Artifacts/model-audit，后端为实际复制，python解释器及各只读模型目录为本机junction，User和TensorRT-Cache均在隔离目录；本机路径仅作环境记录。初次测试FFmpeg仅复制EXE缺DLL，补齐隔离目录DLL后通过；VB变量Scale与继承成员冲突、ChrW命名以及裁剪JSON反射在实施中修正，最终编译无警告。
- Deployment/Git：未发布、未部署、未自动启动宿主；安装EXE SHA256仍为71D3F724FDAE745A84D1B97AFC5933F432EBE04561B7D04CF6845191BCAD7E64。main同步origin/main，但本轮代码/报告/记录未提交，工作树非干净；切换工具/设备前建议考虑git提交。原有长期TODO不变，专项实施已完成，下一步按用户指令决定提交或部署。
### 2026-10-01 15:18 - Codex：模型专项部署本机测试

- Authorization：用户明确要求“部署到我本地3fui测试”，覆盖先前不部署的默认限制；不发布远端、不递增版本、不自动启动宿主。
- Startup/Git：同工具续作，沿用已读AGENTS/INDEX/STATUS及HandShake；git pull --ff-only已最新，main跟踪origin/main，本轮实施改动保留未提交。
- Build：dotnet publish VideoEnhancer.slnx -c Release，HostBin沿用已核实兼容宿主目录；完整solution发布成功，更新EXE嵌入本轮插件DLL。附带本地打包产物没有上传或执行安装器，不重复下载/GPU压力测试。
- Deployment：确认FFmpegFreeUI/videoenhancer均退出；备份旧DLL、EXE与插件配置到`C:/Codex Program/3fui plugin/Artifacts/.refactor-tmp/backup-3fui-before-model-architecture-20261001-151724`；用Artifacts/videoenhancer.exe --apply-update --update-package <同一构建EXE> --update-target <本机Plugin目录> --wait-pid 0，返回UPDATE_COMPLETE|1.3.8。
- Verification：安装EXE SHA256 `F98B5B79BD20521E4E0A493FABABBBCB05EE9683DBEC78649E9E9AAE409C0221`，DLL `5047F0DC711329BCFD00CCF218B99E910269FC989A8AEDB89880CDC30858268A`，均与构建产物一致；现有videoenhancer.plugin.json哈希与备份一致，未手动改写用户能力记录或移动模型文件。部署证据Artifacts/model-audit/local-deployment.json；git diff --check通过（仅autocrlf提示）。
- Closeout：STATUS与中文工作进度更新，版本迭代记录不变。工作树非干净，代码/审计/记录仍未提交，建议本机测试通过后考虑git提交；下一步由用户启动3FUI检查模型分组、倍率菜单和重新检测交互。
### 2026-10-01 15:30 - Codex：模型切换自动更新倍率提示

- 同工具续作；读取AGENTS/INDEX/STATUS及HandShake，git pull --ff-only已最新，main跟踪origin/main；保留前序未提交修改。
- 原因：SetCatalogSelection先SyncOutputScaleControls后保存_config.Model，提示使用旧模型；旧OnModelSelected没有刷新提示。
- 修复：菜单先保存选择再刷新倍率控件；旧列表选择保存后同样刷新。工作台与图片页同步，未添加额外弹窗。变更文件为PluginPanel.UpscalePage.vb和cli/tests/ModelMetadataUi/Program.cs。
- 验证：插件Release build 0警告0错误；UI探针8项通过，新增实际模型切换2x→4x断言，立即展示“原生 4x；原生推理后缩放至 2x”，两个页面一致；完整solution publish成功，git diff --check通过。不重复GPU、下载或安装器测试。
- 部署：沿用用户本地测试授权，检查FFmpegFreeUI/videoenhancer退出，备份`C:/Codex Program/3fui plugin/Artifacts/.refactor-tmp/backup-3fui-before-scale-hint-20261001-153010`后正式--apply-update --wait-pid 0成功；安装EXE `3D7CCA4A37D0C76ED40B3CC32E021188A9C72F3987E385F4E2B3677AD5637E8B`、DLL `E96E772DAC75C293BB890E7453E66F3EADEE931011081F060AAE6365F791BC83`与构建产物一致，现有配置哈希未变，未启动宿主/发布远端/更改版本。
- 收尾：STATUS与中文工作进度更新；工作树非干净，本轮和前序修改尚未提交，建议测试通过后考虑git提交。下一步用户启动3FUI切换不同原生倍率模型检查即时提示。

### 2026-10-01 15:45 - Codex：确认旧导入倍率与TRT缩放机制

- 用户补充：同一内置AnimeVideo v3权重自行导入、修改记录倍率，在TensorRT使用2x/3x。只读核对HEAD旧Program/ModelCapabilityCatalog及本机实际RVE后端。
- 旧清单直接使用user.Scale；CLI将倍率传给--override_upscale_scale。RenderVideo由模型/Engine获取真实modelScale，在超分后resize_image_bytes调整目标尺寸；缩小采用INTER_AREA，放大采用INTER_LANCZOS4。因此旧导入倍率混用了原生与目标输出语义，用户反馈不能据此证明固定4x权重原生2x/3x。
- 本轮新增输出倍率是显式分离既有能力，而不是首次让TRT可以输出2x/3x；新版最终Lanczos与旧RVE缩小INTER_AREA不是完全相同的算法。未取得该用户运行日志，不能确认其当次Engine具体缓存状态。
- 未改产品代码、未再次部署/测试，main前序改动仍未提交；已更新中文进度，建议本机验收后考虑git提交。

### 2026-10-01 15:54 - Codex：模型选择恢复原生倍率与mpv算法核对

- 用户要求模型重新选择自动采用其原生倍率，并询问Lanczos与本机mpv变体。沿用AGENTS/INDEX/STATUS及HandShake，同工具续作，git pull --ff-only已最新；main前序改动保留未提交。
- Changes：SetCatalogSelection的保存超分选择分支及旧OnModelSelected设置OutputScale=0，随后保存并同步工作台/图片页。即使重新选择同一模型也恢复原生；清单刷新(saveConfig=False)与补帧选择不重置。
- Verification：插件Release build 0警告0错误；ModelMetadataUi 12项通过，涵盖恢复原生/两页同步/清单刷新保留/补帧不影响/显式修改后提示；完整solution publish成功，git diff --check通过；未重复GPU、下载或安装器测试。
- Algorithms：只读本机MPV Vanta Edition与mpv-full-private-v1.3.0的portable_config/mpv.conf和profiles.conf，两套启用HQ，scale=ewa_lanczossharp、dscale=catmull_rom、scale-antiring=0.5、sigmoid-upscaling=yes、correct-downscaling=yes、linear-downscaling=no。核对mpv官方manual stable：EWA为Jinc滤波，sharp有锐化；升降采样可分别设置，不是所有缩放均用同一Lanczos变体。建议当前输出仍保留Lanczos；未做画质A/B，不能宣称EWA必然更好。未修改本机mpv配置或产品缩放算法。
- Deployment：检查FFmpegFreeUI/videoenhancer退出，备份`C:/Codex Program/3fui plugin/Artifacts/.refactor-tmp/backup-3fui-before-native-reset-20261001-155334`后正式自更新UPDATE_COMPLETE|1.3.8。EXE `9A824BE308BFE4ED88EE0176FE2AFFDFD631F05C40086416728FBBC8A7A5709B`、DLL `955D9EFBD7A3EB82CBE5F1FD12ED9748220A2A9D5D4A9751021CD5D3F75F4AA9`与构建一致，插件配置哈希保持，证据Artifacts/model-audit/native-reset-deployment.json；未启动宿主或发布远端，版本不变。
- 收尾：STATUS与中文进度更新，代码/审计/记录尚未提交，工作树非干净；建议用户本机测试后考虑git提交。画质算法对比仅为建议，未新增为必须实施任务。

### 2026-10-01 16:24 - Codex：模型菜单悬停与公开介绍修正

- 启动：同工具续作，沿用已读AGENTS/HandShake，复读INDEX/STATUS；git pull --ff-only已最新，main跟踪origin/main，前序实施未提交。用户重复请求及“继续”均作为本任务续作。
- Lifecycle：核对项目暂存的LakeUI源码，FloatingToolTipForm会因关联控件/窗体状态调用Close并释放；原控制器保留readonly窗体且已显示项直接短路。改为延迟创建、关联当前菜单popup、失效/换分组时重建；只有实际可见且未释放才认为已显示；菜单关闭仍释放计时器/窗体/字体。不修改LakeUI或用户mpv。
- Descriptions：ModelDescriptionProvider重写并精简名称/倍率/来源重复。公开信息优先说明训练素材、处理问题与版本差异；纠正OpenProteus实拍、Nomos8k照片、AniSD AC/DC/DB/PS用途、AniScale2 DITN限制与Refiner顺序；解释GIMM RAFT/FlowFormer/LPIPS；删除FlashVSR不能与补帧组合的旧说法和无依据的架构画风/版本排名。RealHatGAN/fix导出、BHI/Sudo/ModernSpanimation训练差异仍待复核，不夸大支持。
- Sources：新增docs/model-introduction-sources.md，记录21组作者仓库/模型卡/论文与限制；OpenProteus发布正文用GitHub API补读。网络搜索曾有错误仓库路径/页面不可读，最终使用正确官方地址；未把未取得内容的链接当证据。98条实际文案导出Artifacts/model-audit/model-introductions.json，长度均不超过180字符，结构能力以先前权重审计为准。
- Files：PluginPanel.vb、PluginPanel.UpscalePage.vb、ModelDescriptionProvider.vb；ModelMetadataUi/Program.cs及新增Program.Tooltips.cs；公开依据文档、STATUS和中文进度。注释中文、UTF-8及源文件CRLF保持。
- Verification：Release插件build 0警告0错误；完整solution publish成功。--tooltips在不切换到前台的独立Windows测试桌面运行，实际菜单三次打开、提示自关释放后重建、切换popup、关闭释放共15项；7项关键文案与98条长度检查共8项，总23项通过。既有倍率UI探针12项通过；git diff --check通过，仅autocrlf提示。未启动本机宿主、移动用户鼠标或重复GPU/下载/安装器测试。初次STA已占窗口资源无法SetThreadDesktop，改新线程先关联桌面后运行，最终无警告。
- Deployment：沿用本地测试授权，确认FFmpegFreeUI/videoenhancer退出，备份`C:/Codex Program/3fui plugin/Artifacts/.refactor-tmp/backup-3fui-before-model-tooltips-20261001-162313`后正式自更新返回UPDATE_COMPLETE|1.3.8；EXE `C85DC315D40FF3D0312F46D326ADD29644516A47CF367F00F4ED9BE67EF1103F`、DLL `12E00DD13C5312865B99CCE97848FAD7517A560CAFEF02D5C5175A575E200A85`与完整构建一致，现有插件配置哈希未变。证据Artifacts/model-audit/tooltips-deployment.json；不自动启动宿主、不发布远端、版本不变。
- 收尾：记录已更新，工作树非干净，前序和本轮代码/文档尚未提交；建议用户在实际窗口检查悬停交互后考虑git提交。无新增执行阻塞，实际宿主观察仍由用户完成。

### 2026-10-01 16:42 - Codex：导入能力弹窗与按钮布局

- 启动：同工具续作，读取AGENTS/INDEX/STATUS及HandShake；git pull --ff-only已最新，main同步origin/main，保留前序未提交改动。
- 变更：PluginPanel.ModelImportPage.vb能力编辑窗口加宽、标签列180，路径/校验值64高，说明56与操作60各占独立行；内容高度与标题padding共同决定窗口高度，小屏用LakeUI垂直滚动。选择模型/文件夹按钮最小210，按实际字体文字宽度+48测量并随FontChanged更新。能力值与保存逻辑不变。
- 验证：Release插件build和完整solution publish均0警告0错误，git diff --check通过。临时Artifacts/model-audit/import-layout-probe验证实际模态弹窗9项通过：两按钮12/18pt留白4项、说明和保存/取消完整3项、六后端及双行路径2项。96DPI客户区820x660，内容818x624；实际高DPI仍由用户观察。独立桌面创建完整插件页停滞，仅终止自有测试进程后改STA普通桌面短暂弹窗验证；路径高度初始断言未计margin，改按真实双行字高。DrawToBitmap是黑图，不作为视觉证据。未增加正式测试或重复GPU/下载/安装器测试。
- 部署：确认宿主/CLI退出，备份旧DLL/配置到`C:/Codex Program/3fui plugin/Artifacts/.refactor-tmp/backup-3fui-before-import-layout-20261001-164012`；CLI备份和首次哈希路径误写为Plugin/videoenhancer.exe，非终止错误后正式自更新仍返回UPDATE_COMPLETE|1.3.8。按真实子目录重新验证EXE/DLL与构建一致，配置与更新前备份哈希一致。旧CLI未留存本轮备份，记录限制；没有重复更新。证据Artifacts/model-audit/import-layout-deployment.json；未自动启动3FUI、不发布远端、版本不变。
- 收尾：STATUS与中文进度更新，main同步origin/main但工作树非干净，前序/本轮均未提交；建议用户本机验证后考虑git提交。

### 2026-10-01 17:01 - Codex：长路径及SHA-256完整显示

- 同工具续作，复核INDEX/STATUS，沿用AGENTS及HandShake；git pull --ff-only已最新，main同步origin/main，前序未提交改动保留。用户截图确认上次行高断言不能证明完整显示。
- 变更：ModelImportPage两个只读长字段由LakeTextLabel换为LakeUI ModernTextBox，ReadOnly/MultiLine/WordWrap启用，完整文本可选择复制，多行滚动支持异常长值；字段各96高。固定说明56/按钮60行与LakeUI垂直滚动容器实际加入，窗口高度显式加标题/边框而不依赖Attach之后尚未生效的Padding。
- 纠正上次记录：先前机械替换的多行片段因CRLF匹配未落实，实际仅窗口宽高/标签列加大；16:42记录关于固定行/滚动的描述超出当时源码。本轮按源码片段明确替换并复读确认，不改写历史日志。
- 验证：插件Release build及最终solution publish均0警告0错误，git diff --check通过。临时UI探针17项通过：原9项布局加两个长字段各4项（只读换行、实际宽度与300px下每个字符均进入视觉行、完整选择）。测试使用真实弹窗/实际LakeUI视觉行，不以区域高度或黑色DrawToBitmap推断文本完整。96DPI窗口820x724、内容818x688。首次试改未命中原行配置，代码复读后落实；VB循环变量Height与控件属性冲突改rowHeight，窄宽度测试先SuspendLayout避免父布局重设，最终通过。未新增正式测试/重跑GPU或下载测试。
- 部署：宿主/CLI退出后完整备份到`C:/Codex Program/3fui plugin/Artifacts/.refactor-tmp/backup-3fui-before-import-details-20261001-170035`，正式自更新UPDATE_COMPLETE|1.3.8；EXE/DLL均与最终构建一致，插件配置与备份哈希一致，证据Artifacts/model-audit/import-details-deployment.json。未发布远端、未更改版本、不自动启动3FUI。
- 收尾：STATUS及中文进度更新；main同步远端但工作树非干净，代码与记录未提交。用户实际窗口查看仍待反馈，建议确认后考虑Git提交。

### 2026-10-01 17:17 - Codex：1.3.9发行启动

- 同工具续作，读取AGENTS/INDEX/STATUS及发布流程，沿用HandShake；git pull --ff-only已最新，main全部未提交变化属于本轮模型专项。
- 用户“发布1.3.9release”授权提交/推送/标签/双源正式发布与流程内本机升级，覆盖先前不发布限制。两项目Version设1.3.9、逐行分类Notes及版本历史保留完成；后端独立包须脚本审计，Python检测/图片桥更新为CLI嵌入资源。
- 前序模型结构/代表GPU验证沿用，运行最终发行所需门禁，不重复完整下载/安装器压力或完整GPU矩阵。下一步本地构建/验证→提交推送/发布→远端与本机升级校验。

### 2026-10-01 17:22 - Codex：1.3.9本地门禁

- build-modelscope-release生成最终五项资产；build/publish0警告0错误，CLI1.3.9与两项目版本一致，stable大小/hash、ZIP内DLL/EXE与构建一致，GPL源码hash正确。内层安装器、自更新成功及回滚通过。
- Python33/33与两项修改脚本py_compile、ModelMetadata倍率/分组参数、ModelMetadataUi12及悬停23、release门禁5与backend更新6通过；预期错误日志属于异常/回滚夹具。前序98模型结构与37种GPU代表场景、17导入布局沿用，本轮不重复GPU矩阵。
- Backend原已发布2026.09.30.1的29709文件逐项SHA256审计UNCHANGED，本轮桥脚本由CLI嵌入同步，独立完整包/channel不发布空更新。最终资产哈希保存在Artifacts/.refactor-tmp/release-1.3.9/asset-hashes.json。
- 接下来完成最终WiX正常/失败缓存门禁，提交本轮源码与版本记录并推送origin main/标签；GitHub发布后上传相同dist到ModelScope，禁止重新打包造成哈希变化。

### 2026-10-01 17:30 - Codex：1.3.9双源发行收尾

- Release：https://github.com/maxzrb/VideoEnhancer/releases/tag/v1.3.9；提交0f38b64已推送main，注释标签v1.3.9同指向。正式稳定版/latest，五项资产且分类正文与本地Notes完全一致。
- ModelScope：Releases目录39项中跳过33项，提交6项，0失败/0删除；Models备用Plugin/videoenhancer.exe用--no-cache同步，复用同hash对象，未动权重/Backend/channel。两份stable JSON完全相同，README下载优先级正确。
- 回读：GitHub五文件、ModelScope五文件及Models备用EXE共11项HTTP200、实际大小与SHA256均与冻结dist一致。模型远端96项、无重复/PotPlayer，备用EXE唯一且大小17312224。Backend线上latestVersion2026.09.30.1，两条历史补丁保持；独立包29709文件审计UNCHANGED，本轮辅助脚本由CLI嵌入更新，不发布空后端版本。
- 最终门禁：build/publish均0警告0错误，Python33/33和修改脚本py_compile、ModelMetadata倍率/分组、ModelMetadataUi12及悬停23、发布门禁5、后端更新6通过。build脚本内层安装/自更新成功和回滚通过；最终WiX外层成功/无效目录/回滚及精确缓存/注册清理通过；ZIP内EXE/DLL/aria2/许可与stable校验通过。前序98模型结构、37种不同GPU代表场景、17导入布局证据继续有效，未重跑完整GPU矩阵；本机有NVIDIA并已完成本轮实际TRT图片/视频代表推理，未宣称所有模型逐个GPU验收。
- 双源回退：用最终编译插件将GitHub检查配置指向真实404仓库，回退ModelScope读到1.3.9；将ModelScope下载数据集设无效，实际回退GitHub下载1.3.9且SHA256正确。测试只在项目隔离夹具，不改变真实用户配置。
- 本机升级：确认FFmpegFreeUI/videoenhancer退出，完整备份到`C:/Codex Program/3fui plugin/Artifacts/.refactor-tmp/backup-3fui-before-release-1.3.9-20261001-172635`，执行冻结发行EXE --apply-update --wait-pid 0返回UPDATE_COMPLETE|1.3.9；安装CLI --version=1.3.9，EXE/DLL哈希匹配，videoenhancer.plugin.json、models/User/model-catalog.json和aria2-next.exe哈希保持。未自动启动宿主，实际高DPI/窗口目视继续由用户反馈。
- 证据：Artifacts/.refactor-tmp/release-1.3.9中的asset-hashes、github-release、remote-verification、remote-models、remote-backend-channel、local-deployment、backend-audit与updater-probe；不纳入Git。
- 来源/风险：模型介绍公开依据和待复核项已写docs/model-introduction-sources.md；结构能力依据权重审计，本轮不新增/重传模型权重；模型再分发来源/授权逐文件复核保留长期TODO。原历史清理/其他专项待办不扩大为本轮任务。
- 收尾：STATUS、中文工作进度及版本迭代记录更新，发行任务已完成；main代码与标签同步origin，最后提交推送收尾文档并核对干净工作树，切换工具/设备前无需遗留未提交源码。

- 1.3.9发行资产：
  - aria2-next-2.5.6-source.tar.gz: 2493064 bytes / SHA256 `0a1e324cc8ddae583e3d3b18411594ee24f24d210ee7216cf11872e3454ccb72`。
  - VideoEnhancer-1.3.9-manual-install.zip: 18611136 bytes / SHA256 `869421a26080208a5f1e41892e2f55a7fec79ee6127d95f9a136888c39af845c`。
  - VideoEnhancer-1.3.9-win-x64.exe: 17312224 bytes / SHA256 `e5acdf9c681224d04421811f4b601066cc5d454adff172ed1880fc7eeacd0f94`。
  - VideoEnhancerInstaller-1.3.9-win-x64.exe: 16089960 bytes / SHA256 `121a0bb8eee61254d0e3da2d00ed779e64986648f45fdcc167be5f28ae094072`。
  - stable.json: 1592 bytes / SHA256 `61049243ef96f2b549837cd913ad3e2f339ee9a3beabe9f814ce61ad021b6e8e`。

### 2026-10-01 17:41 - Codex：1.3.9同版本8x修订启动

- 同工具续作，复核AGENTS/INDEX/STATUS，沿用HandShake与发布流程；git pull --ff-only已最新，起始main干净。用户明确授权替换1.3.9，同步GitHub资产、稳定清单、ModelScope Releases/Models及标签，保留旧版资产证据在Artifacts/.refactor-tmp/release-1.3.9-before-8x。
- CLI/图片桥目标范围设1–8，插件两页列表一致；OutputScale配置属性将旧9–16钳制8，队列/图片/工作台共用值。只改目标输出范围，不改模型原生scale或用户能力记录。定向8边界/9拒绝及旧配置UI测试已加入，尚待运行。

### 2026-10-01 17:47 - Codex：8x修订本地验证完成

- 范围：两页选项原生/1–8，CLI及图片桥同步；PluginConfig.OutputScale用属性钳制0–8，旧JSON16实际反序列化为8，队列和图片用同一值。模型原生scale、Engine缓存、用户模型能力清单不变。
- 验证：ModelMetadata接受8并拒绝9/16/17，UI15项通过（JSON迁移、两页9个选项、8边界、既有状态同步），图片桥py_compile，build/publish0警告0错误；最终安装/回滚、自更新/独立组件保留、ZIP DLL/EXE和stable大小/hash通过。Backend29709文件审计UNCHANGED，不重跑GPU/下载/完整WiX压力矩阵；此前完整门禁沿用。
- 文件：PluginConfig、UpscalePage、OutputScale、CliHelp、图片桥、倍率/UI探针、能力审计文档、Notes及状态/中文版本记录。PluginConfig原文件已有混合换行，机械插入首次造成新增CRLF尾白提示，按Git原始字节仅替换属性块保留未改行，diff检查最终通过；据最终原编码源码重新publish并冻结资产，未在上传后重包。
- 原资产保存Artifacts/.refactor-tmp/release-1.3.9-before-8x；修订证据Artifacts/.refactor-tmp/release-1.3.9-8x。用户明确同版本覆盖，下一步提交推送main、用精确tag lease更新v1.3.9、clobber相同五资产并更新正文，ModelScope同路径--no-cache替换。已有旧1.3.9客户端SemVer不会发现同版本，需要手动安装；本机会同步更新。

### 2026-10-01 17:54 - Codex：1.3.9同版本8x修订覆盖收尾

- Authorization/Git：用户明确“改完之后替换1.3.9release”，本轮不增版本；源码b1cebcb正常推送main，v1.3.9注释tag用force-with-lease只更新该标签。旧tag对象60b132e记录在previous-tag-object.txt，新对象275161c指向b1cebcb；没有强推main。原资产/正文保留在release-1.3.9-before-8x。
- Remote：GitHub同一Release五资产clobber，正文与target_commitish更新；先四包再stable。ModelScope用隔离目录仅含1.3.9及清单/README/Notes，--no-cache替换，7项提交、0失败/0删除；Models备用EXE同路径同步。旧其他版本、模型权重和独立Backend资产未更换。
- Verification：11文件（GitHub5、ModelScope5、Models备用1）实际HTTP200、大小/SHA256均匹配冻结8x修订产物；两份stable JSON一致、分类正文一致，后端2026.09.30.1两条历史补丁保持。实际编译插件的GitHub检查失败→ModelScope清单及ModelScope包失败→GitHub新hash下载通过。
- Product：CLI和图片桥目标1–8，工作台/图片选择原生+1–8共9项，PluginConfig载入旧JSON16→8且两页/队列使用同值。15项界面检查、CLI8接受/9以上拒绝、图片桥py_compile通过；build/publish0警告0错误、最终安装/回滚与自更新/独立组件保留、ZIP/清单hash通过。Backend29709文件逐项审计UNCHANGED。此前模型结构、代表GPU与完整WiX门禁沿用，不重复完整GPU/下载/安装压力。
- Local：检查宿主/CLI退出，备份`C:/Codex Program/3fui plugin/Artifacts/.refactor-tmp/backup-3fui-before-1.3.9-8x-20261001-175231`后用最终修订EXE正式--apply-update返回UPDATE_COMPLETE|1.3.9；安装EXE/DLLhash匹配，-h输出范围1–8。配置、models/User/model-catalog.json与aria2-next.exe哈希保持，不自动改能力记录、不启动宿主。旧高倍率只在新程序载入配置时归一，未在部署脚本改写配置。
- Assets/evidence：新EXE17312244字节/hash98519edf…，安装器16091564/hash253b5c4b…，ZIP18611086/hash2efb7398…，stable1665/hash9bbe3253…，GPL源码hash不变；完整hash见版本记录及Artifacts/.refactor-tmp/release-1.3.9-8x。构建末尾仅为保持PluginConfig原有混合换行重publish，最终产物重新冻结并验证后才上传，上传后未重新打包。
- Limitation：同版本SemVer不会触发已装旧1.3.9的自动更新，其他设备需手动安装修订版；本机已替换。用户选择此同版本覆盖策略，未扩展修改更新器语义。
- Closeout：STATUS/中文进度/版本记录更新；main与tag已推送，最后提交推送收尾记录并核对干净工作树。此前独立TODO不变。

### 2026-10-01 17:56 - Codex：确认旧1.3.9迁移策略

- 用户说明1.3.9刚发布，直接替换影响小；旧1.3.9等下一版即可。更新当前快照、持久决策及版本迁移说明，撤回前述要求立即手动更新的建议；保留历史日志说明当时记录。
- 仅文档修改，不改程序/资产/标签，不重跑构建或发布；git pull已最新，起始main干净。UTF-8和原换行保留，diff检查后提交推送文档，结束核对工作树。

### 2026-10-04 10:32 - Codex：PR #8/#9 审核与原版构建

- 读取AGENTS/INDEX/STATUS及HandShake，新会话；git pull --ff-only已最新，main=5942b48。gh默认识别upstream，发现后显式-R maxzrb/VideoEnhancer并核对URL，最终审核本仓库PR。
- 获取origin/pr-8及pr-9，项目Artifacts/pr9-review隔离检出ae94b26；原版publish成功，host-runtime（含真实宿主契约）、DPI、scroll通过；不重复GPU/下载/安装器全矩阵。
- 从实际单文件宿主只读提取FFmpegFreeUI/LakeUI到Artifacts/pr9-host-check，版本6.2.33/5.109；PR要求5.110，已向用户询问试用策略，未部署。PR8倍率方案被当前输出倍率覆盖、安装交互违背已确认方向；aria2可单独考虑。只读merge-tree检出5处冲突。
- 新增docs/pr8-pr9-review.md；main仅审核和状态记录变化，无源码合并、版本/远端发布或GitHub评论。待用户选择后完成本机备份部署和收尾。

### 2026-10-04 10:46 - Codex：aria2选择性移植及PR9本机部署

- 同工具续作，复读AGENTS/INDEX/STATUS并沿用HandShake；git pull --ff-only已最新，保留审核记录。用户明确授权只合aria2并试用PR9。只读提取升级宿主确认6.2.35/5.110，原兼容阻碍解除；GitHub API核对aria2 tag653e070b→f58a2d9和Windows官方digest。
- main与隔离PR9移植csproj的aria2版本/固定二进制与源码hash、源码下载/产物、依赖许可验证及两种安装载荷打包；SOURCE、THIRD-PARTY-NOTICES和DEPENDENCY-LICENSES同步，发布流程示例源码名更新。未引入倍率预设、convert_tensorrt、InstallerManager/InstallerBundle变化或移除WiX。
- 组合dotnet publish成功，插件/安装器0警告0错误；二进制和源码SHA256匹配，COPYING/AUTHORS与源码一致，13项依赖许可规范行末空白后相同；ZIP15项中的DLL/EXE/aria2/依赖许可与最终文件一致。aria2 --version2.8.3，实际下载GitHub stable.json1665字节/hash9bbe3253…与官方digest一致。升级后真实宿主契约通过，前轮DPI/scroll仍有效，未重跑GPU/全量安装器矩阵。
- 实施纠正：首个移植脚本在试用树工作目录执行，仅改试用树后第二路径不存在，改在项目根明确同步main；初次依赖许可严格比较失败原因仅行末空白，检查后规范比较通过；记录脚本初次JSON路径键使用正斜杠而证据含反斜杠，规范键后更新成功。均未影响安装结果。
- 宿主/CLI退出后备份`C:/Codex Program/3fui plugin/Artifacts/.refactor-tmp/backup-before-pr9-aria2-20261004-104341`，最终EXE正式--apply-update返回UPDATE_COMPLETE|1.3.9；自更新保留独立组件，随后显式替换aria2及随附许可。EXE/DLL/aria2哈希一致，配置及models/User/model-catalog.json哈希保持；证据Artifacts/pr9-review/Artifacts/local-deployment.json。未启动宿主、不动模型/Backend、不发布或变更版本。
- 收尾记录更新；main选择性代码和文档未提交，工作树非干净，PR9未合并。等待用户实际界面/真实任务反馈，建议考虑提交aria2选择性升级。

### 2026-10-04 11:15 - Codex：1.3.10整合与发行启动

- 同工具续作，复读AGENTS/INDEX/STATUS和发布流程，沿用HandShake；git pull --ff-only已最新。用户授权提交推送、选择性整合、1.3.10双源正式发行及流程内本机升级。
- PR8仅aria2相关代码/许可/记录提交5f1c9c0，未带入旧倍率和控制台安装。PR9合并ae94b26无冲突；比较已试用源码，仅aria2源码产物路径自动合并仍用旧变量，改ResolvedArtifactsDirectory恢复试用版本。PR9合并提交保留原分支 ancestry；PR8以选择性移植结案。
- GitHub/ModelScope认证可用，v1.3.10不存在，SDK10.0.400及磁盘空间可用。继续构建/必须发行门禁→提交标签/上传冻结资产→双源哈希回读/故障回退及本机部署，不扩大GPU全矩阵。

### 2026-10-04 11:25 - Codex：1.3.10本地门禁与冻结产物

- Release与插件/CLI版本一致，六类插件检查（宿主契约/DPI/scroll/appearance/tooltips/倍率）、DownloadQueueUi5场景通过；Python16项路径/控制静态检查通过。旧测试仍要求外部宿主Private=false/ExternallyResolved，修正为LakeUI编译包/不带运行库和无宿主Reference，非产品代码故障。
- build/publish0警告0错误；发布门禁5与后端更新6、内层安装/无效目录/回滚、自更新独立组件保留/回滚、最终WiX成功/无效目录/回滚及缓存/临时登记清理通过。ZIP15项，DLL/EXE/aria2/依赖许可hash一致，不含宿主/渲染DLL或外置布局JSON。不重跑完整GPU矩阵。
- 独立后端按上一发行目录逐项SHA256审计UNCHANGED，继续2026.09.30.1。aria2源码下载遇EOF且停滞，终止自有dotnet发布进程，改为存在源码缓存时复用并始终锁定hash；发布脚本复用构建产物再hash，防止重复下载。缓存恢复自已验证41,412,730字节归档，最终hash420e3125…匹配。重新完整构建成功，首次失败日志保留。
- 冻结五项资产hash在Artifacts/.refactor-tmp/release-1.3.10/asset-hashes.json；上传后不重包。接下来提交/推送/标签、GitHub及ModelScope同步、11文件实际hash和双源故障回退、本机正式升级。

### 2026-10-04 11:35 - Codex：1.3.10双源发行与收尾

- Git：main已推送5f1c9c0(aria2选择性)、fb19abe(PR9有祖先关系合并)、9bfba97(release1.3.10)，注释tagv1.3.10同指向9bfba97。PR9自动标记MERGED；PR8因排除其余行为仅关闭，不伪装整包合并，不发送评论。
- GitHub正式latest五资产及逐行分类正文正确；ModelScope隔离上传只含新版本/README/Notes/stable，7项提交、0删除，Models备用EXE同路径--no-cache复用hash。冻结后未重包，旧发行/模型/Backend channel不变。
- 验证：GitHub5/ModelScope5/Models1共11文件实际HTTP200、大小/SHA256与冻结资产一致，两份stable相同；模型96项，无PotPlayer/重复，备用EXE唯一且17,316,043字节。Backend线上2026.09.30.1与两条补丁保持，本地逐项审计UNCHANGED。
- 网络：首次Python下载GitHub资产read-timeout、插件包回退TLS握手EOF；ModelScope六文件当时通过。只重试GitHub失败项，用Windows curl实际下载通过，结果remote-verification.json11/11；插件故障回退先临时进程IPv6关闭重试成功（DNS本无IPv6），随后撤掉该变量在默认环境再次通过。没有关闭TLS验证或修改产品网络逻辑，原失败日志保留。
- 实际更新器：GitHub检查仓库404→ModelScope1.3.10；ModelScope包数据集404→GitHub1.3.10包且hash通过。夹具在项目Artifacts，不改真实配置。最终六类UI、队列5场景、Python16项、release5/backend6、内外层安装回滚/缓存清理与自更新通过；未重复GPU全矩阵，用户此前实际功能反馈合理。
- 本机：确认宿主/CLI退出，备份`C:/Codex Program/3fui plugin/Artifacts/.refactor-tmp/backup-before-release-1.3.10-20261004-112800`后正式--apply-update返回UPDATE_COMPLETE|1.3.10；显式补齐aria2/许可，EXE/DLL/aria2hash匹配、插件配置及models/User/model-catalog.json保持。未自动启动宿主，版本记录/中文进度/审核报告更新。
- 证据：Artifacts/.refactor-tmp/release-1.3.10中的asset-hashes、github-release、remote-verification、remote-models、remote-backend-channel、local-deployment、build-release、门禁/UI日志及updater-probe。长期TODO保持，不扩大本轮任务。完成收尾文档提交推送后核对干净工作树。

### 2026-10-04 19:21 - Codex：解码与AVV3倍率反馈核查

- 新会话，使用HandShake，读取AGENTS/INDEX/STATUS，git pull --ff-only已最新，起始main=0664186、工作树干净。
- 核查：cli/Program.cs BuildBackendArgs将原视频路径与FFmpeg路径传给RVE；部署后端src/FFmpegBuffers.py与项目Artifacts/model-audit/runtime副本SHA256一致（a8077454…），FFmpeg读取命令无hwaccel，CPU解码为rawvideo并通过stdout供模型使用。分段桥同样使用无hwaccel的FFmpeg解码管道，不要求预先转码。
- RTX与RVE分开：归档记录2026-09-14已对宽<320或高<240走软解/上传D3D11且边界专项通过，但本轮未检查sidecar完整源码或复现老视频，不能推断所有编码通用软解已支持。需用户失败后端、输入编码和错误日志再定位。
- AVV3：cli/model-capabilities.json明确NCNN 2/3/4x三个条目，PTH realesr-animevideov3为4x；DetectScale优先能力清单/图结构/检测，固定模型目标倍率通过OutputScale.Encoder在编码前Lanczos缩放。本地Compact实现由末层权重形状推导PixelShuffle倍率；改名不改变该PTH结构。用户旧版仅改后缀的行为尚未复现，不能据此否定其性能反馈。
- 下一步：待用户提供AVV3文件格式/模型名、旧新版本、软解失败时后端/日志，区分原生2x识别问题与4x后缩放性能问题，再形成定向实现方案；不恢复全局文件名覆盖权重倍率。
- 验证仅源码检查/部署后端文件hash核对，未运行GPU/性能测试，不宣称速度下降原因已确证。更新STATUS和中文进度，版本记录不变；没有提交/推送/部署/发布。本轮只新增两份记录修改，建议切换工具前考虑Git提交。

### 2026-10-04 19:33 - Codex：TRT图内缩放与旧编码兼容性核查

- 同工具继续HandShake，复读STATUS快照/最近日志，沿用AGENTS/INDEX；git pull --ff-only已最新，main=0664186，保留上一轮两份未提交记录。
- 用户澄清AVV3为TRT。核查v1.3.8 DetectScale文件名倍率解析、EnsureTensorRtEngine的outputScale参数以及现版模型检测/OutputScale.Encoder。部署convert_tensorrt.py与项目副本SHA256一致（db4d1187…），FinalOutputScale明确将模型输出用F.interpolate bicubic缩放后再导出；目标2x/3x的Engine可由同一原生4x权重构建，不是内嵌三套原生权重。
- 性能解释：旧版改后缀可将2x传入转换脚本、生成GPU图内缩放的2x输出Engine；新版目标倍率与原生倍率分离后，固定4x权重默认生成4x Engine，再在FFmpeg Lanczos缩小，向CPU/编码管道输出4x帧，像素数为2x输出的4倍。该变化具有明确源码依据，尚未用用户旧新参数计时，不宣称模型卷积计算量变为4倍或性能根因已实测锁定。普通RVE覆盖倍率为CPU OpenCV INTER_AREA缩小；TRT转换特例不同，纠正中间GPU缩放泛化推测。
- 在线主源：Real-ESRGAN官方inference_realesrgan.py明确AVV3 upscale=4；FFmpeg n7.1 allcodecs.c有RV10/20/30/40，allformats.c有RM解复用。实际本机兼容FFmpeg -decoders/-demuxers列出RM、RV10–40、MPEG1/2/4、WMV1/2/3及Cook。宿主Settings工作目录为空导致首次枚举命令失败，随后显式使用已知后备FFmpeg，未修改设置。
- 定向样本：从https://samples.ffmpeg.org/real/VC-RV40/spygames-2MB.rmvb下载，45秒超时仅887800/2097152字节前缀；保留证据、不当完整文件。前缀ffprobe为rv40/yuv420p/576x320；FFmpeg成功解4帧rgb24共2211840字节，等于576*320*3*4；部署Python OpenCV打开/首帧成功、fps23.9760437、报告帧数174722，不能作为实际完整帧数。结果Artifacts/feedback-decode-audit/result.json，未进入Git。
- 兼容性：模型接收RGB像素/张量而非RMVB压缩码流，编码支持取决于解码器。RVE RenderVideo实际使用OpenCVInfo读尺寸/fps/帧数，VideoInfo/RenderVideo部署文件hash与项目副本一致；老编码可能在此先失败或进度异常。QuadGridForm IsVideoFile和文件筛选漏.rm/.rmvb，SetVideo明确拒绝，因此该入口存在独立限制。没有用户原片，不能据此认定其具体报错原因。
- 下一步方案：保留原生4x元数据，对TRT低倍率输出恢复显式目标倍率的图内缩放及按实际输出倍率缓存；兼顾分块、组合补帧和画质算法差异，不恢复全局改名倍率覆盖。老视频优先补入口扩展名并替换OpenCV元数据依赖为可靠FFmpeg探测；用用户原片日志验证。当前用户要求调查，未实施/发布/部署/改版本。
- 记录：STATUS快照/待办及中文进度更新；验证仅上述源码/hash/4帧解码，未运行GPU矩阵/速度对比。本轮两份记录未提交，git diff --check通过，工作树非干净；建议考虑提交后切换工具。

### 2026-10-04 19:37 - Codex：其他模型倍率风险扩展审计

- 用户担心其他模型同类问题，同工具沿用HandShake/AGENTS/INDEX，复读STATUS快照和最近日志；git pull --ff-only已最新，保留两份记录修改，main=0664186。
- 全清单机器统计98项，其中TRT41项=4x11/2x28/1x2。检查普通视频、图片、同后端/跨后端组合、分段、FlashVSR及RTX的入口和缩放位置；新增docs/model-output-scale-path-audit.md，列出全部41项TRT候选范围和条件。
- 确认低目标倍率引擎构建/编码后缩放为通用路径，并非AVV3特判；TRT图片同类。先超后补目标只在编码时生效可导致补帧仍按原生高尺寸运行；需同步帧/预览/补帧缓存及分块拼接。FlashVSR声明2/4x已有直接推理选择；其他后端与TRT图内缩放回归区分，未宣称全部模型实际变慢。
- 下一步为统一TRT原生/Engine输出/目标倍率管理、缓存隔离和视频图片同步，涵盖分块/组合补帧及算法差异，不做AVV3名称补丁。用户本轮为风险核查，未实施产品代码修复，不改版本或发布/部署。
- 验证：清单JSON读取与逐项统计、源码路径核对及git diff --check；未进行GPU矩阵/逐模型性能测试。本轮报告与两份记录未提交，工作树非干净，建议考虑提交后切换工具。

### 2026-10-04 19:40 - Codex：CUDA/NCNN/ONNX及时序后端倍率核查

- 用户要求范围不限TRT，同工具沿用HandShake，复读STATUS/前序审计，git pull --ff-only已最新，保留已有三份文档修改，main=0664186。
- 逐后端统计CUDA47(1x2/2x33/4x12)、NCNN21(1x2/2x13/3x1/4x5)、ONNX28(1x2/2x23/4x3)、FlashVSR1、BasicVSR++1；声明可重叠，不作为额外唯一模型总量。
- 核对v1.3.8覆盖倍率参数、部署RenderVideo/Util普通CPU INTER_AREA缩放，以及主线目标只进FFmpeg滤镜，确认其他普通后端也存在位置/算法变化和原生高尺寸管道量。GPU→CPU是否增加须区分：普通旧路径也先回CPU，TRT旧版图内缩放单独支持；未泛化为全部旧版GPU缩放。
- 核对FlashVSR dispatcher将override scale传给runner并传node.main，2/4x为直接推理；其他目标仍后缩放。BasicVSR++根据is_low_res_input确定4/1x，原生片段numpy输出后进入RawVideoWriter，官方4x低目标须后缩放。
- 核对同后端先超后补按upscaleTimes初始化并在模型结果后执行override尺寸，跨后端先超后补中间视频无目标滤镜，当前低目标只最终编码生效，可增加补帧面积；先补后超不同。图片通用Pillow后缩放、时序桥原生输出也核查。
- 更新审计报告全后端表与统一方案；修复范围扩大为模型原生倍率、处理链帧尺寸、目标尺寸统一管理，各后端在后续处理前缩放一次，尽可能GPU侧，否则至少减少管道量；保留真实多倍率和算法差异，不只修TRT。
- 本轮源码审计/JSON统计，未改产品或运行GPU/性能测试；STATUS和中文进度已更新，git diff --check通过。报告与两份记录未提交，工作树非干净，建议考虑提交后切换工具。

### 2026-10-04 19:47 - Codex：MPEG-4 Visual/FMP4实际解码对照

- 用户提供失败源编码MPEG-4 Visual/XviD/DivX/FMP4，同工具沿用HandShake及AGENTS/INDEX；复读STATUS最新状态/日志，git pull --ff-only已最新，main=0664186，保留前序文档修改。
- 三个4帧640x360合成样本，FFmpeg mpeg4+FMP4/XVID/DIVX FourCC、yuv420p。部署后备FFmpeg和实际宿主FFmpeg8.1.1软解均成功，各RGB24字节2764800，OpenCV均正常640x360/24fps/4帧并首帧成功；不是用户原片或全部XviD特性矩阵。
- 实际安装CLI1.3.10：FMP4640x360普通NCNN AVV3-2x成功，输出1280x720/4帧；同源RTX VSR2x处理0帧、退出1、decoder send packet Invalid argument。192x128 FMP4低尺寸RTX成功，输出384x256/4帧。首次NCNN后端已完成渲染、CLI对既有Vulkan退出阶段异常有提示，未将其误认成此次解码失败，最终输出帧数完整。
- 读取对应已发行sidecar源码c83df0f：回退仅width<320或height<240，普通尺寸choose_d3d11_format无D3D11格式时返回NONE，没有编码能力回退。在线主源与本机对照支持RTX路由问题；用户失败后端尚未确认，已询问是否RTX/HDR及错误日志，不把样本复现直接等同用户原片。
- 网络：raw源码下载502，用gh contents API获取base64源码副本成功，未更改远端。诊断捕获CLI含混合控制台编码，保存UTF-8日志；关键英文错误/JSON与输出探测可读，不据乱码推断业务失败。
- 来源/证据：docs/mpeg4-visual-decode-audit.md；Artifacts/feedback-decode-audit的源码副本、mpeg4-decode-results.json、mpeg4-pipeline-results.json、三项实际处理日志与输出；产物不入Git。直接FFmpeg/模型验证与CLI输出均使用项目Artifacts，不修改用户配置或视频；CLI自身沿用已安装运行日志机制。
- 方案：独立RTX runtime按解码器硬解能力选择软解/上传，不仅按尺寸；初始化失败需明确重开软件解码边界，像素格式转换范围另检查。普通后端有问题则先看原片/日志而非添加模型编码限制。
- STATUS/中文进度和新增报告已更新；无产品修改、版本/发行/部署，不重跑完整模型矩阵。git diff --check通过，当前四份文档未提交，工作树非干净，建议考虑Git提交后切换工具。


### 2026-10-04 21:21 - Codex：模型结果端尺寸与RTX软解回退实施、实机验收

- 同工具继续HandShake，已复读AGENTS/INDEX/STATUS/skill，git pull --ff-only已最新，main=0664186；保留前序审计和未提交改动。用户明确授权执行修复，随后要求Lanczos尽可能留GPU。未请求重复批准，不扩展到发布或真实安装部署。
- 模型链：Program.cs把低目标输出传给TRT Engine构建/缓存，图片同步；普通模型统一走rve-ordered包装器，在结果进入插帧/预览/写队列前落实尺寸；独立精度对齐保留。新增rve_output_scale.py原设备Lanczos4，逐瓣累加限制临时显存；NCNN/ONNX的CPU输出用cv2 Lanczos4。Flash/Basic runner以内嵌脚本随CLI同步，Flash2/4x真实推理保持，Basic在回CPU前缩放。OutputScale.Encoder保留编码端最终尺寸兜底支持用户滤镜。
- 先补后超修复转场重复Frame共享导致原地改写，逐帧clone并按生成器顺序处理；新增共享帧回归。模型原生能力清单/权重不变，分段模式不套普通-output-scale。Flash原有CPU分块合并、NCNN/ONNX原生回传没有伪称消除。
- RTX：Artifacts/rtx-backend-fix中cpp枚举AVCodecHWConfig，硬解初始化或首帧失败重开软解；保留首帧探测所有流packet重放，软件帧转NV12/P010上传D3D11；补常见平面422/444/高位深。完整CLI最后发现双显卡capabilities探测默认集显误判，已对RTX/NVENC探测优先选择NVIDIA，与实际处理一致。可审查两文件diff存release/patches/rtx-video-decode-fallback.patch，reverse apply --check通过，README注明rve-patches/c83df0f。
- 验证：solution Release build/publish0警告0错误；GPU Lanczos2项（含4种目标尺寸、FP16设备/常量）通过，9项顺序/精度/错误退出测试通过（错误夹具traceback为预期），ModelMetadata参数/最终滤镜验证通过。RTX已有16项FfmpegTranscodePipeline单测通过。Python脚本语法编译通过，git diff --check通过。
- 实机模型：verify_model_catalog_runtime.py --scale-fix-only共17场景记录通过，六后端/图像/两顺序/跨后端/2x Engine；追加TRT32分块2x及目标6x、最终两顺序、完整RTX CLI共5项通过。测试先把2倍RIFE输出误预期8帧，源码与实际对照是4源帧+3间隙=7帧，已改断言，不改产品尾帧语义。新断言检查精确帧数。
- RTX新构建5种输入通过：FMP4 640x360、H264 high10/444/低尺寸软解上传；普通H264保持D3D11VA。输出均4帧、音频帧数不丢；能力JSON VSR/TrueHDR/H264NVENC/HEVCMain10为true（3060不支持AV1 NVENC）。最终CLI原调查FMP4输出1280x720/4帧成功。证据Artifacts/rtx-decode-fix-verification及model-audit/verification。不是所有历史XviD/DivX变体或用户原片验收。
- GPU缩放单步测速：3060 Laptop 4K FP16→1080p预热3/测10次平均11.793ms，峰值分配314154496字节，FP16相对CPU FP32最大误差0.0004884；只测缩放，未承诺整链提速比例或恢复用户旧版全部速度。Lanczos4不等同FFmpeg/Pillow参数/抗锯齿逐像素结果。
- 交付：Artifacts/model-fix-build内CLI、插件ZIP、安装器及rtx-video-runtime.zip，hash清单model-rtx-fix-assets.json。RTX源码补丁含在组件ZIP，相关报告更新；版本仍1.3.10，版本迭代记录不变。无提交/推送/部署/发布，实际安装保持原样。完成本轮授权修复，后续发布或部署再沿用这些可审查产物；建议考虑Git提交。


### 2026-10-04 21:30 - Codex：按用户授权部署3FUI试用

- 同工具续做，已复读AGENTS/INDEX/STATUS/HandShake启动规则；git pull --ff-only已最新，main=0664186，保留已有源码/测试/文档未提交改动。用户明确要求部署，无需另行确认。
- 本机进程检查：部署前后均未发现FFmpegFreeUI/3FUI/videoenhancer/vsr_backend运行。实际Plugin目录 C:/Program portable/3FUI/3FUI/Plugin，python/backend为实际目录。候选4资产逐项SHA256匹配model-rtx-fix-assets.json，手动ZIP内CLI与候选EXE一致。
- 备份：C:\Codex Program\3fui plugin\Artifacts\.refactor-tmp\backup-before-model-rtx-fix-20261004-212715。保存16项目标部署前文件及用户配置/模型清单/下载组件，deployment-before.json记录原hash和原不存在文件。项目内deploy-local.py只写明确Plugin目录内目标，未删除/搬动原目录。
- 部署：ZIP中的CLI及plugin/videoenhancer.3fui.dll；独立RTX包中的runtime EXE/DLL（DLL既有hash相同）；image/Flash/Basic/尺寸模块及1.3.10 embedded-tools下ordered/尺寸模块。16项目标hash匹配，用户videoenhancer.plugin.json、models/User/model-catalog.json、aria2-next和7za hash不变，权重不修改，未自动启动宿主。
- 验证：部署CLI -h为1.3.10；使用已部署CLI和RTX组件对项目FMP4 640x360样本运行2x、FFV1最终编码，成功输出1280x720、完整4帧。日志deployment-rtx-smoke.log、输出及local-deployment.json在Artifacts/model-fix-build；部署后再次逐项hash和保护文件核对通过。不重跑六后端GPU矩阵。
- 状态：本机试用已落实，等待用户从3FUI测试实际素材。项目/Backend远端版本和发布资产不变，无push/发布；STATUS、中文进度和两份审计的部署说明同步。工作树非干净、改动未提交，建议测试反馈后考虑Git提交。


### 2026-10-04 21:47 - Codex：AVV3官方倍率与NCNN图结构再核对

- 用户询问官方是否没有原生2x可选。沿用HandShake/当前部署状态，git pull --ff-only已最新、工作树保留既有未提交改动。仅查源和文档，不重跑GPU或修改部署。
- 在线主源：Real-ESRGAN docs/anime_video_model.md标X4（可用于1/2/3x）；inference_realesrgan.py的AVV3明确upscale4/netscale4；utils.py的outscale不同则cv2.INTER_LANCZOS4。确认官方这份PTH为4x网络+输出缩放；项目TRT GPU双三次不是官方具体算法指定。
- 实际读取项目隔离模型2x/3x/4x.param尾部：2/3x均Conv48通道、PixelShuffle4、Nearest残差4x相加后Interp0.5/0.75，4x无末端Interp。纠正先前把NCNN2/3x输出图称为原生2/3x网络的说法；官方NCNN README及src/main.cpp仅证明2/3/4x输出模型选择。没有证据把同一4x权重改后缀变成独立2x训练网络。
- 审计报告新增官方来源和图结构说明，STATUS快照/TODO与中文进度同步。当前模型图输出倍率检测及已部署TRT低倍率修复仍有效；新增术语复核TODO，不擅自改产品行为。无版本/发布/部署变更，无提交/推送；建议试用反馈后考虑Git提交。


### 2026-10-04 22:12 — Codex继续：1.3.11发行门禁与RTX组件更新入口

- 2026-10-04 22:12：用户已授权发布1.3.11，覆盖之前仅本机试用限制。版本源与分类说明已更新；补RTX安装版本标记及下载页“可更新”入口，旧用户升级本体后刷新列表即可下载覆盖组件。RTX独立版本2026.10.04.1，源提交6afb9a8。发行五资产已冻结，本地publish/安装与回滚/自更新/Burn/队列/宿主/滚动/Python11项/组件状态4场景通过；Python独立归档仍2026.09.30.1，29,709文件审计UNCHANGED，尺寸脚本作为CLI内嵌资源同步，不上传新Python包。即将提交推送和发布双源，远端回读及本机正式升级尚待完成。

- 修改：两版本源、release-notes.txt、DownloadInstallStatus.vb、ModelDownloadPage.vb、ModelDownloadManager.cs；保留本轮全后端/RTX修复。旧版无标记视作可更新，仅成功校验解压后记录版本；pending继续阻止误判。
- 验证：Artifacts/release-1.3.11内package/burn/release-gates/backend-updater/queue/queue-ui/host-runtime/scroll/python日志；组件状态probe通过旧无标记/旧版本/新版本/pending四场景。既有真实GPU和RTX五输入结果沿用，不重复全矩阵。
- Git：main同步0664186，工作树仅本轮预期修改；准备发行提交及标签。下一步上传RTX及本体双源、实际下载校验和本机正式更新。


### 2026-10-04 22:19 — Codex：1.3.11正式发行收尾

- 2026-10-04 22:19：**1.3.11正式发布完成**：[GitHub v1.3.11](https://github.com/maxzrb/VideoEnhancer/releases/tag/v1.3.11)、ModelScope Releases及Models备用EXE同步；源码/标签7ffeea2。RTX2026.10.04.1同步Models，源码6afb9a8/tag已推送。11本体远端实际下载hash+RTX归档hash+最新包筛选通过，双源故障回退通过；真实CLI覆盖旧RTX并记录版本通过。本机正式升级1.3.11及RTX，配置/用户清单/aria2/7zip保持，宿主未启动。用户新增要求：其他组件也须更新识别，随后独立实现，不修改已发布1.3.11资产。

- 本机证据Artifacts/release-1.3.11/local-deployment.json，备份C:\Codex Program\3fui plugin\Artifacts\.refactor-tmp\backup-before-release-1.3.11-20261004-221638。远端与升级证据verify-remote.log/remote-verification.json/remote-rtx.json/fallback-probe.log/rtx-upgrade.log/local-rtx-update.log。
- PythonBackend仍2026.09.30.1/channel历史2条补丁；GPU和RTX既有验证沿用。首个远端校验使用便携Python缺truststore，切换本机发布Python后11项通过。
- Git：源码7ffeea2、v1.3.11及RTX6afb9a8/标签已推送；本条文档收尾提交后继续用户组件更新需求。


### 2026-10-04 22:28 — Codex：通用组件更新识别及同版本覆盖授权

- 2026-10-04 22:28：用户“这些做完后覆盖11”明确授权同版本修订1.3.11，取代临时1.3.12开发分流，版本源已恢复1.3.11。通用Bin组件SHA256安装标记、旧安装版本待核验、同路径旧完整包/旧续传缓存失效已实现。组件状态22场景、真实下载/标记及旧缓存替换通过；0警告0错误。先前1.3.11五资产留存Artifacts/release-1.3.11-before-component，WiX已clean避免同版本载荷缓存；修订发行构建/门禁进行中，随后覆盖源码标签/GitHub/MS及本机。RTX独立包仍2026.10.04.1，不覆盖其权重/运行库。

- 修改DownloadInstallStatus/ModelDownloadPage/ModelDownloadManager：CLI列表包含sha256；组件只有核心文件+对应归档hash标记才认作最新，兼容初版1.3.11的RTX日期标记。失败/pending继续不认完成。旧组件无hash记录显示版本待核验，同路径hash改变显示可更新。下载前移除不同内容的完整旧包及旧身份续传缓存，当前身份继续断点。
- 验证release/tests/ComponentInstallStatus/Probe.vbproj：4组件各5状态+RTX两兼容状态，共22场景；真实CLI归档hash标记和旧缓存替换通过。初次在未部署aria2的build目录下载报缺依赖，复制项目内测试所需便携aria2后通过，不修改安装目录。
- 本体原版发布结果保留；本轮修订用户明确授权覆盖同版本，安装初版1.3.11的用户需安装器/ZIP手动覆盖，SemVer自动更新不触发。源码尚未提交修订。


### 2026-10-04 23:01 — Codex：暂停发行，复现并修复RTX编码填充黑边

- 2026-10-04 23:01：**RTX黑边已构造复现并完成本地修复验证，未部署/发布。**342×258可见画面/384×288编码画布的合法crop样本，原组件4x输出右/底黑边283274像素，2x也复现70646像素；源片正常解码无黑边。根因D3D11源矩形禁用，采样隐藏编码填充；硬解需显式可见矩形+crop偏移，软解保留并完整应用crop避免对齐残边。修复后五裁剪/非对齐输入黑像素0，既有五解码及16C++测试通过。反馈原片未知，不能断言完全同根因。报告docs/rtx-black-border-audit.md；候选RTX hash04fd467035699b97022ef955d4e593c2fb37297ca03bc011dc8b48dcc4fca83f。

- 用户补充老视频可能非标准尺寸，暂无原片参数。先10普通/非标准/8K/非方形像素输入无新增黑边，再构造合法SPS crop复现；完整CLI普通342×258无黑边，定位落在sidecar处理区域。首个CLI探针带-y触发-y/-n冲突，移除诊断命令-y后通过，未将无关参数问题扩展为产品修复。
- 源码Artifacts/rtx-backend-fix基于6afb9a8，修改ffmpeg_transcode_pipeline.cpp；主仓可审查patch release/patches/rtx-video-visible-rect.patch及验证cli/tests/verify_rtx_visible_rect.py、定位报告。保留原解码回退patch，不改已发布RTX2026.10.04.1。候选仅Artifacts/rtx-border-audit/runtime；修复首版左/上软解残10像素，通过禁用解码器自动crop并在CPU侧明确UNALIGNED应用后解决，最终五场景0黑像素。
- 组件SHA补充：DownloadInstallStatus.vb有远端hash时不接受仅日期标记，避免同日换RTX包漏更新；对应ComponentInstallStatus验证改为23场景。补丁逆向apply --check及git diff --check通过。
- 发行真实状态：9989899/v1.3.11修订GitHub资产已覆盖；gh release edit短SHA被422拒绝，改完整SHA后成功；ModelScope Releases与Models备用EXE均上传完成。用户暂停到达时上传已结束，暂停后无新的发布/部署。修订远端回读及本机覆盖未完成。当前本机仍初版1.3.11+原RTX2026.10.04.1（非本轮黑边候选）。
- Git：main HEAD9989899已同步，当前DownloadInstallStatus/组件测试/patch README与本轮新报告/黑边验证/patch未提交；RTX源码独立工作树也未提交。建议将诊断与修复提交后再恢复发行，不继续挪动已发布标签或覆盖远端直到用户恢复。

### 2026-10-04 23:49 Codex 同工具续接：老视频 RTX 再排查

- 启动遵循 AGENTS/HandShake：读取 INDEX、STATUS 和技能；主线 git pull 已同步，HEAD9989899，保留上一轮未提交更改。当前本机环境沿用已核验 MSVC/CMake/RTX SDK/FFmpeg 与便携 Python，执行限定项目产物目录。
- 源码修改：Artifacts/rtx-backend-fix/backend/src/video/ffmpeg/ffmpeg_transcode_pipeline.cpp 及 backend/tests/unit/ffmpeg_transcode_pipeline_tests.cpp；新增可提交镜像 release/patches/rtx-video-legacy-upload.patch、cli/tests/verify_rtx_legacy_decode.py、docs/rtx-legacy-decode-audit.md；README 补应用基准与不叠加说明。
- 对照复现：MPEG-4 343×259 为奇数尺寸上传失败；BGR24/灰度为未支持帧格式。改偶数内部纹理、保持GPU可见矩形，补整数RGB/PAL/灰度/打包YUV转换，不新增DLL。转换函数供定向单元验证调用。
- 命令与验证：cmake --build Artifacts/rtx-backend-fix/build --config Release -j6；19 C++单元通过；verify_rtx_legacy_decode.py 11样本通过；verify_rtx_decode_fallback.py 五样本、verify_rtx_visible_rect.py 五样本通过；CLI奇数MPEG4 4x输出1372×1036/4帧通过。日志在 Artifacts/rtx-legacy-audit。前期常量指针/内部链接构建错误已修正；最终构建及回归通过。
- TODO：原用户反馈无法确诊，待其组件更新情况/失败代码；恢复发行后打包独立RTX并验证下载更新、远端hash及本机安装。此前发布暂停保持，不覆盖当前安装或远端2026.10.04.1；新修复仅隔离runtime。
- Git：主线9989899及独立源码6afb9a8均有未提交改动，未推送；主线含此前黑边/同日期SHA补充，本轮保留。建议在继续或切换工具前提交源码与记录。

### 2026-10-05 00:00 Codex：1.3.12发行授权与准备

- 用户明确发布1.3.12，解除暂停；读取HandShake/AGENTS/INDEX/STATUS，主仓及RTX git pull最新，保留预期修改。两版本源改1.3.12，分类说明与README组件路径同步。发行步骤：冻结RTX和本体、门禁、提交标签/双源上传、远端hash及旧安装更新验证；不重跑全模型GPU矩阵。

### 2026-10-05 00:03 1.3.12候选冻结

- build/publish、安装/回滚、自更新通过；组件23场景、队列及UI、Python11项通过，Python后端逐文件UNCHANGED。Burn/host/scroll及发布门禁执行中。RTX源码2db5b02/标签videoenhancer-runtime-2026.10.04.2已本地提交；归档c7d44d8f59f866b0f236b0bea46efe9e34e810a7aad5b300d77ebe04219f9064，EXE690b0801859eb625fb32b7706d378046777c6fe5363433983eac522413396983，包内其他运行库保持。
- 本体五资产冻结于release/dist/modelscope/releases/1.3.12及stable.json；仅当前版本上传目录Artifacts/release-1.3.12/modelscope-publish，SHA清单asset-hashes.json。准备提交主线/标签，全部门禁通过后上传双源与RTX，实际下载hash和旧组件更新验收。

### 2026-10-05 00:12 Codex：1.3.12正式发行收尾

- 本体源码1b442ea/v1.3.12、RTX源码2db5b02/videoenhancer-runtime-2026.10.04.2均已提交推送；GitHub五资产/分类正文、ModelScope Releases当前版本五资产+stable/README、Models备用EXE及RTX独立包同步。不修改Python后端channel/模型权重，不覆盖1.3.11标签。
- 检查：build/publish0警告0错误；安装/自更新/回滚及Burn清理通过；组件23、队列5/UI5、host/scroll、Python11、release门禁5/backend updater6通过；前轮已通过RTX11老视频+五解码+五黑边+19C++验证沿用。Python后端逐文件审计UNCHANGED。
- 远端实际下载：五本体资产双源+Models备用EXE共11项SHA/大小一致，RTX包22777585字节/hash一致；GitHub发布正文、稳定清单和Backend历史两补丁保持核对通过。故障注入验证GitHub版本检查失败→ModelScope及ModelScope下载失败→GitHub通过。
- 本机更新：宿主/任务退出检查后备份，正式--apply-update安装1.3.12，真实--download-model覆盖旧RTX并写新归档SHA标记，配置/用户模型清单/aria2/7zip哈希不变。记录local-deployment.json、备份Artifacts/.refactor-tmp/backup-before-release-1.3.12-20261005-000807；模型列表仅一个最新RTX且SHA正确、单备用EXE、无PotPlayer。未启动宿主。
- 安装后RTX完整CLI MPEG4 343×259做4x输出1372×1036/4帧成功（installed-odd-4x.log/json）；首个探针误写样本文件名，纠正为mpeg4-odd.avi后通过，无产品额外修改。
- 证据：Artifacts/release-1.3.12/asset-hashes.json、verify-remote.log/remote-verification.json/remote-rtx.json、fallback-probe.log、local-update.log/local-rtx-update.log/model-list.json；新补丁保留可审查来源。反馈原片/日志仍未知，不宣称所有老视频均保证可用。
- Git：发布源码和标签已同步，主线及RTX工作树在收尾记录前干净；本条记录提交推送后复核。建议切换设备/工具前维持当前已提交状态。

### 2026-10-05 15:08 Codex：许可证与原生组件整改启动

- 启动读取 AGENTS.md、docs/codex/INDEX.md、STATUS.md；跨设备续接。Git 同步及权限已核查，保留 main，为用户指定四项建立功能分支，无已有用户改动。
- 发现 README 仍称 SharpCompress/aria2 2.5.6；实际 aria2 2.8.3 已独立安装并已有 GPL/源码资产。FFF Native API 11 当前 DLL 以 Base64 内嵌且缺依赖完整声明。官方最新 API 16 不兼容，选择固定 2026.8.20/API 11，构建读取发布容器而不执行播放器。
- 用户明确确认 LakeUI 赞助许可证已取得；采用该授权，不篡改上游 GPL 文本，不在仓库存私密编号。RVE 上游为 AGPL-3.0，Python 集成与可选运行库须单列许可，不能一概按项目 MIT。
- 下一步：独立 DLL/许可/源码获取与打包、7za 预检/解压/创建、定向回归、PR。当前未提交，建议阶段完成后考虑 git commit。

### 2026-10-05 15:15 Codex：RTX rawvideo/MP4-only 错误来源核查

- 新会话按HandShake读取AGENTS、INDEX、STATUS与技能；git pull --ff-only已最新，main f261e37，启动工作树干净。
- 用户提供错误完整文本，检索当前CLI/独立RTX源码和git log -S/git show：旧job_types.h在output.container非mp4时返回unsupported_container及该英文原文；ea16ce4已删除，c83df0f加入framePipePath，已发布RTX源码2db5b02含管道支持。CLI Program.cs主动传rawvideo和命名管道，由宿主FFmpeg最终编码。该错误发生于任务创建校验，不能归因输入MPEG-4解码或VSR驱动。
- 结论：错误证据指向旧组件与新CLI不匹配；用户实际加载路径/EXE hash未取得，不宣称已在用户设备确认或修复。建议插件1.3.12+下载页刷新并更新RTX组件；若仍失败核对FindBackend两候选，顶层旧EXE优先于runtime内EXE，不能只凭日期包名确认更新。
- 验证：核对既有Artifacts/release-1.3.12/installed-odd-4x.log，正式安装CLI+RTX此前已完成4帧rawvideo管道→宿主FFmpeg输出。未重复GPU测试，未修改产品代码/版本/安装/远端。命令含rg/git log/git show及UTF-8读取；git diff --check核对记录。
- 更新STATUS当前快照及中文工作进度。待用户更新反馈，必要时取实际加载路径/hash。收尾仅两记录未提交，工作树非干净，建议考虑Git提交后继续或切换工具。

### 2026-10-05 17:51 Codex：许可/独立原生组件/源码分包实施与回归

- 延续启动已读取的 AGENTS、INDEX、STATUS；初始 git pull --ff-only 成功且工作树干净，创建功能分支。现复核 Git 状态及上游 main=f261e37。用户 LakeUI 赞助授权和源码/二进制分包要求已落实，无需再次确认。
- 代码：删除 EmbeddedFffNativePayload.vb、ManagedArchiveExtractor.cs 和 SharpCompress 依赖/许可；增加 FffNativeRuntime.vb、共享 ThirdParty.props 与 acquire-fff-native.ps1，构建只解析官方容器白名单 DLL 并校验哈希，实际 API 11 固定 FFF 2026.8.19（8.20 实际为 API 12）。DLL 位于 bin/fff-native-11，保留 LGPL 用户替换能力。
- NativeSevenZipExtractor.cs 统一清单/解压/7z 创建，覆盖压缩 TAR、Unicode、多线程进度、取消、CRC失败；写盘前拒绝越界/Windows特殊路径/符号硬链接/重解析点/加密/冲突。支持列表与帮助统一，不再宣称支持 RAR。
- 分发与许可：csproj、create-source-package.ps1、build-modelscope-release.ps1 生成/版本化独立源码 ZIP，运行 EXE/二进制 ZIP/安装器不带源码归档；保留必要 Python 运行资源、许可和源码说明。补 FFF 所有动态依赖许可、FriBidi/vcpkg 原始源码与补丁、LakeUI 赞助来源、RVE AGPL-3.0-only、WiX MS-RL/OSMFEULA及完整对应源码、实际 .NET pack 许可。修改 LICENSE-SCOPE、README/手动说明/发布流程/第三方审计，旧安装 RTF 声明也移除 SharpCompress。
- 下载：测试发现 aria2-next 默认 SQLite 状态在当前环境写入受限、HTTP下载成功后 BT 公网监听拖住退出。使用独立插件 cache state-dir、关闭用户全局配置；仅 HTTP/HTTPS 分支绑定 loopback并关闭 DHT/LPD/端口映射，不改官方 GPL 二进制。真实公共 ModelScope HTTPS 成功且哈希一致。
- 验证：归档 40、打包 110、下载 8、后端事务 6、发布门禁 5 项通过；安装、真实内层载荷、自更新/回滚通过；上一轮 publish 0 警告/0 错误。私有下载仅模拟服务协议验收，宿主/GPU播放未测。已发布 1.3.12 aria2 源码资产实检包含 third_party 与上游构建脚本，可据方案 B 核查 issue #6。
- 环境与修正：UTF-8 原编码/BOM 保留；.NET10.0.401/runtime10.0.12，测试解释器仅 Artifacts/python-tests。.NET 许可生成目录改入 cli/obj，清除本轮误生成的 cli/third-party/dotnet 三文件。所有缓存/夹具/发布候选保持忽略，不保存私密凭据。
- 下一步：最终 publish 与包检查，提交推送并创建 PR，评论核查结果和按证据关闭 issue #6，再提交收尾记录。未改版本、发布或部署；建议阶段完成后考虑 git commit，本轮已由用户授权直接提交。

### 2026-10-05 17:55 Codex：最终包检查与提交准备

- 最新全 solution publish 成功（0 警告/0 错误），独立源码包生成；最新 test-third-party-package.ps1 110 项全部通过，真实 FFF API 11、内层安装与手动 ZIP 每个 core 文件哈希一致。
- 修正 .NET 许可目录早期 MSBuild 属性为空导致的源码目录生成问题，改为绝对 cli/obj/third-party/dotnet/Release/win-x64；构建复核无 cli/third-party/dotnet。旧 InstallerNotice.rtf 移除 SharpCompress，明确 MIT 的 C#/VB 范围。README 产物列表加入独立源码 ZIP。
- .gitattributes 对固定校验的 FFF 许可原文禁用换行转换，并把该文件纳入源码包，避免 Windows 新检出后 SHA256 不一致。未改变上游许可原文字节。
- 状态与中文进度已更新，版本不变。下一步提交推送/PR/issue核查收尾；建议继续前考虑 Git 提交，本轮已获用户直接提交授权。

### 2026-10-05 18:04 Codex：PR #10 与 issue #6 核查关闭收尾

- Git：git diff --cached --check通过，源码提交649aeb98691472ea2404b7f27b82bde703951ce2已推送至 SteveYu000 fork 的 fix/dependency-licenses-native-archives。十份固定哈希上游许可经暂存和 core.autocrlf=true 实际检出后 SHA256 均一致；两份上游法律文本原有末尾空白依属性保留。源码 ZIP 更新至183项，与二进制分离。
- PR：gh pr create --repo maxzrb/VideoEnhancer --base main --head SteveYu000:fix/dependency-licenses-native-archives --body-file 创建 https://github.com/maxzrb/VideoEnhancer/pull/10；回读 OPEN、非草稿、MERGEABLE、head=649aeb9，无托管CI检查。正文记录许可证边界、LakeUI已获赞助授权、独立DLL/API11、7za/RAR限制、源包分离、各项回归与私有认证/GPU验收边界。
- issue #6：实核当前主线及已发布1.3.12 ZIP/aria2对应源码，采用方案B；既有发行已经包含GPL/作者/来源/精确哈希与完整third_party和构建脚本。评论 https://github.com/maxzrb/VideoEnhancer/issues/6#issuecomment-5992264852 明确现有许可问题已整改、本轮额外下载加固/分包仍待PR合并。gh issue close --reason completed成功，回读CLOSED/COMPLETED。
- 验证范围：最终publish0警告0错误；40归档/110包检查/8下载/6后端事务/5发布门禁，安装/自更新回滚通过，真实公共ModelScope HTTPS哈希一致。私有下载为本机模拟协议验证；本设备未启动宿主或做GPU播放。不重复与此次收尾无关的模型矩阵。
- 授权与后续：用户明确授权PR和已解决issue的关闭，均已执行；没有合并main、版本变化、发行上传或本机部署。WiX官方工具营收维护费条件仍按发布者实际情况核对，已进入公开声明/发行门禁，不推定其已获付费协议。项目既有其他TODO保持，不扩大本轮范围。
- 记录：更新本文件当前快照/TODO/Git状态并在version/工作进度.md追加中文收尾；版本迭代记录不变。当前仅两份收尾记录待提交，随即提交推送后确认工作树干净。切换工具/设备前本轮无需追加源码提交，后续新修改仍按项目要求考虑git commit。

### 2026-10-06 09:27 Codex：上游同步、TRT倍率与LakeUI预览修复，本机部署与持续性能复验

- 同工具新请求：用户要求拉maxzrb最新，修复AVV3倍率与PixelPictureBox.set_Image异常，调查原生2x持续降速。先查轻量历史索引及AGENTS/INDEX/STATUS/README，核对本机安装/远端；旧feature干净，git pull --ff-only已最新，git fetch origin取得f261e37，从origin/main建立fix/trt-native-lakeui-20261006。旧feat/trt-portable-aria2-20261001与backup分支保留，无reset、stash、合并旧PR8或恢复旧安装器。
- 用户确认AVV3保留官方4x权重、优化GPU内2x/3x输出，不换权重。本机已为1.3.12，上游已有按目标倍率构建/调度路径；本轮将convert_tensorrt.py作为CLI资源同步，更新模型显示名与两页输出提示，不改模型ID/原生倍率、缓存schema、用户记录或权重。新转换器真实构建80×64的2/3/4x Engine，目标2/3x I/O shape已核实；6x复用4x Engine后缩放，四项各12帧输出正确。
- 预览根因：新版LakeUI5.112取消PixelPictureBox.Image入口，旧引用在回调JIT时抛MissingMethodException。新增PixelPreviewImage兼容Image/Source；新版用缓存的回调工厂调用D3D DrawImage，图片由调用方管理。PreviewPage、PluginPanel释放与QuadGridForm全部替换该setter，换源后释放旧图，失败显示状态；没有改控件几何。
- 性能：初次96帧NVENC短测为9–10fps，未复现4fps。用户补充增强模式与完整SVT参数后，将OP原片视频流复制前960帧进行持续复验，并记录GPU温度/频率/功耗/显存/限制标记、超分与实际编码计数、编码CPU与工作集。NVENC渲染96.81秒/9.92fps，用户SVT完整参数256.29秒/3.75fps，实测输出3840×2160/yuv444p10le/960帧。SVT缓冲填充后编码反压超分、GPU明显空闲；没有擅自改变画质参数，没有宣称恢复同参数9fps或排除所有1.3.0历史差异。详见docs/trt-preview-performance-20261006.md及本机verification日志/CSV。
- 附带定向修复实际FPS统计错误：首条进度计时重置却包含先前帧，修正为首条之后帧数差，ETA同步；不是GPU或编码吞吐提升。FpsTracker Probe验证起点帧偏移、暂停与ETA；ModelMetadataUi18项覆盖原生/2/3x GPU提示/>4x/两页同步，PixelPreview Probe在5.110及5.112均实际调用插件回调并验证替换、清空与所有权；host契约与38项Python通过。
- 构建：dotnet publish VideoEnhancer.slnx -c Release成功，CLI/插件版本仍1.3.12，未改版本迭代记录。test-updater与test-installer哈希/保留组件/回滚通过。第一次安装器测试与构建并行未找到正在重建文件，等待完成后通过；小样本NVENC因低于最小尺寸失败，复验改80×64+FFV1，第二次失败为先前空输出已存在，改独立路径并按CLI约定末尾-y，不修改产品绕过。
- 本机：确认FFmpegFreeUI/VideoEnhancer任务退出后，备份EXE/DLL/转换脚本及配置至Plugin/videoenhancer/.work/backups/20261006-before-trt-preview-fix，再由新EXE --apply-update --wait-pid 0部署，不启动宿主。最后本机EXE SHA09B64D97…、DLL SHA A85C1D96…、转换器SHA D1E3CBA1…均与最终产物一致；配置SHA FDEAC061…备份前后保持。模型与其他运行组件未替换，新增Engine仅用于本轮验证。
- 收尾：README、复验报告、STATUS快照/TODO与中文进度同步；git diff --check通过。工作树含本轮未提交源码/测试/文档；未commit/push/PR/Release操作。建议试用实际宿主预览后考虑提交，跨设备/工具前保留本机证据；后续SVT资源优化或历史对比须单独保留原参数基线。

### 2026-10-06 09:34 Codex：SVT preset10持续对照（实际preset9）

- 同工具续做，读取AGENTS/INDEX、STATUS快照/最近会话、README与已有复验报告；轻量历史索引仅作定位，实时状态以本机为准。git pull --ff-only已最新，保留上一轮所有未提交源码/测试/文档；确认无3FUI/VideoEnhancer/FFmpeg任务再测试，不改代码、版本、安装或用户预设。
- 使用release/tests/MeasureTensorRt.ps1及同一op-960.mkv、AnimeJaNai原生2x、TRT FP16、不分块。由p6日志取得实际编码参数，仅将-preset:v:0 6改为10，保留CRF12/yuv444p10le/完整svtav1-params与映射相关选项；输出及日志使用独立fixed-janai-svt-p10-960命名，不覆盖p6证据。
- SVT4.2.0日志明确4K以上Random Access最高M9，自动将请求M10改M9；屏幕内容工具按M9关闭、film grain快preset开销警告仍保留，未移除用户film-grain4。处理中约9.3–9.5fps，渲染106.72秒/960帧=9.00fps，最终FFmpeg计数960、9.05fps；103.04秒超分842/编码724，积压约118而不是p6约470帧。GPU多约90%–97%，工作集约12GB、PPCS156，无热降频标记；该片段能跟上模型，没有跌至4fps，不能代替整片长测。
- ffprobe -count_frames完整确认AV1/3840×2160/yuv444p10le/960帧，文件约232.86MB；配置SHA FDEAC061…未变。只读git show v1.3.0确认旧FPS公式也未减首次计数；没有运行旧版、旧FFmpeg对照，不据此认定旧反馈只是虚高。用户强调旧版本p6可维持速度的差异保持待查，需后续同环境基线与真实格式/参数核对。
- README、复验报告、STATUS与中文进度追加，git diff --check通过；工作树仍含之前未提交修改，未commit/push/PR/Release。建议后续版本对照前考虑提交现有修复和证据，不将本轮快速预设试验当作产品修复。

### 2026-10-06 10:07 Codex：SVT参数逐项短片对照与CDEF自动模式

- 同工具续做，已读AGENTS/INDEX、STATUS快照/最近会话及README；主目录无project/changelog。git pull --ff-only已最新，保留既有本地修复与所有dirty文件。轻量历史查询仅用于测速取证和保留用户参数的原则，实际参数/版本/格式重新核验；本机已打开3FUI但无其他编码任务，不关闭或覆盖宿主。
- 用户确认preset6/CRF12不变，明确不压完整片；直接读取D:/RebellionHEVC444p10-700MBPS-SoftwareLast.mkv（HEVC/3840×2160/yuv444p10le/24000/1001），每组开头240帧、仅视频，不经过超分管道。新增MeasureSvtParameters.ps1，串行运行11组，记录真实FFmpeg benchmark rtime、frame/end、每五秒工作集、CSV与独立输出，不覆写已有证据。完整串没有旧参数complex-hvs1，快速对照也保留keyint1025/scd-min33。
- 结果：完整50.778秒/4.726fps，快速20.326秒/11.808fps；关闭AC47.222秒/5.082、DLF1为47.922秒/5.008、QM0为48.598秒/4.938、TF1为49.344秒/4.864、variance0为46.574秒/5.153、删DLF48.904秒/4.908。仅删CDEF23.995秒/10.002fps；快速仅加CDEF1为49.208秒/4.877fps，双向验证主要影响项。用户追加enable-cdef=-1测试，只改这一项22.454秒/10.689fps，约2.26倍；删项与-1的小差异未重复统计，不宣称不同自动模式或普适速度。
- 核对官方4.2.0参数解析到整数cdef_level、-1自动及默认初始化；官方tag仍有420限制，不能冒充与本机扩展构建完全一致。码流首帧两对照tile行列均log2=0，排除不同自动分块；CodecPrivate未初始化序列头与实际码流分别识别，不据首个CDEF0误判滤波关闭。
- 收尾验证：五份关键输出逐个ffprobe -count_frames完整读取，均AV1/3840×2160/yuv444p10le/24000/1001/240帧；脚本语法检查及git diff --check通过，已无测试FFmpeg编码进程。本轮两次读取plugin.json哈希均F5380A5F…，不同于此前会话哈希但宿主已打开，未将历史值冒充本轮基线，也未主动写配置。
- 报告docs/svt-parameter-ablation-20261006.md、README、STATUS与中文进度同步。只新增诊断脚本/文档；不改版本、用户预设、产品代码、模型或安装文件，不执行提交/推送/PR/发布。工作树仍非干净，建议继续或切换工具前考虑提交保存本地修复与证据。短片不代替全片或画质验收，旧版本历史差异保持待查。

### 2026-10-06 11:14 Codex：AVV3本地测速，按用户要求停止等PR

- 启动读取AGENTS/INDEX/STATUS/HandShake，git pull --ff-only最新；main f261e37，保留原两份未提交记录。先查UI异常：NuGet及本机LakeUI5.110仍有PixelPictureBox.Image，上游5.112改Source，与用户MissingMethodException吻合，但未取得用户实际DLL；用户说明贡献者将交PR，停止自主UI修复。四宫格入口早已移除，只留不可达旧代码，不恢复功能。
- 用户授权先验证本地AVV3速度，未提交的PR尚不可对比，gh pr list当前为空。隔离复制冻结1.3.12 EXE和已安装backend、PTH；仅Python解释器/FFmpeg只读链接，共享解释器禁止生成pyc。发布/本机/隔离EXE SHA均13777336c711577faab02268ae762debecb5a421be8185872f9fbb74477d1600，PTH SHA b8a8376811077954d82ca3fcf476f1ac3da3e8a68a4f4d71363008000a18b75d。安装目录不修改。
- 测试条件：RTX3060 Laptop 6GB；1920x1080 testsrc2/H264、60帧、FP16、无分块/补帧，FFmpeg wrapped_avframe/null空输出。计时只算RVE渲染段，排除导入、Engine构建和任务启动，不含实际压制编码。2x 15.06s/3.984FPS；3x 16.47s/3.643FPS；4x诊断16.79s/3.574FPS。全部有效有进度frame=60/progress=end。2x首次预热12.39s/4.843FPS保留，未作为稳态结果。首次加入带空格的progress路径被现有参数拆分导致失败，改项目相对文件名后通过，不修改产品。
- 4x原版流程构建完成后，torch_tensorrt.save→torch.jit.trace重复输出校验将4x大帧转FP64，torch.testing.assert_close再申请760MiB导致CUDA OOM；不能认作4x普通成功。仅诊断benchmark_convert4.py把大输出同容差数值比较移CPU（保留设备一致性检查），两次比较通过后保存Engine，用于后续推理测量。未覆盖安装/已发布转换脚本，不将诊断当产品修复。
- 路径对照：同1.3.12推理4x/7680x4320帧回传，再FFmpeg Lanczos缩2x为16.97s/3.536FPS，缩3x为16.93s/3.544FPS。不是旧发行版整体复现，也未验证用户原9FPS参数/设备。约13%的2x数值差只作本轮观测，受功耗温控影响，不能当恒频性能承诺。
- 纯Engine预热5/测20次：2x平均269.364ms/3.712FPS，3x276.999ms/3.610FPS，4x274.301ms/3.646FPS，实际输出均FP16且尺寸正确。单独RGB量化/回传约14.14/24.65/49.60ms。同步GPU采样81–82℃、577–900MHz；nvidia-smi -q显示SW Power Cap及SW Thermal Slowdown Active，Current Power Limit55W/Default80W，Target Temperature75℃。因此不能将当前全部低速归因代码或以本轮断言此前修复无效；之前正确低倍率路径已确认，但先前96x64/4帧验证没有证明恢复9FPS。
- 用户“不必测了，等pr”到达时冷却2x复测进行中，taskkill /PID 26096 /T /F只终止本轮已核对root及4个子进程；该轮无有效结果，不计入数据。不再追加GPU/构建测试。命令及证据：Artifacts/avv3-speed-audit的prepare.py/run.py、identity.json/results.json、各CLI/progress日志、4-build-cpu-check.log、engine-results.json、gpu-limits-after.log；所有产物位于项目内忽略目录。
- 当前结论：等待PR审核，不推断“集成效率回来了”具体实现或真实性；产品源码/版本/安装/发布不改，版本迭代记录不改。STATUS与中文进度收尾，工作树仅两份记录未提交、非干净，建议后续继续或切换工具前考虑Git提交。

### 2026-10-06 13:44 Codex：MyGO AVV3 2x与用户新SVT参数约三分钟持续测试

- 同工具续做，按AGENTS/INDEX和STATUS快照/最近记录定位，读取README（根目录无project/changelog）。git pull --ff-only已最新，保留全部既有未提交源码/测试/文档。用户明确实际运行约三分钟；p6保持，CRF沿前轮12，y410按既有yuv444p10le理解并提前说明，无音频，不改用户预设。
- 实测输入D:/Animation Enhance/MyGO BDRemux/01.mkv为H264/1920×1080/yuv420p/24000/1001。安装版AVV3官方PTH、TensorRT FP16、tile0、output-scale2，命中1080p scale2 cfg-cdf1f5d880590924cc61缓存；实际后端Model Scale2和rawvideo写管道3840×2160。用户新svtav1-params逐字保留，编码日志p6/VQ/CRF12/YUV444/10-bit/lp4/PPCS102、grain5 adaptive False等核对通过；lp4是并行级别而非四线程，不强行改其设置。
- MeasureTensorRt.ps1增加可选OutputScale/StopAfterSeconds、独立命名停止共享内存、从编码器出现起计时、RenderSeconds/StopRequested采样、正常停止退出130接受。首次完整模型路径缺引号导致ReadyToRun未知参数，在渲染前失败；修正仅该诊断脚本，保留失败日志，以-run前缀独立重试，不改产品代码。实际渲染181.69秒写停止字节，通过既有GracefulStop排空并封装。
- 区间差分：23.32–58.01秒超分7.821/编码7.330fps；63.85–116.74秒8.196/8.499；122.68–175.72秒8.694/8.882。积压先约140帧后缩到约100帧；编码采样最大10396MiB，GPU大多高负载、温度最高84℃，软件/硬件热降频未激活，功耗限制激活不误报为热降频。开头约9fps后有波动，但后段回升，不称持续下降或全片恒速。
- 核验：FFmpeg末frame1575/progress=end/8.26fps；ffprobe整文件1575视频包，唯一流AV1/3840×2160/yuv444p10le/24000/1001，65.691秒、139659317字节；首两帧解码通过，无音频/字幕，不冒称逐帧全解码。测试进程已退出。插件配置两次SHA均A071F867…保持，不沿用前会话哈希。本机日志/CSV/编码器日志与MKV保存在.work/verification/20261006-mygo-avv3-svt。
- README、持续复验报告、STATUS、中文进度同步，脚本语法及git diff --check通过。版本仍1.3.12，产品/安装/模型/配置保持，未commit/push/PR/Release；工作树非干净，建议后续或切换工具前考虑提交保存。只验证这组参数/素材开头三分钟，不是旧参数同素材单变量对照，整集/场景变化及画质待用户进一步测试。

### 2026-10-06 13:49 Codex：按授权准备上游修复PR

- 同工具续做，读取AGENTS/INDEX/STATUS与README；根目录无project/changelog。git pull --ff-only已最新，确认origin=maxzrb、fork=user-Wing/VideoEnhancer-fork，登录user-Wing，fork父仓库maxzrb；当前fix/trt-native-lakeui-20261006跟踪origin/main f261e37，所有本轮未提交修改已逐文件审查。上游开放PR10属于他人第三方许可改动，不修改；当前分支无同名PR，计划新建main目标PR，历史PR8不作为本轮更新目标。
- 提交前重新build Release通过（0警告0错误），38项Python含CUDA图内缩放、18项倍率UI、FPS起点/暂停/ETA、LakeUI5.110/5.112实际插件回调、宿主运行契约全部通过；git diff --check通过。没有重新运行长片编码或安装/发布，引用此前真实Engine/部署和MyGO三分钟记录，不扩大为全片性能保证。
- 准备将AVV3转换器内嵌/显示、Image/Source预览兼容、FPS统计与测试/测速记录显式暂存。版本仍1.3.12，不包含视频、权重、Engine、缓存或二进制。PR说明采用直接中文条目和验证边界；写作风格技能未提供可用检索工具，依据用户当前表达撰写，不声称检索了历史写作样本。待完成提交/推送/创建后追加实际链接与同步检查。

### 2026-10-06 13:51 Codex：上游PR11提交与交付核对

- 显式暂存并审查21项源码/测试/文档，git diff --cached --check通过；无exe/dll/engine/mkv/pth/zip/7z纳入。源码提交4583bbd（fix: bundle TRT output scaling and support LakeUI preview APIs），非强制推送至fork/fix/trt-native-lakeui-20261006并设置该远端跟踪，不修改上游main或fork/main。
- gh pr create指定repo=maxzrb/VideoEnhancer、base=main、head=user-Wing:fix/trt-native-lakeui-20261006成功，PR https://github.com/maxzrb/VideoEnhancer/pull/11；普通开放PR，已调用attach_artifact附当前聊天。回读head SHA等于4583bbd6d278b8605c214cb9ea3dcdb211d7e465，目标/源正确，21文件与提交一致，mergeStateStatus=CLEAN。gh pr checks报告没有检查，不能冒称CI通过；未执行合并、标签、Release或安装部署。
- PR说明按中文直接条目列出低倍率图内输出、LakeUI兼容、FPS修正及38项Python/18项UI/双LakeUI回调/宿主验证，并明确官方4x权重不变、GPU缩放不是独立2x/3x网络、短测不保证整片性能/画质。本轮采用写作风格技能依据当前用户表达组织说明，没有可用的检索工具或历史样本检索声明。
- 源码首次提交推送后git status为空；本条及中文进度作为收尾文档提交再次同步到同一PR分支，最终回读远端HEAD和干净状态。无需额外提交本轮已同步文件；版本仍1.3.12，后续合并/发布需要用户另行授权。

### 2026-10-06 18:00 Codex：审核 PR #10/#11

- 同工具延续会话，按HandShake读取AGENTS/INDEX/STATUS及技能，git pull --ff-only与status完成，保留启动时两份记录改动。审核固定maxzrb/VideoEnhancer #10 ea3776f7898677ac61b1ba3b1f7aba71cc972eb9、#11 6f0756bbcf188f7804e11f46fb0b7d662be43178，共同基线f261e375；gh查询显式--repo，两个PR OPEN、无远端自动检查。先前默认gh选中upstream的PR查询不能作为本fork是否有PR的证据。
- 执行git fetch origin refs/pull/10/head:refs/remotes/origin/pr-10-review及11对应ref，在项目Artifacts/pr10-review-current、pr11-review-current建立detached worktree，产品审查不改主分支。
- PR10：dotnet build cli/VideoEnhancer.csproj -c Release通过0警告0错误；dotnet publish VideoEnhancer.slnx -c Release -p:PluginInstallDir=成功生成EXE/ZIP/安装器/183项源码包。test-native-archives.ps1 40项、test-third-party-package.ps1 110项通过，包含归档预检、错误/取消、模拟内部安装、逐项哈希、FFF API11与导出检查。独立FFF不内嵌LakeUI，移除SharpCompress，RAR不再支持且文档已写。只确认许可材料的技术打包，不对未公开商业授权作独立法律判断。
- PR11：CLI与插件Release构建0警告0错误；PixelPreview Probe按LakeUIVersion=5.110.0及5.112.0运行实际OnPreviewFrameReady，assign/replace/clear/ownership均通过。PR10未修复回调在5.112通过同一Probe复现MissingMethodException:set_Image，退出-532462766属于预期对照。FpsTracker Probe通过initial-frame-offset/pause-time/eta。便携Python -B运行test_tensorrt_output_scale.py，CPU/CUDA两项通过，非测速；ModelMetadataUi Probe18项通过，未启动宿主/显示窗口。
- 结论：没有发现阻止合并的新缺陷。此前AVV3目标倍率调度及安装转换脚本已有图内缩放，PR11补充随CLI内嵌同步，未替换官方4x权重；2/3x仍4x网络后GPU双三次缩小。FPS修正不提高实际吞吐。作者约9FPS数据包含异机及SVT CDEF自动策略/预设变化，其原始日志不在本仓库，不能宣称全部性能回退已解决或证明此前修复失败。此前6GB/1080p/4x torch_tensorrt.save校验OOM逻辑仍存在，作为既有未解决问题；没有大尺寸构建/长时间测速。四宫格只修遗留调用，入口未恢复。
- git merge-tree --write-tree显示产品代码可自动合并，仅docs/codex/STATUS.md与version/工作进度.md冲突；合并须保留两边历史及本地审核记录。本轮没有实际合并或组合版本运行。两个review worktree各自git diff --check通过；PR10的.gitattributes保留上游许可原文空白，不改其哈希。
- 收尾新增docs/pr10-pr11-review.md，更新STATUS snapshot/TODO/Git Sync及此会话记录，并追加中文进度。不改版本迭代记录。所有下载/构建/模拟安装在项目Artifacts隔离目录；本机正式安装、版本、远端不变。主工作区三个文档变化，非干净，无提交/推送/部署/发布，建议继续合并或切换工具前考虑Git提交。


### 2026-10-06 19:32 Codex：PR10/11安全合并与独立源码供给（prepared）

- 同工具续做，读取AGENTS/INDEX/STATUS与HandShake；git pull --ff-only已最新，main f261e37，保留三份本地审核文档。用户明确授权合并，不再询问方案；先显式提交审核记录81be99d，再建立codex/pre-pr10-pr11-20261006恢复分支，fetch核对PR10 ea3776f、PR11 6f0756b没有变化。
- 顺序git merge --no-ff --no-commit PR11与PR10；产品代码自动合并，STATUS/中文进度冲突用UTF-8保留双方段落、按冲突区域内新增会话时间合并，原冲突字节备份Artifacts/pr-record-conflicts-pr11/pr10。完整历史逐块对比81be99d及两PR通过；合并提交eb51739和c3f667d，两个head均通过merge-base --is-ancestor核对，未重写贡献者历史、未删除分支。
- 组合验证：git worktree add --detach Artifacts/pr10-pr11-merged c3f667d；缓存从项目内既有审核目录复用，只覆盖隔离新建目录；dotnet publish solution -c Release -p:PluginInstallDir=且FffNativeCacheDirectory指定项目内已校验缓存。成功生成四类产物，源码ZIP192项约62882085字节，运行ZIP18040761字节；独立源码不进入运行包。test-third-party-package.ps1 110项、PixelPreview Probe5.110/5.112实际插件回调、FpsTracker Probe及ModelMetadataUi18项通过，转换器内嵌资源与独立源码ZIP均匹配源文件SHA。没有重复40项归档/长GPU测速、没有启动或覆盖本机宿主/正式安装。
- 用户追加“根据许可证要求有必要增发源码包吗，没有必要就不发”。在线核对OSI许可原文：LGPL2.1第4条要求分发库二进制时提供对应完整源码/同处等效下载；MS-RL第3(A)要求相关文件源码；GPL2第3条规定完整对应源码供给方式。7-Zip官方FAQ对未修改组件有明确来源链接说明，不能泛称所有组件都强制ZIP；MIT原创主体也无强制源码ZIP义务。具体履约方案选择为保留现有独立源码资产，因自动GitHub项目源码不含FriBidi完整源码、vcpkg构建补丁及WiX对应源码。该选择是当前分发方案的完整源码供给方式，ZIP格式不是唯一法律要求；若未来取消，须先提供等效完整渠道。参考：https://opensource.org/license/lgpl-2-1、https://opensource.org/license/ms-rl、https://opensource.org/license/gpl-2.0、https://www.7-zip.org/faq.html。GNU网页部分超时，使用OSI原文及上游官方FAQ核对。
- 本地合并完成，准备普通推送origin/main，远端PR状态待核对。本次不修改/补发已发布v1.3.12附件、不上传新Release、不改版本或版本迭代记录、不部署，不宣称原9→3.9问题及4x OOM全部解决。审核报告追加组合验证与源码方案核对，STATUS及中文进度同步，完成步骤后考虑Git提交；本轮将保存并推送记录，最终核对干净工作树。


### 2026-10-06 19:34 Codex：PR10/11远端合并确认与收尾

- git push origin main成功，f261e37→cfbb2c5，未强制推送。gh api回读PR10与PR11均closed/merged=true，合并SHA分别c3f667dc51d9186b4f300e8bbc75467083030fc4、eb51739af9e32752190b0d2e27d48b2d0716a486。git ls-remote与本地HEAD均cfbb2c51f0619702b6316889440c0d316720943a，主工作树及组合验证worktree无未提交文件，冲突标记检查为空。
- 完整会话历史再次核对通过。用户追加的源码问题已按前条核对：有第三方对应源码供给义务，ZIP非唯一格式；当前保留后续独立source.zip流程，没有补发旧版或上传Release。组合验证结果见前条及审核报告，版本与正式安装未改，速度与低显存4x问题仍保留。
- 更新当前快照与此收尾记录、追加中文进度，随后提交并普通推送；最终核对远端HEAD与工作树。合并、验证记录都已保存，无需用户再次提交本轮内容；继续或切换工具前可确认Git状态。


### 2026-10-06 19:48 Codex：准备1.3.13发行与GitHub独立源码附件

- 用户明确授权1.3.13 Release，取代此前仅合并不发布范围；源码附件合为一个ZIP且仅上传GitHub。启动读取AGENTS/INDEX/STATUS、沿用HandShake与发布流程，git pull --ff-only最新、main15e4139/工作树干净，GitHub v1.3.13与本地tag不存在。执行环境仍Windows/PowerShell7/.NET10，gh与modelscope可用；未打印凭据值。
- 同步两项目Version为1.3.13、更新分类Release Notes、修正版本记录（保存原1.3.11当前段至历史，补1.3.12发行历史）。发布脚本将source.zip暂存到dist/github/releases/<version>，ModelScope采用仅本版运行文件/清单/说明的暂存目录，防止源码或历史资产混入上传。README/许可范围/第三方SOURCE/发行说明同步单GitHub源码获取约定，保持UTF-8/BOM/换行。
- 本轮将构建并完成项目发布门禁、后端真实目录逐文件审计、来源/包哈希与安装/更新验证，再提交推送版本源码与标签、上传双源运行资产及单GitHub源码附件，回读核验并收尾。模型/RTX/Python独立包不计划重发；不运行长期GPU测速，不承诺已完全解决此前9→3.9或4x OOM。


### 2026-10-06 20:20 Codex：继续1.3.13并剥离四宫格残留

- 用户追加授权安全剥离四宫格残留。同工具续做，读取AGENTS/INDEX/STATUS和HandShake，git pull --ff-only最新，保留前轮18项发行准备改动。修复cli/README混合换行被规范化产生的无关差异。
- 引用核对确认QuadGridForm/QuadGridControls/GridCompositionBuilder仅为已取消功能使用；删除三文件、主面板未接入按钮/窗口字段/点击回调/释放代码和专属DPI检查，保留主面板DPI与实时预览PixelPreviewImage兼容层、独立FFF运行分发。发布说明及当前文档同步，旧历史保留。
- 尚未发布；接下来运行必要构建、预览双版本与DPI及既有发行门禁，保存提交后发布固定产物与单GitHub源码附件。


### 2026-10-06 20:30 Codex：1.3.13发行门禁通过并准备冻结

- Release构建0警告0错误，Python顺序回归9项通过；后端逐文件审计UNCHANGED，保持2026.09.30.1，无后端/模型/RTX包重发。新189项独立源码包、110项运行/源码/隔离安装/API11检查通过。
- 首次installer/Burn检查命中既有脚本硬编码Artifacts目录导致误读旧载荷/旧运行EXE，不是新包安装损坏；为两脚本添加ArtifactsRoot参数并由发布脚本传入。改后installer/updater/回滚/Burn/5项release门禁、DownloadQueue/ComponentInstallStatus/DownloadQueueUi、host-runtime/scroll、主面板DPI和LakeUI5.110/5.112实际预览回调全部通过；日志Artifacts/release-1.3.13。
- 四宫格专属三文件与主面板残留已删除并成功构建，现存正常预览不变。代码准备提交、单独刷新源码ZIP（脚本修正不改变运行二进制），冻结五资产后推送v1.3.13并发布GitHub和ModelScope；源码仅GitHub，镜像暂存目录仅六个本版运行/说明文件。尚未上传，原性能与4x OOM限制保留。


### 2026-10-06 20:36 Codex：1.3.13正式发行、源码单GitHub与本机验收

- 发行源提交3747d0a（四宫格剥离/版本/源码约定）与b97622a（定制目录检查修正/验证记录）；git push --atomic origin main v1.3.13成功，标签解引用b97622a7421daddd9b4834f415c48817358bfa40，未改旧标签。gh release create --repo maxzrb/VideoEnhancer --verify-tag --notes-file创建正式非草稿非预发行Release，五项附件和逐行正文回读一致。
- 发布后ModelScope白名单目录上传六文件、删除0；仅1.3.13运行EXE/安装器/手动ZIP及stable/README/notes，另同步Models Plugin/videoenhancer.exe。源码ZIP仅GitHub，不删除旧版既有源码归档，ModelScope API本版目录恰为三个运行文件。源码189项、62859474bytes、SHA256 9e519fe45529245b583a986bf00ad2dcf6b5cafc9a0205626c5b824d21109502；源码包中无被删除的四宫格三文件，包含最新修正发布/安装/Burn检查脚本。运行ZIP18003356bytes，运行EXE12760744bytes，不包含源码归档。冻结清单Artifacts/release-1.3.13/asset-hashes.json。
- verify-remote.py实下载GitHub五项、ModelScope四项和Models备用EXE，共10项大小/SHA一致；GitHubdigest/正文格式、ModelScope源码缺席、README约定与后端channel保持2026.09.30.1/2条历史补丁通过。证据remote-verification.json/modelscope-version-tree.json/remote-backend-channel.json及日志均在Artifacts/release-1.3.13，未提交产物/凭据。
- 本地部署前确认宿主/处理任务退出，备份C:\Codex Program\3fui plugin\Artifacts\.refactor-tmp\backup-before-release-1.3.13-20261006-203252，通过内部完整安装载荷静默安装并跳过旧版清理。45文件与手动ZIP逐项匹配，FFF API11十DLL固定hash匹配，--version为1.3.13；配置/用户模型清单/aria2/7zip/RTX六项原hash保持，没有启动宿主或重发RTX。EXE d4e7d7764966d311c2e3d89e470a62f1e03c076e1d829431d940386c512f3767，DLL 3ad7ddc26a1bf86b3aed28ec21eeedaf51537c0dcfecd66847d89561a2007985；local-deployment.json保存备份和保护项前后hash。
- 验证：Release build 0警告0错误、Python顺序9项、110项打包/隔离安装/API11、安装/更新/回滚/Burn、发布门禁、队列/组件状态/队列UI、宿主/滚动/主面板DPI及LakeUI5.110/5.112真实预览回调均通过。安装/Burn旧硬编码路径问题已修复并定向重跑；没有重复长GPU测速、没有承诺全部9FPS或修复既有低显存4x OOM，宿主可视交互仍待实际使用反馈。
- 更新STATUS当前快照/TODO/环境/验证/Git及本条、追加中文进度、更新1.3.13版本发行与冻结资产记录。发行前git status干净、origin/main和tag均b97622a；本条为后续记录提交，普通推送后再次核对本地/远端SHA和干净状态，无需用户额外保存本轮文件。

### 2026-10-16 20:56 Codex：同步最新源码并修复实时预览卡死与频率切换

- 同工具跨日期续做，读取AGENTS/INDEX/STATUS、根README（根目录无project/changelog）及用户videoenhancer pr.txt。轻量记忆仅作旧PR/路径/边界定位，现场git/源码为准。旧分支git pull --ff-only已最新，但仅同步fork；git fetch origin取得472d62f/v1.3.13，gh回读PR11已于10月6日合并。新建fix/realtime-preview-20261016从origin/main开始，保留旧fix分支、备份和未跟踪说明文件，无reset/stash，不把用户说明纳入修改。
- 根因复核：WinForms Tick同步ReadToEnd后才WaitForExit，原超时不能覆盖读管道，关键帧使用全文件-show_frames/skip_frame触发反复解码；输入帧率探测也存在同类同步读取。定时模式每Tick更新_lastProgressSeconds，慢速内容进度无法累计到0.25秒门槛；500ms调度抖动还可能将0.5秒请求延迟到1秒。
- 先增加ModelMetadataUi --preview-engine/--preview-probe及独立进程工具夹具，未修复版本分别在0.5秒累计进度断言、关键帧Tick耗时断言失败，确认可复现。PreviewEngine改Async Tick并用_tickBusy防重入；stdout/stderr异步排空、WaitForExitAsync和2500ms linked token覆盖读取/等待，取消或超时结束进程树。关键帧按-lastProbePosition/当前进度构造有界-read_intervals，show_packets解析PTS/flags，未命中也记录探测大小；首个PTS0有效，不再遗失busy期间的关键帧目标。
- 定时模式仅在StartExtract更新已抽帧大小/进度，1ms以上新内容进度即可触发，实际开始仍受所选0.5/1/2/3秒节流；轮询100ms避免500ms相位抖动。切换任务/频率/模式、隐藏和停止取消旧探测/抽帧，后台及UI回调均拒绝过期画面并释放图片；未更改控件几何、CLI、后端、模型、版本或用户预设。
- 验证：慢探测时UI心跳继续、探测单航班、真实超时结束PID、文件无增长不重探、0秒首关键帧和新包标记更新、四类取消及慢抽帧不回灌均通过。真实ffmpeg动态样本实测四档约0.53/1.10/2.08/3.04秒、相邻画面PNG哈希不同；实时复制写入AV1/4:4:4 12-bit MKV时显示至少三个新关键帧，写入进程正常退出，覆盖未封口场景。样本Artifacts/preview-20261016，独立夹具TEMP/VideoEnhancer-preview-*；仅小样本，不冒充用户1.3GB原片和实际宿主可视验收。
- 构建限制：上游AcquireFffNative固定下载请求失败，常规插件构建被依赖获取阻挡，没有擅自修订URL/哈希/许可。用-p:DesignTimeBuild=true --no-restore跳过原生获取，仅编译托管插件，0警告0错误；两版LakeUI实际插件回调、宿主运行契约、18项倍率UI及38项Python通过。未称完整安装/打包成功，也未覆盖安装目录或操作用户任务。
- README、插件README、STATUS和中文进度同步；旧文件有混合换行，格式修整只保留未改行原换行，不全文件规范化。版本保持1.3.13，不更新版本迭代记录。工作树含本轮源码/回归/文档及用户原说明，未commit/push/创建PR/合并/发布，建议验证后考虑提交保存。
- 20:59最终收尾：预览31项检查全部通过，最后一轮四档0.55/1.10/2.09/3.05秒；日志Artifacts/preview-20261016/preview-regression.log，最后夹具TEMP/VideoEnhancer-preview-760abf56cbf24bbb853d07c70536088e。混合换行保留后再次构建托管插件0警告0错误，日志同目录managed-build.log，最终DLL位于VideoEnhancerPlugin/obj/plugin-artifact/videoenhancer.3fui.dll，SHA256 4F40BDB91C96F0BF19F580003F58A2F089CB407F4B4C1BC127304C4C0AD0F3DD；git diff --check通过。无测试ffmpeg/ffprobe/ModelMetadataUi残留，不部署或推送；实际用户大文件与宿主可视复验仍保留。

### 2026-10-16 21:42 Codex：音频先行、分步临时输出与关键帧重试补修

- 同工具延续，已读取AGENTS/INDEX/STATUS及README相关说明；git pull --ff-only已最新，保留fix/realtime-preview-20261016全部先前修改和用户videoenhancer pr.txt。轻量记忆只用于验证边界。本机安装DLL SHA为4F40…，对应上轮候选，用户确实已替换；本轮不关闭/启动用户3FUI，不覆盖配置、安装或原视频。
- PreviewEngine读取当前步骤显示名称/实际执行参数（尚未执行时回退步骤命令），识别-vn、音频和ffprobe阶段，明确等待视频，不把音频out_time当视频位置；当前进程/阶段/命令变化、时间回退或输出变小均取消旧工作并清空游标。视频抽帧优先使用当前步骤命令末尾的绝对输出路径，支持末尾-y/-n，不再只找尚未生成的最终封装文件。HostRuntime新增可选当前步骤访问，旧宿主缺少成员可回退。
- 新关键帧先缓存，只有成功抽取且当前代UI回调有效时才提交已显示PTS；失败保留目标，文件大小不变也可按最小0.5秒重试；关键帧不回退到普通进度/原始输入冒充命中。避免关键包尚未写完整时一次失败后等整个长GOP。未修改PixelPreviewImage/PluginPanel显示路径、控件几何、CLI、模型、后端、编码预设和版本。
- 回归：用可选VIDEOENHANCER_PREVIEW_PLUGIN直接测试用户当前安装DLL，新增音频阶段第一项断言失败；修订版25项基础检查全部通过，包括音频等待、实际临时输出、同任务时间回退、失败关键帧原地重试、阶段切换取消旧图和恢复，以及既有异步/超时/心跳/单航班/频率/隐藏/停止检查。Program.Preview.cs及Program/HostRuntime夹具扩展，不改宿主业务行为。
- 用户给定OP.mkv实查943306349字节、3840×2160、7001/146fps、AV1/yuv444p12le，含FLAC和ASS。原片窗口包探测0/30/60秒各约0.09–0.10秒；真实媒体四档持续取得不同图，显示间隔1.26/1.32/4.01/6.12秒，复杂4K解码不保证按0.5/1/2/3秒精确呈现，测试上限按抽帧预算而非小图+1秒设置。8秒无损截段在重封装写入、未封口期间持续显示三个新关键帧，写入正常结束；该项是复制写入，不冒充重新编码。
- 实际纯压制：原片5秒处取3秒内容，不超分，SVT-AV1 p6/CRF12/yuv444p12le/禁音频，enable-tf=0:enable-cdef=-1:lp=4，测试GOP48便于短段产生关键帧（不声称完全复用用户全部预设）。先模拟音频阶段80秒进度，再用真实FFmpeg -progress驱动视频阶段；最终封装路径始终不存在，144视频包/3.003秒正常完成，预览6帧且6个PNG哈希不同。输出位于TEMP/VideoEnhancer-preview-6cae4856360c41849b75c52b83b7136d/native-p6-video.mkv，编码日志同目录；原视频未修改。该项不是整段压制或实际3FUI宿主UI验收。
- LakeUI绘制：computer-use技能已完整读取，但当前无node_repl运行工具，未做屏幕自动化；改用独立测试程序与真实WinForms消息循环读回LakeUI V5 GPU表面。DrawToBitmap不包含GPU表面，早期读回还因VB整数除法优先级误取边框像素，均纠正且移除诊断性强制重绘/篡改。最终测试只调用实际OnPreviewFrameReady，不额外刷新控件：5.110和5.112均从同尺寸红图绘制为蓝图，RGBA分别255/255/0/0和255/0/0/255。未因测试伪失败给产品追加Invalidate或改LakeUI。
- 从本机FFmpegFreeUI.exe单文件包只提取托管程序集至忽略的Artifacts目录，宿主现有当前步骤/编码步骤显示名称/实际执行参数/命令行元数据契约通过；不运行宿主、不载入用户设置。18项倍率UI通过。托管插件DesignTimeBuild=true --no-restore构建0警告0错误，未重试/改造FFF下载和完整安装包；新DLL位于VideoEnhancerPlugin/obj/plugin-artifact/videoenhancer.3fui.dll，SHA256 CBDCE1173744CD436FA50007CB4840B2CA1EB4444C926D03E28D58D0AE1B93C1，安装DLL仍4F40…。
- 证据集中Artifacts/preview-20261016：preview-phase-regression.log、preview-2160-followup.log、preview-native-encode.log、render-5110/5112.log、host-followup.log、managed-followup-build.log；媒体及提取DLL均忽略不提交。README与插件README更新音频等待/临时输出/关键帧重试，并纠正插件README把当前PixelPictureBox写成PictureBox的旧说明；STATUS与中文进度同步，版本保持1.3.13、不改版本迭代记录。实际宿主重新加载候选DLL后的可视复验待用户确认；无commit/push/PR/发行/部署，工作树非干净，建议确认后考虑提交。

### 2026-10-16 21:53 Codex：准备提交实时预览修复上游PR

- 用户明确授权把这一轮更新提交maxzrb仓库PR。读取AGENTS/INDEX/STATUS、README，沿用本轮源码，不追加产品改动；轻量记忆仅用于fork PR路径和验收边界。git pull --ff-only已最新，origin/main仍472d62f；远端OPEN列表为空，fork确认为user-Wing/VideoEnhancer-fork且parent为maxzrb/VideoEnhancer，同名远端分支尚未存在。
- 重新执行托管插件build（DesignTimeBuild=true --no-restore）0警告0错误，25项预览基础回归、18项倍率UI及LakeUI5.112 GPU表面同尺寸红→蓝测试通过。前轮5.110 GPU/真实4K原片四档/3秒p6纯压制/未封口写入与宿主契约已有日志，本轮不重复长编码，不冒充实际3FUI全部预设可视验收。
- 准备显式暂存README、PreviewEngine、HostRuntime、插件README、ModelMetadataUi三个测试文件、PixelPreview测试及STATUS/中文进度，共10文件；用户videoenhancer pr.txt保持未跟踪且不上传，Artifacts和媒体/安装产物不提交。PR说明按用户简洁中文偏好列变更、验证和限制，版本保持1.3.13。普通提交后只推fork修复分支，再创建目标main的普通PR；不推上游main、不合并/发布/部署。
