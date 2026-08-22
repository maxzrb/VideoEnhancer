# VideoEnhancer 1.4 development line

> 1.3 已归档；本目录是后续功能与构建产物的唯一开发位置。

# Short Guide for plugin: VideoEnhancer.3fui.dll



_~~其实也是3fui目前全网最详细的图文教程 终末诗那样推荐小白用x264是在害人~~_

本程序基于Lake1059的ffmpegfreeui核心，以插件形式加载。

本超分软件的工作原理：使用ffmpegfreeui的参数面板功能进行编码配置，以RVE的超分后端配置python和模型，整体上是一个牵线搭桥的UI。

**除了超分，这个dll还实现了实时渲染，盯帧和一键生成比对视频的功能。**

不过，这个UI的上限 **远高于Video2X，VSET，Waifu2x-extension-GUI** ，因为对手基本上几年不更新了，功能做的稀烂。

本文将会手把手带你实现此软件的详细配置，以及模型选择。

## Part.1 软件配置

![[图片]](http://i0.hdslb.com/bfs/new_dyn/1977ba1dd9c0d5cd388f66536e9816f41188097661.png)

VCB那个是测试视频，不用下载

1 从链接 https://yun.139.com/shareweb/#/w/i/2wFH0FJ348y0x
中下载上图中除了\[VCB-Studio\]开头之外的5个文件。VCB那个是测试视频。

2 解压四个.7z格式的文件到对应的文件夹。如果**无法解压(提示算法不支持)**，请前往 [网页链接](https://github.com/mcmilk/7-Zip-zstd) 下载最新版7z-zstd进行解压。

![[图片]](http://i0.hdslb.com/bfs/new_dyn/dba521b1bd17961103e378ab9a6618da1188097661.png)

解压完成

3 在这里新建一个文件夹(示例: 3FUI)，把updater.exe，以及ffmpeg-full解压出来的所有文件放进去(注意不是把文件夹放进去)，目录会变成这个样子：

![[图片]](http://i0.hdslb.com/bfs/new_dyn/780387651b823a5e099f737a4c933d171188097661.png)



4 运行updater.exe，如果你没有安装net10，要按y安装；其会自动下载3FUI到文件夹。下载完成后，主程序会自动运行，先关闭。

![[图片]](http://i0.hdslb.com/bfs/new_dyn/fbe969264449fff60bb432917bd7668d1188097661.png)

如果速度很慢，建议直接浏览器搜索下载，把主程序放进去

5 在主程序ffmpegfreeui.exe所在目录建立名叫plugin的文件夹，转到VideoEnhancer-CLI中，把里面的VideoEnhancer.3fui.dll复制到plugin那里去

![[图片]](http://i0.hdslb.com/bfs/new_dyn/cfb6bc74ec815218873e2db98b0d41991188097661.png)

等下启动主程序

![[图片]](http://i0.hdslb.com/bfs/new_dyn/9c052c356d6a3e70a3dc4e14c2d767d61188097661.png)

复制，放进去

6 启动3FUI，左侧找到“视频超分”

![[图片]](http://i0.hdslb.com/bfs/new_dyn/0e953faa9ec0a3ee6dbbaa87a274cbd11188097661.png)



dll会自动进行环境检测。如果不通过，点击"更改路径"，到VideoEnhancer-CLI中，选择对应的exe程序：

![[图片]](http://i0.hdslb.com/bfs/new_dyn/1631210d5410a6fdccf50d26faa836a11188097661.png)



7 导入预设文件

![[图片]](http://i0.hdslb.com/bfs/new_dyn/4d609a5aacc72418c5a2dcc229948cce1188097661.png)

预设在哪里找？

在3fui中，选择参数面板-预设管理-导入，将里面三个预设全选一起导入进去

![[图片]](http://i0.hdslb.com/bfs/new_dyn/76744cb229030cd0fa4560de8bf0b7251188097661.png)

选择预设

![[图片]](http://i0.hdslb.com/bfs/new_dyn/4af8dc38c7cf30ed5d367640f96b223d1188097661.png)

三个全选一起进去

自此，环境配置完成。

## Part.2 超分的使用(+编码器使用说明)

预设已经定义了绝大多数的项目，这些预设都是群里高手测试出来的优质参数，如果你是小白，别乱动，可 **以动哪些按照本文说的就行了** 。如果你是高手，对ffmpeg参数很了解，那 **随便** 。

![[图片]](http://i0.hdslb.com/bfs/new_dyn/cbc6a8b877f877fb4ba6c0837ad9fd091188097661.png)

选择模型

## 1 超分页面的配置

**先把总开关和左边超分开关打开** ！

推理方式推荐 **NCNN** （无论你有没有N卡都是NCNN快于CUDA）

推荐使用这两个模型： **realesr-animevideov3-2x** (以及3x，4x)，还有基于realesr再训练的模型 **animejanaiv3-2x** ，二者 **水平不分伯仲** 。我们这里以后者为例，进行处理。

## 2 转到"参数面板"页面，找到你刚刚导入的预设。

使用预设的判断如下所示：

如果你有 **40系之后的N卡，且CPU不强，选择Hardware** ， **否则Software** 。

**点击上面的"读取"以加载预设。**

![[图片]](http://i0.hdslb.com/bfs/new_dyn/57d0e75e175024b22c62d4a87bdbe56a1188097661.png)

有N卡选Hardware没有选Software

**注意：上面那个Anime4K别乱动，这是后面才用的。**

## 3 进行你可能需要的自定义配置

### a. 视频参数-质量页面

![[图片]](http://i0.hdslb.com/bfs/new_dyn/27fca9d26c6b76384a973b9ca40140241188097661.png)



如果你是小白，只有crf/cq右边那个数值可以动，其他别动。高手随你便。

数值越小，文件体积越大，画面清晰度上限越高。

**对于Software的CRF，推荐的数值：**

高质量(个人接近无损的收藏，尤其是个人很喜欢的作品)--- **12**

较高质量(比较少用)--- **23**

中等质量(快速传输版本发布)--- **27**

VMAF-体积平衡点(针对高质量版本，VMAF-neg在95-96的平衡点)--- **36**

**对于Hardware的CQ，推荐的数值：**

高质量(个人接近无损的收藏，尤其是个人很喜欢的作品)--- **6~12**

较高质量(比较少用)--- **18~23**

中等质量(快速传输版本发布)--- **27~30**

VMAF-体积平衡点(针对高质量版本，VMAF-neg在95-96的平衡点)--- **38**

### b. 音频参数

音频 **默认压opus，这是秒杀一切的有损算法** 。

![[图片]](http://i0.hdslb.com/bfs/new_dyn/d5b5d3b70697cecef3a91ac1bd718b931188097661.jpg)

opus默秒全

**不推荐使用 除了复制流，禁用，opus之外 的任何其他模式。**

![[图片]](http://i0.hdslb.com/bfs/new_dyn/5210437248c7728abfc198bb14cf062f1188097661.png)



**选择"复制流"或者“禁用”，下面所有参数都不生效。**

**选择opus，按照此规则配置：**

源音频码率>1000K，opus选择压320k

源音频码率在320k-1000k之间，可选192k-320k

原音频码率低于320k，可以直接选96k

质量参数名可以写compression-level(下拉直接有)，右边数值写10(最大压缩)

![[图片]](http://i0.hdslb.com/bfs/new_dyn/599d710e7866a25607befe504455a1ba1188097661.png)



声道布局默认不写；如果 **文件报错-22，则一般写5.1即可解决** ，(本质是5.1 side和5.1 标准的切换问题) 或者你也可以 **把错误日志给AI看，AI会告诉你怎么办** 。

### c. 流控制

此设置主要针对"音频"进行。 **如果源文件包含不止一条音频流，ffmpeg可能会丢弃流** ，所以需要配置。

如果你不知道你的视频有多少音频流， **使用可视化流选择器** ：

![[图片]](http://i0.hdslb.com/bfs/new_dyn/22756d7e80068806dc7c3292ea231ff01188097661.png)



![[图片]](http://i0.hdslb.com/bfs/new_dyn/2ecd0d8476ac57291126d41a79d755cf1188097661.png)



比如这个文件有两条音频流，需要 **都勾选，并确认。**

备注1：字幕流建议全部保存，所以预设直接 **针对第一条字幕流复制并保留其他字幕流** 。元数据，章节和附件都应该保存，不要丢掉视频里面的这些信息。

备注2： **_如果你处理来自BDRemux的一整季动画，看第一集即可，因为一般每一集包含的音频流数目都是一样的。_**

## 4 添加文件并进行超分

![[图片]](http://i0.hdslb.com/bfs/new_dyn/9b08339ed4a9d82e9f966940ab87951a1188097661.png)



把文件从资源管理器中拖进来，点击上面的"加入编码队列"即可。

![[图片]](http://i0.hdslb.com/bfs/new_dyn/166001aa3532528eb43ce36642daa3ac1188097661.png)



![[图片]](http://i0.hdslb.com/bfs/new_dyn/235ba01ce785e6e778cb5ce655efe3501188097661.png)



你可以预览输出文件：

![[图片]](http://i0.hdslb.com/bfs/new_dyn/f12c7936a70283341fc89b3501206add1188097661.png)



**预览界面会增加CPU/GPU消耗，高负载下可能显示不出来，这是正常现象。实测发现Janai-V3就不太好显示，AnimeVideoV3可以正常显示。**

![[图片]](http://i0.hdslb.com/bfs/new_dyn/f45b2dee81cab4131886092dd107d7a61188097661.png)



![[图片]](http://i0.hdslb.com/bfs/new_dyn/79cc8605a533c1aa7d51ff6f1022c5ac1188097661.png)

阿卡林~~~~

等待超分完成即可。

## 5 Anime4K-着色器超分

刚才的RealESR AI超分是很慢的：4070上也只有4-5F每秒。如果是X4更慢，一般只有2.7F/s。在低端设备上，着色器超分是一种很好的解决方案。

使用方法如下所示：

1 在预设那里使用Anime4K的预设

![[图片]](http://i0.hdslb.com/bfs/new_dyn/097f4a640d783ce6a8f2787389ebdb211188097661.png)



然后，切换到"画面帧"-着色器超分

上面打勾，分辨率一般直接3840\*2160即可(原视频的比例必须是16:9才能这么写)，着色器 **选择你解压出Anime4K文件夹里面的Anime4K-modeA.glsl** 即可。

说明：ModeA为保守修复，快且不会过度锐化，一般用A即可。太过模糊采用A+A，可能会失真。

![[图片]](http://i0.hdslb.com/bfs/new_dyn/c99801bb7aabbf12d27421b97195e4661188097661.png)



参数细节调整，小白只推荐动"流控制"，还有"视频参数-质量"，以及"音频参数"。具体的，上一节已经讲过。

我这里只给了软件编码的版本，硬件编码，直接复制AV1-opus-hardware，然后按上一张图调整即可。

**注意：使用着色器超分，一定要关闭超分界面的开关！**

![[图片]](http://i0.hdslb.com/bfs/new_dyn/34bd20ae6dc7662e56c2753daf8a1a081188097661.png)



添加文件的方法一致，执行后具体效果如下所示：

![[图片]](http://i0.hdslb.com/bfs/new_dyn/95cb61f173cb16cdab7ef738eb99e86c1188097661.png)

速度很快，比realesr快9-10倍。4070LP能跑到40F/s，可以实时

![[图片]](http://i0.hdslb.com/bfs/new_dyn/589bb012cf17a052968010b9acb784001188097661.png)

任务也可以预览

## Part.3 补帧的使用

## 1 超分页面开启补帧

![[图片]](http://i0.hdslb.com/bfs/new_dyn/5e56fdf58d2f9f4a4d8e4b428bdbebd51188097661.png)



模型建议4.26/heavy，不过其他模型也在models文件夹有配备。

**RIFE补帧推荐对：**

三次元视频

3D动画(绝大多数当代国漫)

三渲二动画(GBC,mygo,hello world这些)

使用， **纯平面动画效果不好。**

然后选择你想要的倍率。推荐2倍，高倍率没那么大用且算力消耗很大。

## 2 查看视频原始帧率和倍率设计

![[图片]](http://i0.hdslb.com/bfs/new_dyn/e13474b48008a0b279092432a517b0db1188097661.png)



切换倍率时，会弹出提示，前往画面帧页面指定帧率。

一般的视频都是23.976帧，所以应该写47.952

![[图片]](http://i0.hdslb.com/bfs/new_dyn/dbf4fc59a866faa2b8d14dfdb00e2b0a1188097661.png)



不知道自己视频的帧率，可以用ffprobe执行

![[图片]](http://i0.hdslb.com/bfs/new_dyn/3e2637848ea00ad8ea1f5c81230010511188097661.png)



最后同样是准备文件开始即可。

**补帧和着色器超分似乎可以同时开启，直接实现2倍补帧+渲染，但是着色器超分和AI超分不能同时开启。**

补帧+着色器超分组合对性能要求很高，可以轻松吃满CPU+GPU，并把我的天选5pro拉满175W性能释放。

**注意：补帧开启后需要处理两倍(或者你选择倍率)的帧。**

## Part.4 高级功能

## 1 一键制作四宫格对比视频

![[图片]](http://i0.hdslb.com/bfs/new_dyn/352688d94e5a707c38137c81c01fc2a01188097661.png)



**警告：此功能用于比对同一个原视频，在不同超分软件/压制码率下，视频处理产物的效果。不要用这个来做剪视频之类的工作。**

自己放视频进来，右边简单配置编码等即可。这里就别奢求高级编码参数了，没必要！

建议同样长的视频，而且是同一个视频用不同做这个事情，否则我不知道会发生啥事。

![[图片]](http://i0.hdslb.com/bfs/new_dyn/ad1088ae7048935ce9cbda3ca18b8fc51188097661.png)



## 2 盯帧对比

用以上工具导入视频播放/暂停即可，暂时没有放大对比的功能(因为不是主要发心)。更强的版本移步 **群友开发的3FCompare项目**

[网页链接](https://github.com/luoye-cpu)

## 3 导入第三方模型

支持导入bin+param模型(NCNN)，以及pth模型(CUDA only)

放到CLI\\models目录，videoenhancer.exe会自动识别模型。

不保证第三方模型可用性。

## Part.5 来自测试员的更多信息和文件说明

由于本人深知小白对电脑的破坏力，我正在进行了大量的奇怪参数设置，探索不匹配的参数会发生啥事，以防止小白不知道怎么回事乱写参数导致问题。

(待补充)

DnCNN-ColorBlind-1x这种1x的模型是降噪用的

OpenProteus-Compact-i2-2x-70K是 **topaz的默认模型，真人很强但是不如星光和flashvsr，动漫别用**

Waifu2x系列模型一般认为不如realesr-avv3而且更慢

Nomos8k-span-otf-4x-strong这种没足够显存别用，很容易爆显存

**所以推荐还是janai或者realesr**
