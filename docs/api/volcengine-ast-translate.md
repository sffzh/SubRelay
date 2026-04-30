# 火山引擎 AST 同声传译

GameSubRelay 当前主链路使用火山引擎同声传译 2.0 AST 服务。AST 同时承担识别、翻译、字幕输出和可选目标语音输出。应用层仍通过 Provider 接口调用，后续可以替换或新增其他厂商的实时语音翻译服务。

设计文档见：`docs/design/ast-dual-channel-design.md`

## Endpoint

- WebSocket：`wss://openspeech.bytedance.com/api/v4/ast/v2/translate`
- Resource ID：`volc.service_type.10053`

## 鉴权

WebSocket 连接需要以下 Header：

- `X-Api-App-Key`
- `X-Api-Access-Key`
- `X-Api-Resource-Id`
- `X-Api-Connect-Id`

`AppKey` 和 `AccessKey` 来自火山语音服务控制台，不提交到仓库。

## 模式

AST 支持两种主模式：

- `s2t`：语音输入，输出原文字幕和译文字幕。用于系统/应用声音字幕。
- `s2s`：语音输入，输出原文字幕、译文字幕和目标语音。用于麦克风翻译语音输出。

当前目标：

| 通道 | 模式 | 输出 |
| --- | --- | --- |
| 麦克风 | `s2s` | 原文、译文、翻译后音频 |
| 系统/应用声音 | `s2t` | 原文、译文 |

## 事件映射

客户端事件：

- `100` StartSession
- `200` TaskRequest
- `102` FinishSession

服务端事件：

- `150` SessionStarted
- `152` SessionFinished
- `153` SessionFailed
- `154` UsageResponse
- `250` AudioMuted
- `650` SourceSubtitleStart
- `651` SourceSubtitleResponse
- `652` SourceSubtitleEnd
- `653` TranslationSubtitleStart
- `654` TranslationSubtitleResponse
- `655` TranslationSubtitleEnd
- `350` TTSSentenceStart
- `351` TTSSentenceEnd
- `352` TTSResponse

`VolcengineAstSubtitleMapper` converts subtitle response/end events into `TranslationSegment` values keyed by channel and provider sequence.

## 协议状态

官方接口使用 WebSocket protobuf 帧。代码通过 `IAstProtocolCodec` 封装请求/响应字段，并用单元测试覆盖关键事件映射。

当前实现已经支持麦克风 AST `s2s` 和系统/应用声音 AST `s2t`。纯 ASR 仅保留为诊断和“只转写”扩展能力。
