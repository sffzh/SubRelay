# GameSubRelay MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Windows MVP for live two-channel Windows speech translation with a click-through subtitle overlay and optional translated speech output. The product is now positioned for general system/application audio scenarios; games remain one supported use case.

**Architecture:** Use a .NET desktop solution with a WPF app shell, a pure Core project for state/models/interfaces, and an Infrastructure project for NAudio, Win32 interop, Provider-based speech services, config persistence, and output playback. Current implementation uses Volcengine WebSocket providers while keeping the service boundary open for other vendors. Keep subtitle rendering independent from TTS so captions remain available when speech output is disabled or failing.

**Tech Stack:** .NET 8, WPF, NAudio, Microsoft.Extensions.Hosting, System.Text.Json, DPAPI, ClientWebSocket, xUnit, FluentAssertions.

---

## 1. Target File Structure

Create this solution layout:

```text
GameSubRelay.sln
src/
  GameSubRelay.Core/
    Audio/
    Captions/
    Configuration/
    Hotkeys/
    Translation/
    Tts/
  GameSubRelay.Infrastructure/
    Audio/
    Configuration/
    Hotkeys/
    Translation/Volcengine/
    Tts/Volcengine/
    Windows/
  GameSubRelay.App/
    App.xaml
    App.xaml.cs
    MainWindow.xaml
    MainWindow.xaml.cs
    Overlay/
    Settings/
    ViewModels/
tests/
  GameSubRelay.Core.Tests/
  GameSubRelay.Infrastructure.Tests/
```

Project responsibilities:

- `GameSubRelay.Core`: no WPF, no NAudio, no network. Holds models, interfaces, caption aggregation, queue policy, config validation, hotkey parsing.
- `GameSubRelay.Infrastructure`: adapters for Windows audio, Volcengine APIs, encrypted config, global hotkeys, Win32 window flags.
- `GameSubRelay.App`: WPF windows, styles, view models, dependency injection composition, application lifecycle.
- `tests`: fast tests for Core and adapter behavior with fakes.

## 2. Milestone Map

| Milestone | Deliverable | Verification |
| --- | --- | --- |
| M1 | Solution scaffold and Core contracts | `dotnet build`, Core tests pass |
| M2 | Config store and device enumeration | Settings can list devices; config persists |
| M3 | Audio frame pipeline | Fake and real capture can produce PCM16 mono 16 kHz frames |
| M4 | Translation session and fake provider | Captions appear in Overlay from fake provider |
| M5 | Volcengine speech translation | Real mic/system audio produces source and translated captions |
| M6 | Overlay play/edit modes | Click-through works in play mode; edit mode persists layout |
| M7 | TTS output | Final translations can be spoken to selected output device |
| M8 | Packaging and manual QA | Windows build artifact and verification checklist complete |

## 3. Tasks

### Task 1: Create .NET Solution Scaffold

**Files:**

- Create: `GameSubRelay.sln`
- Create: `src/GameSubRelay.Core/GameSubRelay.Core.csproj`
- Create: `src/GameSubRelay.Infrastructure/GameSubRelay.Infrastructure.csproj`
- Create: `src/GameSubRelay.App/GameSubRelay.App.csproj`
- Create: `tests/GameSubRelay.Core.Tests/GameSubRelay.Core.Tests.csproj`
- Create: `tests/GameSubRelay.Infrastructure.Tests/GameSubRelay.Infrastructure.Tests.csproj`

- [ ] Create the solution and projects.

Run:

```powershell
dotnet new sln -n GameSubRelay
dotnet new classlib -n GameSubRelay.Core -o src/GameSubRelay.Core
dotnet new classlib -n GameSubRelay.Infrastructure -o src/GameSubRelay.Infrastructure
dotnet new wpf -n GameSubRelay.App -o src/GameSubRelay.App
dotnet new xunit -n GameSubRelay.Core.Tests -o tests/GameSubRelay.Core.Tests
dotnet new xunit -n GameSubRelay.Infrastructure.Tests -o tests/GameSubRelay.Infrastructure.Tests
dotnet sln add src/GameSubRelay.Core/GameSubRelay.Core.csproj
dotnet sln add src/GameSubRelay.Infrastructure/GameSubRelay.Infrastructure.csproj
dotnet sln add src/GameSubRelay.App/GameSubRelay.App.csproj
dotnet sln add tests/GameSubRelay.Core.Tests/GameSubRelay.Core.Tests.csproj
dotnet sln add tests/GameSubRelay.Infrastructure.Tests/GameSubRelay.Infrastructure.Tests.csproj
```

- [ ] Add project references.

Run:

```powershell
dotnet add src/GameSubRelay.Infrastructure/GameSubRelay.Infrastructure.csproj reference src/GameSubRelay.Core/GameSubRelay.Core.csproj
dotnet add src/GameSubRelay.App/GameSubRelay.App.csproj reference src/GameSubRelay.Core/GameSubRelay.Core.csproj
dotnet add src/GameSubRelay.App/GameSubRelay.App.csproj reference src/GameSubRelay.Infrastructure/GameSubRelay.Infrastructure.csproj
dotnet add tests/GameSubRelay.Core.Tests/GameSubRelay.Core.Tests.csproj reference src/GameSubRelay.Core/GameSubRelay.Core.csproj
dotnet add tests/GameSubRelay.Infrastructure.Tests/GameSubRelay.Infrastructure.Tests.csproj reference src/GameSubRelay.Core/GameSubRelay.Core.csproj
dotnet add tests/GameSubRelay.Infrastructure.Tests/GameSubRelay.Infrastructure.Tests.csproj reference src/GameSubRelay.Infrastructure/GameSubRelay.Infrastructure.csproj
```

- [ ] Add NuGet packages.

Run:

```powershell
dotnet add src/GameSubRelay.Infrastructure/GameSubRelay.Infrastructure.csproj package NAudio
dotnet add src/GameSubRelay.App/GameSubRelay.App.csproj package Microsoft.Extensions.Hosting
dotnet add src/GameSubRelay.App/GameSubRelay.App.csproj package Microsoft.Extensions.DependencyInjection
dotnet add tests/GameSubRelay.Core.Tests/GameSubRelay.Core.Tests.csproj package FluentAssertions
dotnet add tests/GameSubRelay.Infrastructure.Tests/GameSubRelay.Infrastructure.Tests.csproj package FluentAssertions
```

- [ ] Verify scaffold.

Run:

```powershell
dotnet build
dotnet test
```

Expected: build succeeds and both empty test projects pass.

- [ ] Commit.

```powershell
git add GameSubRelay.sln src tests
git commit -m "chore: scaffold Windows desktop solution"
```

### Task 2: Add Core Models and Provider Interfaces

**Files:**

- Create: `src/GameSubRelay.Core/Audio/AudioChannelId.cs`
- Create: `src/GameSubRelay.Core/Audio/AudioFrame.cs`
- Create: `src/GameSubRelay.Core/Captions/CaptionLine.cs`
- Create: `src/GameSubRelay.Core/Captions/SegmentStability.cs`
- Create: `src/GameSubRelay.Core/Translation/TranslationSegment.cs`
- Create: `src/GameSubRelay.Core/Translation/ISpeechTranslationSession.cs`
- Create: `src/GameSubRelay.Core/Translation/ISpeechTranslationProvider.cs`
- Create: `src/GameSubRelay.Core/Tts/ITtsProvider.cs`
- Create: `tests/GameSubRelay.Core.Tests/ModelDefaultsTests.cs`

- [ ] Add the core records and enums.

Use these signatures:

```csharp
namespace GameSubRelay.Core.Audio;

public enum AudioChannelId
{
    Microphone = 1,
    Monitor = 2
}

public sealed record AudioFrame(
    AudioChannelId ChannelId,
    byte[] Pcm16Mono16Khz,
    TimeSpan CapturedAt,
    TimeSpan Duration);
```

```csharp
namespace GameSubRelay.Core.Captions;

public enum SegmentStability
{
    Interim,
    Final
}

public sealed record CaptionLine(
    Guid Id,
    AudioChannelId ChannelId,
    string ChannelLabel,
    string SourceText,
    string TranslatedText,
    SegmentStability Stability,
    DateTimeOffset UpdatedAt);
```

```csharp
namespace GameSubRelay.Core.Translation;

public sealed record TranslationSegment(
    AudioChannelId ChannelId,
    long ProviderSequence,
    string SourceLanguage,
    string TargetLanguage,
    string SourceText,
    string TranslatedText,
    SegmentStability Stability,
    TimeSpan BeginTime,
    TimeSpan EndTime);

public interface ISpeechTranslationSession : IAsyncDisposable
{
    ValueTask SendAudioAsync(AudioFrame frame, CancellationToken cancellationToken);
    ValueTask CompleteAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<TranslationSegment> ReadSegmentsAsync(CancellationToken cancellationToken);
}

public interface ISpeechTranslationProvider
{
    Task<ISpeechTranslationSession> StartSessionAsync(
        AudioChannelId channelId,
        SpeechTranslationSessionOptions options,
        CancellationToken cancellationToken);
}
```

- [ ] Add `SpeechTranslationSessionOptions` with source language, target language, region, and optional hot words.
- [ ] Add `ITtsProvider` returning an async stream of PCM audio chunks or a completed PCM buffer.
- [ ] Write a small test proving the two channel IDs keep stable numeric values.
- [ ] Run `dotnet test tests/GameSubRelay.Core.Tests`.
- [ ] Commit.

```powershell
git add src/GameSubRelay.Core tests/GameSubRelay.Core.Tests
git commit -m "feat: add core audio and translation contracts"
```

### Task 3: Implement Configuration Models and Persistence

**Files:**

- Create: `src/GameSubRelay.Core/Configuration/AppSettings.cs`
- Create: `src/GameSubRelay.Core/Configuration/SecretSettings.cs`
- Create: `src/GameSubRelay.Core/Configuration/ISettingsStore.cs`
- Create: `src/GameSubRelay.Infrastructure/Configuration/JsonSettingsStore.cs`
- Create: `src/GameSubRelay.Infrastructure/Configuration/DpapiSecretStore.cs`
- Create: `tests/GameSubRelay.Core.Tests/AppSettingsTests.cs`
- Create: `tests/GameSubRelay.Infrastructure.Tests/JsonSettingsStoreTests.cs`

- [ ] Define settings records matching the design document: audio, translation, overlay, hotkeys.
- [ ] Set defaults:
  - `sourceLanguage`: `en`
  - `targetLanguage`: `zh`
  - `region`: `cn-north-1`
  - overlay opacity `0.65`
  - font size `22`
  - max lines `6`
  - TTS disabled
- [ ] Implement JSON persistence under `%APPDATA%\GameSubRelay\settings.json`.
- [ ] Implement DPAPI secret persistence under `%APPDATA%\GameSubRelay\secrets.json.dpapi`.
- [ ] Tests:
  - default settings serialize and deserialize without losing values.
  - `maxLines` clamps to 1-12.
  - opacity clamps to 0.2-0.95.
  - secret store round-trips using a temporary directory.
- [ ] Run tests.

```powershell
dotnet test tests/GameSubRelay.Core.Tests
dotnet test tests/GameSubRelay.Infrastructure.Tests
```

- [ ] Commit.

```powershell
git add src/GameSubRelay.Core/Configuration src/GameSubRelay.Infrastructure/Configuration tests
git commit -m "feat: persist app settings and encrypted secrets"
```

### Task 4: Implement Audio Device Enumeration

**Files:**

- Create: `src/GameSubRelay.Core/Audio/AudioDeviceInfo.cs`
- Create: `src/GameSubRelay.Core/Audio/IAudioDeviceService.cs`
- Create: `src/GameSubRelay.Infrastructure/Audio/NaudioDeviceService.cs`
- Create: `tests/GameSubRelay.Infrastructure.Tests/AudioDeviceMappingTests.cs`

- [ ] Add a device model with ID, display name, flow direction, state, and default flag.
- [ ] Implement microphone enumeration from active capture devices.
- [ ] Implement monitor/output enumeration from active render devices.
- [ ] Mark the Windows default capture and render devices.
- [ ] Keep device IDs stable by storing the MMDevice ID string.
- [ ] Add mapping tests using a pure mapper function so CI does not require real devices.
- [ ] Manual check on Windows:

```powershell
dotnet run --project src/GameSubRelay.App
```

Expected: settings window can list microphone and playback devices after Task 11 wiring.

- [ ] Commit.

```powershell
git add src/GameSubRelay.Core/Audio src/GameSubRelay.Infrastructure/Audio tests
git commit -m "feat: enumerate Windows audio devices"
```

### Task 5: Build PCM16 Mono 16 kHz Audio Pipeline

**Files:**

- Create: `src/GameSubRelay.Core/Audio/IAudioFrameSource.cs`
- Create: `src/GameSubRelay.Infrastructure/Audio/Pcm16Mono16KhzConverter.cs`
- Create: `src/GameSubRelay.Infrastructure/Audio/MicrophoneCaptureService.cs`
- Create: `src/GameSubRelay.Infrastructure/Audio/LoopbackCaptureService.cs`
- Create: `tests/GameSubRelay.Infrastructure.Tests/Pcm16Mono16KhzConverterTests.cs`

- [ ] Write tests for converting stereo float 48 kHz input to mono PCM16 16 kHz.
- [ ] Implement the converter with NAudio resampling.
- [ ] Implement microphone capture using selected capture device.
- [ ] Implement loopback capture using selected render device.
- [ ] Emit `AudioFrame` chunks of 100-200 ms.
- [ ] Add cancellation and disposal so stopping capture releases WASAPI handles.
- [ ] Manual check:
  - mic capture produces non-empty frames.
  - loopback capture produces frames while audio plays on the selected render device.
- [ ] Run tests and build.

```powershell
dotnet test tests/GameSubRelay.Infrastructure.Tests
dotnet build
```

- [ ] Commit.

```powershell
git add src/GameSubRelay.Core/Audio src/GameSubRelay.Infrastructure/Audio tests
git commit -m "feat: capture normalized audio frames"
```

### Task 6: Implement Caption Aggregation

**Files:**

- Create: `src/GameSubRelay.Core/Captions/CaptionStore.cs`
- Create: `src/GameSubRelay.Core/Captions/CaptionStoreOptions.cs`
- Create: `src/GameSubRelay.Core/Captions/ChannelLabelProvider.cs`
- Create: `tests/GameSubRelay.Core.Tests/CaptionStoreTests.cs`

- [ ] Write tests for source-first, translation-first, interim update, final update, and max-line trimming.
- [ ] Implement aggregation keyed by `ChannelId + ProviderSequence`.
- [ ] Preserve a single visible row for interim updates.
- [ ] Replace interim text when final text arrives for the same sequence.
- [ ] Trim old lines after appending beyond `maxLines`.
- [ ] Expose an event or observable snapshot API for the WPF Overlay ViewModel.
- [ ] Run tests.

```powershell
dotnet test tests/GameSubRelay.Core.Tests --filter CaptionStore
```

- [ ] Commit.

```powershell
git add src/GameSubRelay.Core/Captions tests/GameSubRelay.Core.Tests
git commit -m "feat: aggregate translation segments into captions"
```

### Task 7: Add Fake Translation Provider for UI Development

**Files:**

- Create: `src/GameSubRelay.Infrastructure/Translation/FakeSpeechTranslationProvider.cs`
- Create: `tests/GameSubRelay.Infrastructure.Tests/FakeSpeechTranslationProviderTests.cs`

- [ ] Implement a provider that accepts audio frames and emits deterministic sample segments for each channel.
- [ ] Use short sample text:
  - Mic source: `hello team`
  - Mic translation: `你好，队友`
  - Monitor source: `enemy on the left`
  - Monitor translation: `敌人在左边`
- [ ] Add tests proving the fake provider emits both interim and final segments.
- [ ] Use this provider behind a `Fake` provider setting so Overlay and settings can be developed without API credentials.
- [ ] Commit.

```powershell
git add src/GameSubRelay.Infrastructure/Translation tests/GameSubRelay.Infrastructure.Tests
git commit -m "test: add fake translation provider"
```

### Task 8: Implement Volcengine Speech Translation Provider

**Files:**

- Create: `src/GameSubRelay.Infrastructure/Translation/Volcengine/VolcengineSpeechTranslationProvider.cs`
- Create: `src/GameSubRelay.Infrastructure/Translation/Volcengine/VolcengineSpeechTranslationSession.cs`
- Create: `src/GameSubRelay.Infrastructure/Translation/Volcengine/VolcengineSigner.cs`
- Create: `src/GameSubRelay.Infrastructure/Translation/Volcengine/VolcengineSubtitleParser.cs`
- Create: `tests/GameSubRelay.Infrastructure.Tests/VolcengineSubtitleParserTests.cs`
- Create: `docs/api/volcengine-speech-translate.md`

- [ ] Document the official endpoint, audio format, packet cadence, request shape, response shape, and error codes in `docs/api/volcengine-speech-translate.md`.
- [ ] Implement request signing using AK/SK, service `translate`, region from settings, and path `/api/translate/speech/v1/`.
- [ ] Send initial `Configuration` with source language and target language.
- [ ] Base64 encode each `AudioFrame.Pcm16Mono16Khz` and send it as `AudioData`.
- [ ] Parse `Subtitle` events into source/target partial records.
- [ ] Pair source and target records into `TranslationSegment`.
- [ ] Handle errors:
  - `-401` and `-403`: stop and surface auth error.
  - `-429`: surface rate limit and allow orchestrator cooldown.
  - `-301`: reconnect because packet cadence was broken.
  - `-5xx`: retry with backoff.
- [ ] Tests:
  - parser handles interim and final subtitle payloads.
  - parser handles source and target languages for same sequence.
  - error response maps to a typed provider exception.
- [ ] Manual verification with real credentials:

```powershell
dotnet run --project src/GameSubRelay.App
```

Expected: microphone speech produces translated captions in the fake or real Overlay pipeline.

- [ ] Commit.

```powershell
git add src/GameSubRelay.Infrastructure/Translation docs/api tests
git commit -m "feat: connect Volcengine speech translation"
```

### Task 9: Build Overlay Window and ViewModel

**Files:**

- Create: `src/GameSubRelay.App/Overlay/OverlayWindow.xaml`
- Create: `src/GameSubRelay.App/Overlay/OverlayWindow.xaml.cs`
- Create: `src/GameSubRelay.App/Overlay/OverlayViewModel.cs`
- Create: `src/GameSubRelay.Infrastructure/Windows/WindowClickThroughService.cs`
- Create: `src/GameSubRelay.Infrastructure/Windows/DpiService.cs`
- Create: `tests/GameSubRelay.Core.Tests/OverlaySettingsValidationTests.cs`

- [ ] Create a transparent borderless WPF window.
- [ ] Bind caption rows to `OverlayViewModel`.
- [ ] Use black semi-transparent background and white text.
- [ ] Render channel label, source text, and translated text per line.
- [ ] Apply configured opacity, font size, max line count, width, height, left, and top.
- [ ] Implement play mode by applying `WS_EX_LAYERED`, `WS_EX_TRANSPARENT`, and `WS_EX_TOOLWINDOW`.
- [ ] Implement edit mode by removing `WS_EX_TRANSPARENT` and showing resize/drag handles.
- [ ] Persist overlay bounds and style changes through `ISettingsStore`.
- [ ] Manual checks:
  - play mode does not intercept mouse clicks.
  - edit mode can move and resize the window.
  - returning to play mode restores click-through behavior.
- [ ] Commit.

```powershell
git add src/GameSubRelay.App/Overlay src/GameSubRelay.Infrastructure/Windows tests
git commit -m "feat: add click-through subtitle overlay"
```

### Task 10: Implement Global Hotkeys

**Files:**

- Create: `src/GameSubRelay.Core/Hotkeys/HotkeyGesture.cs`
- Create: `src/GameSubRelay.Core/Hotkeys/HotkeyParser.cs`
- Create: `src/GameSubRelay.Infrastructure/Hotkeys/GlobalHotkeyService.cs`
- Create: `tests/GameSubRelay.Core.Tests/HotkeyParserTests.cs`

- [ ] Parse gestures such as `Ctrl+Alt+S`, `Ctrl+Alt+E`, and `Ctrl+Alt+C`.
- [ ] Reject invalid or duplicate gestures in settings validation.
- [ ] Implement Win32 `RegisterHotKey` and `UnregisterHotKey`.
- [ ] Expose events:
  - toggle overlay visibility.
  - toggle edit mode.
  - clear captions.
- [ ] Surface registration failure in the settings UI so users can change conflicting shortcuts.
- [ ] Tests:
  - parser handles modifier ordering.
  - duplicate hotkeys are detected.
  - invalid key names are rejected.
- [ ] Commit.

```powershell
git add src/GameSubRelay.Core/Hotkeys src/GameSubRelay.Infrastructure/Hotkeys tests
git commit -m "feat: register configurable global hotkeys"
```

### Task 11: Build Settings Window

**Files:**

- Modify: `src/GameSubRelay.App/MainWindow.xaml`
- Modify: `src/GameSubRelay.App/MainWindow.xaml.cs`
- Create: `src/GameSubRelay.App/Settings/AudioSettingsView.xaml`
- Create: `src/GameSubRelay.App/Settings/TranslationSettingsView.xaml`
- Create: `src/GameSubRelay.App/Settings/OverlaySettingsView.xaml`
- Create: `src/GameSubRelay.App/Settings/TtsSettingsView.xaml`
- Create: `src/GameSubRelay.App/Settings/HotkeySettingsView.xaml`
- Create: `src/GameSubRelay.App/ViewModels/SettingsViewModel.cs`

- [ ] Add tabs or sections for Audio, Translation, Overlay, TTS Output, and Hotkeys.
- [ ] Bind microphone, monitor, and TTS output device selectors.
- [ ] Bind Volcengine credential fields without echoing secrets back into logs.
- [ ] Add source language and target language selection.
- [ ] Add connection test button for translation credentials.
- [ ] Add overlay live preview controls for opacity, font size, max lines, and edit mode.
- [ ] Add TTS enable switch and per-channel toggles.
- [ ] Warn when monitor render device equals TTS output render device.
- [ ] Save settings on explicit Apply and on clean application exit.
- [ ] Manual checks:
  - settings persist across restart.
  - secret fields persist encrypted.
  - changing overlay settings updates the Overlay without restarting.
- [ ] Commit.

```powershell
git add src/GameSubRelay.App/Settings src/GameSubRelay.App/ViewModels src/GameSubRelay.App/MainWindow*
git commit -m "feat: add settings window"
```

### Task 12: Wire Application Orchestration

**Files:**

- Modify: `src/GameSubRelay.App/App.xaml.cs`
- Create: `src/GameSubRelay.App/AppHost.cs`
- Create: `src/GameSubRelay.Core/Runtime/ChannelRuntimeState.cs`
- Create: `src/GameSubRelay.Infrastructure/Runtime/TranslationChannelWorker.cs`
- Create: `src/GameSubRelay.Infrastructure/Runtime/AppRuntimeService.cs`

- [ ] Build a hosted service that starts enabled channels.
- [ ] For each channel:
  - start capture source.
  - start translation session.
  - send frames to provider.
  - read provider segments into `CaptionStore`.
  - report state to settings UI.
- [ ] Restart a channel when its device, language, or Provider settings change.
- [ ] Use bounded queues so capture cannot grow memory if network stalls.
- [ ] Implement graceful stop on app exit.
- [ ] Manual checks:
  - disabling microphone stops only microphone capture.
  - disabling monitor stops only loopback capture.
  - provider error on one channel does not stop the other channel.
- [ ] Commit.

```powershell
git add src/GameSubRelay.App/App.xaml.cs src/GameSubRelay.App/AppHost.cs src/GameSubRelay.Core/Runtime src/GameSubRelay.Infrastructure/Runtime
git commit -m "feat: orchestrate capture and translation channels"
```

### Task 13: Implement TTS Provider and Output Playback

**Files:**

- Create: `src/GameSubRelay.Core/Tts/TtsRequest.cs`
- Create: `src/GameSubRelay.Core/Tts/TtsQueuePolicy.cs`
- Create: `src/GameSubRelay.Infrastructure/Tts/Volcengine/VolcengineTtsProvider.cs`
- Create: `src/GameSubRelay.Infrastructure/Tts/Volcengine/VolcengineTtsProtocol.cs`
- Create: `src/GameSubRelay.Infrastructure/Tts/TtsOutputWorker.cs`
- Create: `src/GameSubRelay.Infrastructure/Audio/AudioOutputDevicePlayer.cs`
- Create: `tests/GameSubRelay.Core.Tests/TtsQueuePolicyTests.cs`
- Create: `docs/api/volcengine-tts.md`

- [ ] Document the official TTS WebSocket endpoint, Bearer Token auth, appid/cluster requirements, and single-synthesis connection behavior in `docs/api/volcengine-tts.md`.
- [ ] Implement queue policy:
  - only final translated segments enqueue.
  - max pending requests default to 3.
  - when full, drop the oldest pending item.
  - per-channel toggles decide whether a segment enters the queue.
- [ ] Implement TTS WebSocket protocol wrapper.
- [ ] Decode returned audio chunks into a playable format.
- [ ] Play audio to the selected render device through NAudio.
- [ ] Surface TTS failures without modifying CaptionStore.
- [ ] Tests:
  - queue drops oldest item when full.
  - disabled channel does not enqueue.
  - fake TTS failure does not throw through caption flow.
- [ ] Manual checks:
  - output to headphones plays translated speech.
  - output to VB-Cable can be selected as an input in Discord or a game.
- [ ] Commit.

```powershell
git add src/GameSubRelay.Core/Tts src/GameSubRelay.Infrastructure/Tts src/GameSubRelay.Infrastructure/Audio docs/api tests
git commit -m "feat: output translated speech"
```

### Task 14: Add Status UI and Error Surfacing

**Files:**

- Create: `src/GameSubRelay.Core/Runtime/AppStatus.cs`
- Create: `src/GameSubRelay.App/ViewModels/StatusViewModel.cs`
- Modify: `src/GameSubRelay.App/MainWindow.xaml`
- Modify: `src/GameSubRelay.App/Overlay/OverlayWindow.xaml`

- [ ] Show per-channel state: stopped, starting, capturing, translating, reconnecting, error.
- [ ] Show concise error messages for auth, rate limit, device unavailable, hotkey conflict, and TTS output failure.
- [ ] Show provider cooldown/retry state when reconnecting.
- [ ] In Overlay, show a small non-intrusive channel status indicator only when a channel is not healthy.
- [ ] Keep detailed errors in logs, not in Overlay text blocks.
- [ ] Manual checks:
  - unplug selected microphone and verify error appears.
  - enter invalid credentials and verify auth message appears.
  - set duplicate hotkey and verify conflict message appears.
- [ ] Commit.

```powershell
git add src/GameSubRelay.Core/Runtime src/GameSubRelay.App/ViewModels src/GameSubRelay.App/MainWindow.xaml src/GameSubRelay.App/Overlay/OverlayWindow.xaml
git commit -m "feat: show channel status and errors"
```

### Task 15: Add Logging and Privacy Controls

**Files:**

- Create: `src/GameSubRelay.Core/Diagnostics/DiagnosticsOptions.cs`
- Create: `src/GameSubRelay.Infrastructure/Diagnostics/AppLogger.cs`
- Modify: `src/GameSubRelay.App/Settings/TranslationSettingsView.xaml`
- Modify: `src/GameSubRelay.App/Settings/AudioSettingsView.xaml`

- [ ] Log lifecycle events, device IDs, provider error codes, reconnect attempts, and settings validation failures.
- [ ] Do not log raw audio.
- [ ] Do not log full API secrets.
- [ ] Do not log full subtitle text by default.
- [ ] Add an explicit debug switch for subtitle event logging with UI privacy warning.
- [ ] Store logs under `%LOCALAPPDATA%\GameSubRelay\logs`.
- [ ] Manual check: run the app, trigger a fake provider error, inspect logs for useful diagnostics without secrets.
- [ ] Commit.

```powershell
git add src/GameSubRelay.Core/Diagnostics src/GameSubRelay.Infrastructure/Diagnostics src/GameSubRelay.App/Settings
git commit -m "feat: add privacy-aware diagnostics"
```

### Task 16: Package and Verify MVP

**Files:**

- Create: `scripts/build.ps1`
- Create: `docs/qa/mvp-manual-checklist.md`
- Modify: `README.md`

- [ ] Add build script.

```powershell
dotnet restore
dotnet test
dotnet publish src/GameSubRelay.App/GameSubRelay.App.csproj -c Release -r win-x64 --self-contained false -o dist/win-x64
```

- [ ] Add QA checklist covering:
  - Windows version.
  - audio devices tested.
  - microphone captions.
  - monitor captions.
  - click-through Overlay.
  - edit mode.
  - hotkeys.
  - TTS real device output.
  - TTS VB-Cable output.
  - settings persistence.
  - invalid credential handling.
- [ ] Update README with setup, limitations, and VB-Cable routing note.
- [ ] Run final verification.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
```

Expected: tests pass and `dist/win-x64` contains a runnable app.

- [ ] Commit.

```powershell
git add scripts docs/qa README.md
git commit -m "docs: add MVP build and QA checklist"
```

## 4. Acceptance Criteria

MVP is complete when:

- User can select microphone, monitor render device, and TTS output render device.
- Microphone channel and monitor channel can be enabled independently.
- Both channels can produce source and translated captions through the real Provider.
- Overlay is topmost over normal desktop windows and borderless/windowed fullscreen games.
- Overlay play mode is mouse-through.
- Overlay edit mode supports move, resize, opacity, font size, and max line adjustments.
- Hotkeys work when settings window is not focused.
- TTS output can be enabled without affecting captions.
- TTS can target a normal playback device or a VB-Cable playback endpoint.
- Settings and encrypted secrets persist across app restart.
- Errors are visible enough for recovery without exposing secrets in logs.

## 5. Manual QA Matrix

| Scenario | Expected result |
| --- | --- |
| No API credentials | Settings shows credential requirement; fake provider mode can still show sample captions |
| Invalid AK/SK | Translation channel stops with auth error; app remains open |
| No microphone selected | Mic channel stays stopped; monitor can still run |
| Selected monitor device silent | Monitor channel shows capturing state but no captions |
| Output device equals monitored device | Settings shows feedback risk warning |
| Overlay play mode | Mouse clicks reach the game/window underneath |
| Overlay edit mode | Mouse can drag/resize Overlay and controls respond |
| Hotkey conflict | Conflict is shown and previous valid hotkey remains active |
| Volcengine rate limit | Channel cools down and reports rate limit |
| TTS Provider failure | Captions continue; TTS status shows failure |

## 6. Self-Review Notes

- Scope matches the design document: Windows MVP, two input channels, real-time speech translation, dual-channel captions, click-through Overlay, settings, hotkeys, optional TTS output.
- Deferred items are explicit: exclusive fullscreen, process-level audio capture, source-language auto-detection, more than two input channels, and non-Windows platforms.
- No task requires committing secrets or storing API credentials in plaintext.
- The implementation is decomposed so fake Provider and fake audio can validate UI before real API credentials are available.
- The riskiest integrations are isolated behind interfaces: audio capture, speech translation, TTS, hotkeys, and click-through window flags.
