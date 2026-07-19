# VoxPilot

VoxPilot is a compact Windows voice-to-text app that records the default microphone, sends a WAV recording to either OpenAI or OpenRouter for transcription, and can type the result into the application that was active when dictation started.

## Features

- Small always-on-top controller with a draggable custom title bar
- Standard minimize, maximize/restore, and close controls
- Recoverable from both the Windows taskbar and the VoxPilot notification-area icon
- Live microphone-level visualization and clear ready/listening/working/standby states
- Selectable OpenAI or OpenRouter transcription provider
- Separate API keys stored securely for each provider
- Native OpenAI transcription through `POST /v1/audio/transcriptions`
- OpenRouter transcription through `POST /api/v1/audio/transcriptions`
- Provider-specific transcription models plus editable model IDs
- Dedicated transcription-model filtering (general audio-chat models are excluded)
- Automatic language detection or a manual language hint
- Unicode text injection into the previously focused Windows application
- Configurable global shortcuts for dictation and standby
- System tray controls, optional start with Windows, clipboard copy, and silence stop
- No-focus animated waveform widget when dictating while minimized or hidden
- API key storage in Windows Credential Manager (never in `settings.json`)

## Default shortcuts

- **Ctrl + Shift + F9:** start or stop dictation
- **Ctrl + Shift + F10:** enter standby or resume

Both can be changed under **Settings & shortcuts**.

## Build

Requires Windows and the .NET 10 SDK.

```powershell
dotnet build VoiceFlow.csproj -c Release
dotnet run --project VoiceFlow.csproj -c Release
```

To create the self-contained, single-file Windows build:

```powershell
dotnet publish VoiceFlow.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## Usage notes

1. Choose **OpenAI** or **OpenRouter** as the transcription provider.
2. Enter that provider's API key and select **Save key**.
3. Choose an audio/transcription model and language.
4. Switch to the app where text should appear.
5. Press the dictation shortcut, speak, then press it again.

For the most reliable anywhere-typing workflow, leave the caret in the destination app and use the global shortcut without clicking VoxPilot. If you use VoxPilot's microphone button, it remembers and restores the application directly beneath it before typing.

Drag anywhere in the empty title-bar area to move VoxPilot. The minimize button keeps it in the Windows taskbar; double-click the purple tray icon if you hide or lose sight of the window.

Windows prevents a normal app from injecting input into an administrator-elevated app. For security, microphone audio is held in memory and sent only to the selected provider when recording stops; VoxPilot does not save recordings to disk.
