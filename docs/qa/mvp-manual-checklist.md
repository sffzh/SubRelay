# MVP Manual QA Checklist

This checklist is intended for post-merge verification after all layers are present.

## Baseline

- [ ] Windows 10/11 runtime target confirmed
- [ ] Windows audio devices enumerate
- [ ] No unresolved App startup DI errors (`dotnet run --project src/GameSubRelay.App`)
- [ ] `scripts/build.ps1` passes restore/build/test (and publish when App project exists)

## Functional Coverage

- [ ] Microphone channel captions can be produced
- [ ] Monitor loopback channel captions can be produced
- [ ] Overlay remains topmost and does not block clicks in play mode
- [ ] Edit mode allows move / resize / opacity / font size / max-lines adjustments
- [ ] Hotkeys work while settings window is not focused:
  - show/hide overlay
  - enter/exit edit mode
  - clear captions
- [ ] TTS can synthesize final translation for enabled channels
- [ ] TTS to real playback device works
- [ ] TTS to VB-Cable playback endpoint works and can be consumed downstream
- [ ] Settings persist across restart
- [ ] Secrets persist encrypted (no plaintext API keys written to readable config)
- [ ] Output-device feedback warning appears when monitor output and TTS output are the same

## Error and Recovery

- [ ] Invalid credentials stop translation and show auth guidance
- [ ] Rate limit surfaces clear retry/cooldown behavior
- [ ] Device loss on active channel shows non-fatal channel error state
- [ ] One channel error does not stop caption flow on the other channel
- [ ] TTS failure does not clear existing captions

## App/DI Alignment Checks

- [ ] App DI wiring composes all required App services from Core/Infrastructure contracts
- [ ] Runtime host starts/stops each enabled channel independently
- [ ] No wiring exceptions from `dotnet build`/`dotnet test` related to App startup
- [ ] `docs/qa/mvp-manual-checklist.md` is updated when behavior changes
