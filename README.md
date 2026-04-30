# SubRelay

SubRelay 是一个 Windows 桌面端实时语音翻译字幕工具，用于采集麦克风和系统/应用声音，并在屏幕上方显示透明、置顶、鼠标可穿透的字幕浮层。

中文 | [English](README.en-US.md)

## 功能

- 麦克风通道：把自己的语音实时识别、翻译，并可输出翻译后的语音。
- 系统声音通道：采集指定播放设备的系统/应用声音，显示原文字幕和译文字幕。
- 字幕浮层：透明、置顶、鼠标可穿透，适合会议、直播、语音聊天、视频播放和游戏等场景。
- 语言配置：支持源语言、目标语言配置；当前火山 AST 实现提供中文、英语、日语、印尼语、西班牙语、葡萄牙语、德语、法语和中英互译选项。
- Provider 架构：当前主链路使用火山引擎同声传译 2.0 AST，核心接口保留后续接入其他实时语音翻译服务的空间。
- 手动控制：软件启动后不会自动监听，每个通道都需要手动开始和停止。

## 运行平台

当前优先支持 Windows 10/11。系统声音通道使用 Windows WASAPI loopback，语义是捕获所选播放设备上的混音输出，而不是只捕获某个单独应用或进程。

发布包是 framework-dependent 构建，需要目标机器安装 .NET 8 Desktop Runtime。

## 服务商与凭据

使用火山引擎同声传译 2.0 AST 服务时，需要在控制台获取 `APP ID` 和 `Access Token`：

https://console.volcengine.com/speech/service/10030

在软件设置中填写到语音服务页的火山 AST 凭据区：

- `APP ID` -> `X-Api-App-Key`
- `Access Token` -> `X-Api-Access-Key`

注意：这里不要填写账号级 IAM AK/SK，这些语音接口使用的是语音服务页面里的 `APP ID` 和 `Access Token`。

## 本地构建

```powershell
dotnet restore GameSubRelay.sln
dotnet build GameSubRelay.sln -c Release
dotnet test GameSubRelay.sln -c Release --no-build
dotnet publish .\src\GameSubRelay.App\GameSubRelay.App.csproj -c Release -r win-x64 --self-contained false -o .\dist\win-x64
```

也可以使用仓库里的构建脚本：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

## 文档

- AST 双通道设计：`docs/design/ast-dual-channel-design.md`
- 火山 AST 接入说明：`docs/api/volcengine-ast-translate.md`
- 火山 ASR 诊断说明：`docs/api/volcengine-streaming-asr.md`
- MVP 手动检查清单：`docs/qa/mvp-manual-checklist.md`
