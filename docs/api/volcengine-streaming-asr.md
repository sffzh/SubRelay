# 火山引擎流式 ASR

GameSubRelay 保留火山引擎大模型流式语音识别作为诊断和扩展能力，但它不再作为“系统声音字幕”的主链路。

主链路设计见：`docs/design/ast-dual-channel-design.md`

原因：

- ASR 只负责识别，不负责翻译。
- 系统/应用声音主需求是“识别并翻译成字幕”，应使用 AST `s2t`。
- ASR 后接翻译会引入额外延迟和错误传播，不作为默认方案。

## Endpoints

当前 option model 记录了以下 WebSocket endpoint：

- Bidirectional streaming: `wss://openspeech.bytedance.com/api/v3/sauc/bigmodel`
- Optimized bidirectional streaming: `wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_async`
- Streaming input: `wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_nostream`

## 鉴权

ASR WebSocket 连接使用：

- `X-Api-App-Key`
- `X-Api-Access-Key`
- `X-Api-Resource-Id`
- `X-Api-Connect-Id`

Resource ID 由 `VolcengineStreamingAsrOptions` 管理。

## 保留用途

ASR 只保留在这些场景：

- 测试火山流式识别连接。
- 对比 AST 字幕质量和延迟。
- 后续提供“只转写不翻译”模式。
- 开发诊断页查看原始识别结果。

ASR 结果仍映射为 `SpeechRecognitionSegment`。界面中应明确它是诊断/纯转写能力，主通道仍由系统声音字幕的 AST 链路承担。
