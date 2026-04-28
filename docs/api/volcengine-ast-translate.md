# Volcengine AST Translation

GameSubRelay targets Volcengine 同声传译 2.0 for live speech translation.

## Endpoint

- WebSocket: `wss://openspeech.bytedance.com/api/v4/ast/v2/translate`
- Resource ID: `volc.service_type.10053`

## Authentication

The AST WebSocket connection requires headers:

- `X-Api-App-Key`
- `X-Api-Access-Key`
- `X-Api-Resource-Id`

Keys must come from local settings/secrets or environment variables. Do not commit keys.

## Event Mapping

Client events used by the provider seam:

- `100` StartSession
- `200` TaskRequest
- `102` FinishSession

Server events mapped to application state:

- `150` SessionStarted
- `651` SourceSubtitleResponse
- `654` TranslationSubtitleResponse
- `652` SourceSubtitleEnd
- `655` TranslationSubtitleEnd
- `352` TTSResponse
- `153` SessionFailed

`VolcengineAstSubtitleMapper` converts subtitle response/end events into `TranslationSegment` values keyed by channel and provider sequence.

## Protocol Status

The official interface uses WebSocket protobuf frames. The codebase implements the required AST request/response fields behind `IAstProtocolCodec` and covers the frame mapping with unit tests.

The microphone runtime uses AST mode `s2s`, sends 16 kHz/16-bit mono PCM chunks, maps source/translation subtitle events into `TranslationSegment`, and routes `TTSResponse` PCM bytes to the selected simultaneous output device.
