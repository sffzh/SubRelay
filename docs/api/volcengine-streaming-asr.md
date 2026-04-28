# Volcengine Streaming ASR

GameSubRelay can use Volcengine 大模型流式语音识别 for language recognition / ASR support.

## Endpoints

The current option model records the documented WebSocket endpoints:

- Bidirectional streaming: `wss://openspeech.bytedance.com/api/v3/sauc/bigmodel`
- Optimized bidirectional streaming: `wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_async`
- Streaming input: `wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_nostream`

## Authentication

The ASR WebSocket connection uses:

- `X-Api-App-Key`
- `X-Api-Access-Key`
- `X-Api-Resource-Id`
- `X-Api-Connect-Id`

Known resource IDs are captured in `VolcengineStreamingAsrOptions`.

## Integration Status

The playback-device recognition channel uses optimized bidirectional streaming:

- Endpoint: `wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_async`
- Transport: Volcengine V3 binary WebSocket frames
- Request payload: JSON + Gzip full client request, followed by Gzip PCM audio-only frames
- Audio: 16 kHz, 16-bit, mono PCM, batched around 200 ms

ASR results are mapped into source-only `TranslationSegment` values so the overlay can show recognized playback text without pretending it is translated text.
