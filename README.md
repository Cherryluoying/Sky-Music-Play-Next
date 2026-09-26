<a id="readme-top"></a>

<!-- 模块：项目头图 -->
<div align="center">
  <img src="logo.jpg" alt="SkyMusicPlay Logo" width="168" height="168">

  <h1>猫橘咪音乐 · Sky Music Play Next</h1>

  <p>面向 Windows 的音乐播放、MIDI、钢琴练习、游戏乐器辅助与编谱工作区</p>

  <p>
    <a href="https://github.com/Cherryluoying/Sky-Music-Play-Next"><img alt="Repository" src="https://img.shields.io/badge/GitHub-Sky--Music--Play--Next-181717?style=flat-square&logo=github"></a>
    <img alt="Platform" src="https://img.shields.io/badge/Platform-Windows-0078D4?style=flat-square">
    <img alt=".NET" src="https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square">
    <img alt="Avalonia" src="https://img.shields.io/badge/Avalonia-12.1.2-8B44AC?style=flat-square">
    <img alt="C++" src="https://img.shields.io/badge/C++-20-00599C?style=flat-square">
    <img alt="License" src="https://img.shields.io/badge/License-AGPL--3.0-2F7D32?style=flat-square">
  </p>
</div>

> [!IMPORTANT]
> 当前版本处于持续迁移与开发阶段，专业半 DAW、视觉识别和部分 VST3 高级能力尚未完成 <br>
> 目前舍弃了部分[windhide/SkyMusicPlay-for-Windows](https://github.com/windhide/SkyMusicPlay-for-Windows) 功能<br>
> 本软件不支持任何宏脚本与按键宏功能

> **使用演奏辅助时，请右键程序选择“以管理员身份运行”。** 向以管理员权限运行的游戏发送按键时，猫橘咪音乐也需要管理员权限。普通音乐播放、歌词和本地媒体管理可在普通权限下使用。

> **请使用Visual Studio 2026 打开**

<!-- 模块：项目导航 -->
<details>
  <summary>目录</summary>
  <ol>
    <li><a href="#项目简介">项目简介</a></li>
    <li><a href="#主要功能">主要功能</a></li>
    <li><a href="#架构设计">架构设计</a></li>
    <li><a href="#模块说明">模块说明</a></li>
    <li><a href="#技术栈">技术栈</a></li>
    <li><a href="#开始使用">开始使用</a></li>
    <li><a href="#云端服务">云端服务</a></li>
    <li><a href="#开发路线">开发路线</a></li>
    <li><a href="#来源与致谢">来源与致谢</a></li>
    <li><a href="#开源许可">开源许可</a></li>
    <li><a href="#️-免责声明--disclaimer">免责声明</a></li>
  </ol>
</details>

<!-- 模块：项目简介 -->
## 项目简介

Sky Music Play Next 是 SkyMusicPlay 的新架构版本，用 C#、Avalonia 与原生 C++ 逐步替代旧版 Electron 和嵌入式 Python 运行环境

项目围绕统一的 88 键乐谱模型、游戏键位映射和原生实时音频边界构建，目标覆盖以下使用场景：

- 音乐播放器：播放本地音频并显示封面、歌词和桌面歌词
- MIDI 播放器：导入、播放、导出 MIDI 并连接外部 MIDI 设备
- 钢琴练习器：通过 88 键钢琴窗显示、练习和记录演奏
- MIDI 硬件：适配支持37-88键MIDI键盘
- 游戏乐器辅助：适配光遇、原神和自定义游戏键位
- 编谱工作区：编辑 Sky Studio 与 genshin-music 兼容乐谱，并逐步扩展半 DAW 扒谱能力

Intel 与 AMD 处理器共用能力驱动的执行路径，不再按 CPU 品牌维护两套播放逻辑

<p align="right">(<a href="#readme-top">返回顶部</a>)</p>

<!-- 模块：主要功能 -->
## 主要功能

### 桌面客户端

- Fluent 风格主窗口、页面导航、全宽 MiniPlayer 和沉浸式播放页
- 发现、媒体库、收藏、最近播放、任务和设置页面
- 独立桌面歌词窗口与云端歌词缓存
- 音频、MIDI、TXT/JSON 乐谱
- 读取音频内嵌封面、歌曲名、歌手、专辑与作者，保存到 SQLite 媒体库
- 播放队列支持“添加到下一首”和移除，进度条支持点击定位与拖动预览
- 单句透明桌面歌词、悬停控制栏、字体/描边/填充颜色与透明度设置
- 演奏悬浮球与单一气泡面板，支持歌单搜索、收藏、播放控制和演奏间隔/延迟设置
- 悬浮面板随屏幕边缘调整位置；乐谱与 MIDI 导入目录可配置并直接打开

### 乐谱与自动演奏

- Sky Studio TXT/JSON 导入、加密谱解析与兼容导出
- genshin-music 格式、光遇 15 键和原神 21 键布局
- 标准 MIDI 导入导出与 MIDI 21-108 的 88 键数据模型
- 高精度时间线、暂停、继续、停止、跳转、速度、实时间隔和按键释放延迟
- Windows 扫描码输出、目标窗口选择与持久化自定义键位映射

### 编谱工作区

- 独立工作区窗口、游戏编谱模式和专业模式入口
- 图层、列编辑、复制、合并粘贴、擦除、撤销和重做
- Sky 与 Genshin 原始按键图形、背景、字体和乐器采样素材
- 当前乐器采样预载、Media Foundation 解码和 RtAudio 低延迟试听
- MIDI 转游戏谱、选区预览、工程保存与 MIDI 导出

### MIDI 与原生音频

- RtMidi 输入、输出、实时演奏记录和设备枚举
- 单音频图、单设备时钟和明确的实时/离线边界
- RtAudio WASAPI 后端与 64 复音乐器采样节点
- Rubber Band 实时变速、变调和离线渲染基础
- VST3 插件发现、隔离宿主和 MIDI 事件路径
- MIDI 原始事件序列播放、插件音色切换与插件编辑器入口
- MIDI 钢琴可视化、按键高亮及播放位置同步
- FFmpeg 解码与 NAudio 音频输出、音量控制

### 外部扩展

- PianoTrans 1.0 可选音频转 MIDI 扩展
- FFmpeg 用户配置、环境变量、系统 PATH 与 PianoTrans 路径发现
- Go 歌词与乐谱服务

<p align="right">(<a href="#readme-top">返回顶部</a>)</p>

<!-- 模块：架构设计 -->
## 架构设计

```text
SkyMusic.App
    Avalonia UI、AXAML、ViewModel、主窗口与编谱工作区
        |
        v
SkyMusic.Core
    乐谱、播放、项目模型与服务契约
        |
        v
SkyMusic.Infrastructure
    文件、网络、设置、Windows 输入和原生桥接
        |
        +--> SkyMusic.Midi
        |    RtMidi 设备 C ABI
        |
        +--> SkyMusic.AudioPreview / SkyMusic.AudioEngine
        |    采样解码、设备时钟、实时音频图和离线渲染
        |
        +--> SkyMusic.VstHost
             隔离的 VST3 原生宿主

skymusic-cloud
    独立 Go 歌词与乐谱封面 API

PianoTrans
    可选外部扒谱扩展，不嵌入主程序
```

核心原则：

- UI 不直接实现播放、文件解析或 Windows 输入
- 乐谱先进入统一领域模型，再在输出边界应用游戏键位限制
- 实时线程不执行文件读取、JSON、网络请求、内存分配或 UI 更新
- 非实时任务使用单线程或有界并行，不按 Intel、AMD 品牌分支
- VST3 插件运行于独立宿主进程，插件故障不应直接终止桌面端

<p align="right">(<a href="#readme-top">返回顶部</a>)</p>

<!-- 模块：模块说明 -->
## 模块说明

| 模块名称 | 路径 | 职责 |
| --- | --- | --- |
| SkyMusic.App | `src/SkyMusic.App` | Avalonia 界面、ViewModel、桌面交互与编谱工作区 |
| SkyMusic.Core | `src/SkyMusic.Core` | 领域模型、服务契约、乐谱规则与播放状态 |
| SkyMusic.Infrastructure | `src/SkyMusic.Infrastructure` | 导入导出、网络、设置、Windows 输入与原生互操作 |
| SkyMusic.AudioEngine | `native/SkyMusic.AudioEngine` | 音频图、设备时钟、Rubber Band 节点与离线渲染 |
| SkyMusic.AudioPreview | `native/SkyMusic.AudioPreview` | 乐器采样解码、预载、复音和按键试听 ABI |
| SkyMusic.Midi | `native/SkyMusic.Midi` | RtMidi 输入输出 C ABI |
| SkyMusic.VstHost | `native/SkyMusic.VstHost` | VST3 扫描、加载、音频处理和进程隔离 |
| SkyMusic.Native.Contracts | `native/SkyMusic.Native.Contracts` | C# 与 C++ 工作区的版本化 ABI 契约 |
| skymusic-cloud | `server/skymusic-cloud` | Go 歌词与乐谱 API |
| Backend Tests | `tests/SkyMusic.Backend.Tests` | 格式兼容、时间线、设置与服务行为测试 |

<p align="right">(<a href="#readme-top">返回顶部</a>)</p>

<!-- 模块：技术栈 -->
## 技术栈

| 分类 | 技术 |
| --- | --- |
| 桌面端 | C#、.NET 10、Avalonia 12、AXAML、MVVM |
| 原生层 | C++20、CMake、Windows SDK、Media Foundation |
| 音频与 MIDI | NAudio、RtAudio、RtMidi、Rubber Band、VST3 SDK |
| 乐谱与 MIDI 文件 | DryWetMIDI、Sky Studio / genshin-music 兼容格式 |
| 云端服务 | Go 1.22、HTTP JSON API |
| 外部工具 | FFmpeg、PianoTrans 1.0 |
| 测试 | xUnit、CTest、原生音频测试 |

<p align="right">(<a href="#readme-top">返回顶部</a>)</p>

<!-- 模块：开始使用 -->
## 开始使用

### 环境要求

- Windows 10/11 x64
- .NET SDK 10.0.401 或兼容的 .NET 10 SDK
- Visual Studio 2022/2026 C++ 桌面开发工具
- CMake 3.25 或更高版本
- Go 1.22，仅开发云端服务时需要
- 本地音频解码与封面/标签读取使用 FFmpeg、FFprobe；一键构建脚本会准备并打包这两个工具
- PianoTrans 为可选扩展；构建 VST3 宿主时需要 VST3 SDK

### 权限要求

使用游戏演奏辅助前，请右键 `SkyMusic.App.exe`，选择“以管理员身份运行”。Windows 会限制普通权限进程向高权限窗口注入按键；目标游戏以管理员身份运行时，本程序也需要提升权限。通过 Visual Studio 调试演奏辅助时，请以管理员身份启动 Visual Studio。普通音乐播放、歌词和本地媒体管理不要求管理员权限。

### 获取源码

```powershell
git clone https://github.com/Cherryluoying/Sky-Music-Play-Next.git
cd Sky-Music-Play-Next
```

### 构建原生模块

```powershell
powershell -ExecutionPolicy Bypass -File native/SkyMusic.AudioPreview/build.ps1 -Configuration Release
powershell -ExecutionPolicy Bypass -File native/SkyMusic.Midi/build.ps1 -Configuration Release
```

VST3 SDK 不随本仓库分发，需要单独获取：

```powershell
git clone --recursive https://github.com/steinbergmedia/vst3sdk.git third_party/vst3sdk
powershell -ExecutionPolicy Bypass -File native/SkyMusic.VstHost/build.ps1 -Configuration Release
```

也可以通过 `-SdkRoot` 指定已有 SDK：

```powershell
powershell -ExecutionPolicy Bypass -File native/SkyMusic.VstHost/build.ps1 `
  -Configuration Release `
  -SdkRoot "D:\SDK\vst3sdk"
```

### 构建桌面端

仓库根目录提供 `build-release.cmd`，可一键运行测试、发布桌面端并打包 FFmpeg / FFprobe，输出位于 `artifacts/release`。未找到本地工具时，脚本会下载 FFmpeg；原生模块仍按上方步骤构建。

```powershell
dotnet restore
dotnet build SkyMusicPlay.Next.sln -c Release
```

### 运行

```powershell
# 默认音乐客户端
dotnet run --project src/SkyMusic.App/SkyMusic.App.csproj

# 独立编谱工作区
dotnet run --project src/SkyMusic.App/SkyMusic.App.csproj -- --workbench
```

### 测试

```powershell
dotnet test tests/SkyMusic.Backend.Tests/SkyMusic.Backend.Tests.csproj -c Release
```

用户设置保存在 `%LocalAppData%\SkyMusicPlay\settings.json`

<p align="right">(<a href="#readme-top">返回顶部</a>)</p>

<!-- 模块：云端服务 -->
## 云端服务

```powershell
cd server/skymusic-cloud
$env:SKYMUSIC_WRITE_TOKEN="change-me"
go run ./cmd/server
```

默认监听 `:8787`，数据文件默认为 `data/store.json`

支持的环境变量：`SKYMUSIC_ADDRESS`、`SKYMUSIC_DATA_FILE`、`SKYMUSIC_CORS_ORIGIN`、`SKYMUSIC_WRITE_TOKEN`

<p align="right">(<a href="#readme-top">返回顶部</a>)</p>

<!-- 模块：开发路线 -->
## 开发路线

- [x] Electron / Python 核心能力迁移到 C# 服务层
- [x] Sky Studio、MIDI、宏脚本和加密谱基础兼容
- [x] 光遇 15 键、原神 21 键与自定义键位目标
- [x] 原生 MIDI 输入输出、88 键钢琴窗和演奏记录
- [x] 独立游戏编谱工作区与原生乐器采样试听
- [x] Go 歌词/乐谱服务与桌面端缓存
- [x] VST3 隔离宿主基础链路
- [x] 播放悬浮窗、歌单搜索与演奏控制
- [x] SQLite 本地媒体库、收藏、播放记录与播放队列
- [x] 音频标签/封面读取、桌面歌词与进度条交互
- [ ] 完成专业半 DAW 的轨道、钢琴卷帘与轻量混音界面
- [ ] 完成 VST3 编辑器、预设和音频设备持久化
- [ ] 将旧版识别模型转换为 ONNX 并完成 DirectML 一致性验证
- [ ] 完成跟弹识别、透明提示层和全局热键
- [ ] 扩展在线内容 API、云端同步与更新系统
- [ ] UI 重设计与动画加入

<p align="right">(<a href="#readme-top">返回顶部</a>)</p>

<!-- 模块：来源与致谢 -->
## 来源与致谢

本项目基于或使用了以下开源项目、SDK 与素材。代码、素材及二进制仍遵守各自的许可证：

| 来源 | 地址 | 用途 |
| --- | --- | --- |
| SkyMusicPlay-for-Windows | [windhide/SkyMusicPlay-for-Windows](https://github.com/windhide/SkyMusicPlay-for-Windows) | 原版功能、格式兼容 |
| genshin-music | [Specy/genshin-music](https://github.com/Specy/genshin-music) | 游戏编谱交互、格式兼容与运行素材 |
| PianoTrans | [azuwis/pianotrans](https://github.com/azuwis/pianotrans) | 可选音频转 MIDI 扩展 |
| Best README Template | [othneildrew/Best-README-Template](https://github.com/othneildrew/Best-README-Template) | README 信息结构参考 |
| Avalonia | [AvaloniaUI/Avalonia](https://github.com/AvaloniaUI/Avalonia) | 桌面 UI 框架 |
| DryWetMIDI | [melanchall/drywetmidi](https://github.com/melanchall/drywetmidi) | MIDI 文件读写 |
| NAudio | [naudio/NAudio](https://github.com/naudio/NAudio) | FFmpeg 解码后的 PCM 音频输出与音量控制 |
| RtMidi | [thestk/rtmidi](https://github.com/thestk/rtmidi) | 原生 MIDI 设备访问 |
| RtAudio | [thestk/rtaudio](https://github.com/thestk/rtaudio) | 原生音频设备与回调 |
| Rubber Band | [breakfastquay/rubberband](https://github.com/breakfastquay/rubberband) | 实时变速与变调 |
| Essentia | [MTG/essentia](https://github.com/MTG/essentia) | 后续音频分析工作层基础 |
| VST3 SDK | [steinbergmedia/vst3sdk](https://github.com/steinbergmedia/vst3sdk) | VST3 原生宿主开发 |
| FFmpeg | [FFmpeg/FFmpeg](https://github.com/FFmpeg/FFmpeg) | 音频解码，配合 FFprobe 读取时长、标签与内嵌封面信息 |

genshin-music 的 MIT 许可证副本保存在 `src/SkyMusic.App/Assets/GenshinMusic/Licenses`。Sky 乐器采样素材中特别感谢 Discord 用户 `Integrated Cane`

第三方源码目录内保留各自的 `LICENSE` 或 `COPYING` 文件，分发时必须同时遵守对应许可

<p align="right">(<a href="#readme-top">返回顶部</a>)</p>

<!-- 模块：开源许可 -->
## 开源许可

Sky Music Play Next 采用 [GNU Affero General Public License v3.0](LICENSE) 开源

第三方组件与素材不自动变更为 AGPL-3.0，仍适用其原始许可证与署名要求

<p align="right">(<a href="#readme-top">返回顶部</a>)</p>

<!-- 模块：免责声明 -->
## ⚠️ 免责声明 / Disclaimer

1. **用途限定**：本软件仅用于音乐学习与游戏内乐器玩法演奏，
   不适用于也不得用于战斗、竞技、经济系统等任何游戏内竞争性场景。
2. **风险告知**：使用任何第三方辅助工具理论上均可能违反游戏用户协议，
   因使用本软件而产生的一切后果（包括但不限于账号处罚）由用户自行承担，
   开发者不承担任何责任。请自行了解并遵守所在游戏的相关条款。
3. **非商业性质**：本软件仅供学习交流，严禁任何形式的售卖或捆绑收费，
   二次分发须保留本声明及项目原始链接。
4. **音源说明**：音乐播放功能为基础功能，本软件不提供、不分发任何音频
   音源与乐谱文件，用户使用的一切音源及曲谱均为用户自行获取与导入，
   相关版权归原权利人所有。
5. **项目性质**：本项目为开源学习项目，与任何游戏官方无关，
   未接受任何商业赞助或授权。

<p align="right">(<a href="#readme-top">返回顶部</a>)</p>

<div align="center">
  <img src="easter-egg.gif" width="98">
</div>
