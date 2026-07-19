# VoxPilot Demo Script

Target length: 2 minutes 40 seconds. Keep all API keys hidden.

## 0:00–0:15 — Problem and hook

“Voice typing should work wherever the cursor already is. VoxPilot is a compact Windows companion that lets me press one shortcut, speak naturally, and type the result into almost any application.”

Show the VoxPilot main window beside an empty text editor.

## 0:15–0:40 — Product setup

Show the provider selector and briefly switch between OpenAI and OpenRouter.

“Each provider keeps its own model choice and API key. Keys are stored in Windows Credential Manager, and recordings stay in memory.”

Do not display or type a real key on camera. Have the selected provider configured before recording.

## 0:40–1:15 — Core workflow

Place the caret in the text editor and press **Ctrl + Shift + F9**.

Say: “VoxPilot lets me dictate directly into the application I am already using, without interrupting my workflow.”

Press the shortcut again. Show the processing state and the completed text appearing at the caret.

## 1:15–1:45 — Differentiating experience

Minimize or hide VoxPilot, then dictate a second short sentence.

Point out the no-focus waveform widget, automatic language option, tray recovery, clipboard option, configurable shortcuts, standby mode, and optional silence stopping.

## 1:45–2:15 — Technical implementation

Show the repository briefly.

“VoxPilot is a native C# and .NET WPF application. NAudio records WAV audio in memory, dedicated clients call the selected transcription provider, Windows Credential Manager protects secrets, and Win32 integrations handle global shortcuts, focus restoration, and Unicode text injection.”

## 2:15–2:35 — Codex and GPT-5.6 collaboration

Show the README collaboration section and a safe view of the primary Codex task.

“We built VoxPilot with Codex and GPT-5.6 as our engineering collaborator. Codex accelerated the WPF implementation, Windows integration debugging, provider support, visual iteration, packaging, and verification. We retained the product, privacy, architecture, and interaction decisions.”

## 2:35–2:45 — Close

“VoxPilot turns voice into text exactly where the work is happening: one shortcut, any ordinary Windows app.”

End on the working application and project name.
