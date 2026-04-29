# GameSubRelay

GameSubRelay 是一个 Windows 桌面端游戏同声传译工具，用于采集麦克风和游戏/系统声音，并在游戏上方显示可穿透的字幕浮层。

## 功能目标

- 采集麦克风音频，通过火山 AST `s2s` 生成翻译字幕和翻译后的语音输出。
- 采集指定监听源的游戏或系统声音，通过火山 AST `s2t` 生成原文和译文字幕。
- 在透明、置顶、鼠标可穿透的浮层中按声道显示字幕，避免影响游戏操作。
- 启用语音输出后，将翻译后的语音输出到指定播放设备或虚拟麦克风。
- 软件启动后不会自动监听，每个通道都需要手动开始和停止。

## 设计文档

- AST 双通道设计：`docs/design/ast-dual-channel-design.md`
- 火山 AST 接入说明：`docs/api/volcengine-ast-translate.md`
- 火山 ASR 诊断说明：`docs/api/volcengine-streaming-asr.md`

## 火山引擎凭据

使用火山引擎同声传译 2.0 AST 服务时，需要在控制台获取 `APP ID` 和 `Access Token`：

https://console.volcengine.com/speech/service/10030

在软件设置中填写到火山 AST 凭据页：

- `APP ID` -> `X-Api-App-Key`
- `Access Token` -> `X-Api-Access-Key`

注意：这里不要填写账号级 IAM AK/SK，这些语音接口使用的是语音服务页面里的 `APP ID` 和 `Access Token`。

## 运行平台

当前优先支持 Windows 10/11，主要面向无边框窗口模式的游戏场景。
