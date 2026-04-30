# GameSubRelay

GameSubRelay 是一个通用 Windows 桌面同声传译工具，用于采集麦克风和系统/应用声音，并在屏幕上方显示可穿透的字幕浮层。

## 功能目标

- 采集麦克风音频，通过当前接入的语音翻译服务生成翻译字幕和翻译后的语音输出。
- 采集指定播放设备的系统/应用声音，生成原文和译文字幕。
- 在透明、置顶、鼠标可穿透的浮层中按声道显示字幕，避免影响当前操作。
- 启用语音输出后，将翻译后的语音输出到指定播放设备或虚拟麦克风。
- 支持源语言、目标语言配置；当前火山 AST 实现提供中文、英语、日语、印尼语、西班牙语、葡萄牙语、德语、法语和中英互译选项。
- 当前实现使用火山引擎同声传译 2.0 AST，核心接口按 Provider 抽象保留，后续可接入其他厂商的实时语音翻译服务。
- 软件启动后不会自动监听，每个通道都需要手动开始和停止。

## 设计文档

- AST 双通道设计：`docs/design/ast-dual-channel-design.md`
- 火山 AST 接入说明：`docs/api/volcengine-ast-translate.md`
- 火山 ASR 诊断说明：`docs/api/volcengine-streaming-asr.md`

## 服务商与凭据

使用火山引擎同声传译 2.0 AST 服务时，需要在控制台获取 `APP ID` 和 `Access Token`：

https://console.volcengine.com/speech/service/10030

在软件设置中填写到语音服务页的火山 AST 凭据区：

- `APP ID` -> `X-Api-App-Key`
- `Access Token` -> `X-Api-Access-Key`

注意：这里不要填写账号级 IAM AK/SK，这些语音接口使用的是语音服务页面里的 `APP ID` 和 `Access Token`。

## 运行平台

当前优先支持 Windows 10/11，面向会议、直播、游戏、语音聊天、视频播放等需要实时字幕和翻译的桌面场景。播放设备通道使用 Windows WASAPI loopback，语义是捕获该播放设备上的混音输出。
