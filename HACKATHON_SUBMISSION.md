# VoxPilot — OpenAI Build Week Submission Draft

## Submission metadata

- **Project name:** VoxPilot
- **Track:** Apps for Your Life
- **Tagline:** Speak naturally and type anywhere on Windows.
- **Repository URL:** TODO
- **Public demo video:** TODO
- **Download/test build:** TODO
- **Core-build Codex Session ID:** TODO — run `/feedback` in the Codex task where most core functionality was built

## Short description

VoxPilot is a compact Windows voice-to-text companion that turns a global keyboard shortcut into accurate typing in almost any application. It records from the default microphone, transcribes through the user's selected OpenAI or OpenRouter provider, and injects the resulting Unicode text into the application that was active when dictation began.

Unlike transcription tools that require users to work inside a dedicated editor, VoxPilot stays out of the way. It offers an always-on-top controller, a no-focus waveform widget while minimized, system-tray controls, configurable shortcuts, automatic language detection, secure per-provider API-key storage, optional clipboard copying, and automatic silence detection.

## Inspiration

Voice input is useful, but changing applications, copying text, and restoring focus breaks the user's flow. VoxPilot was designed around a simpler interaction: place the caret anywhere, press one shortcut, speak, and continue working.

## What it does

- Records the default Windows microphone in memory
- Supports selectable OpenAI and OpenRouter transcription providers
- Stores each provider key separately in Windows Credential Manager
- Transcribes with automatic language detection or a selected language hint
- Restores the previous application and types the transcript at the caret
- Supports global record and standby shortcuts
- Shows live audio feedback without stealing keyboard focus
- Provides tray recovery, clipboard copying, start-with-Windows, and silence-stop options

## How we built it

VoxPilot is a native .NET 10 WPF application written in C#. NAudio captures WAV audio in memory. Dedicated HTTP clients handle OpenAI and OpenRouter transcription requests. Windows APIs provide global hotkeys, secure Credential Manager integration, foreground-window tracking, Unicode input injection, tray behavior, and startup registration.

Codex with GPT-5.6 served as the primary engineering collaborator throughout development. It helped scaffold the application, implement and debug Windows integrations, iterate on the visual experience through rendered previews, package the self-contained build, and verify the final source and release artifact. The entrant directed the product scope, native-Windows architecture, interaction model, privacy constraints, provider support, and final design decisions.

## Challenges

The hardest part was reliably returning text to the correct destination application without allowing the floating UI or dictation widget to steal focus. Windows also intentionally blocks a non-elevated process from injecting input into an elevated application. VoxPilot handles normal focus restoration, explains the privilege limitation clearly, and falls back to copying the transcript when direct typing is blocked.

Another challenge was presenting a useful recording and processing state while the main window was hidden. The separate waveform widget is deliberately non-activating so users can see progress without losing their caret position.

## Accomplishments

- A complete native product experience rather than a transcription proof of concept
- Reliable global-shortcut workflow across ordinary Windows applications
- Memory-only recording and secure provider-specific credential storage
- A polished compact controller and non-focus-stealing dictation widget
- A self-contained Windows build that judges can run without installing the .NET SDK
- OpenAI and OpenRouter support behind one coherent provider selector

## What we learned

Building dependable desktop voice input requires more than transcription accuracy. Focus ownership, window privilege levels, keyboard injection, secret storage, audio lifecycle, recovery from the tray, and clear visual state all materially affect whether the experience feels trustworthy.

## What's next

- Optional local/offline transcription
- User-defined vocabulary and formatting profiles
- Streaming transcription for lower perceived latency
- Signed installers and automatic updates
- Additional accessibility controls and richer language-specific formatting

## Built with

Codex, GPT-5.6, C#, .NET 10, WPF, NAudio, OpenAI API, OpenRouter API, Windows Credential Manager, Win32 hotkeys, and Unicode input injection.
