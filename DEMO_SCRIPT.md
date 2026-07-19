# VoxPilot Demo Script

Target length: 2 minutes 50 seconds. Keep all API keys hidden.

## 0:00–0:15 — Problem and hook

“Voice typing should work wherever the cursor already is. VoxPilot is a compact Windows companion that lets me press one shortcut, speak naturally, and type the result into almost any application.”

Show the VoxPilot main window beside an empty text editor.

## 0:15–0:40 — Guided setup

Show the quick-setup card, provider selector, microphone selector, and **Test mic** button. Briefly switch between OpenAI and OpenRouter.

“Each provider keeps its own model choice and API key. Keys are stored in Windows Credential Manager, recordings stay in memory, and I can verify my microphone without sending audio anywhere.”

Do not display or type a real key on camera. Have the selected provider configured before recording.

## 0:40–1:15 — Core workflow

Place the caret in the text editor and press **Ctrl + Shift + F9**.

Say: “VoxPilot lets me dictate directly into the application I am already using, without interrupting my workflow.”

Press the shortcut again. Show the processing state and the completed text appearing at the caret. Briefly mention that **Esc** cancels and sends nothing.

## 1:15–1:55 — Differentiating experience

Choose **Hold to talk** and **Polished**, minimize VoxPilot, then hold the shortcut while dictating a second short sentence.

Release the shortcut and show the cleaned result. Point out the no-focus waveform widget, tray recovery, Exact/Polished/Notes choices, automatic language option, configurable shortcuts, standby mode, and optional silence stopping.

“Exact returns the provider transcript unchanged. Polished and Notes use GPT-5.6 Terra, and VoxPilot safely falls back to exact text if that formatting pass is unavailable.”

## 1:55–2:20 — Technical implementation

Show the repository briefly.

“VoxPilot is a native C# and .NET WPF application. NAudio records WAV audio in memory, dedicated clients call the selected transcription provider, GPT-5.6 smart text uses the OpenAI Responses API, Windows Credential Manager protects secrets, and Win32 integrations handle global shortcuts, key release, focus restoration, and Unicode text injection.”

## 2:20–2:40 — Codex and GPT-5.6 collaboration

Show the README collaboration section and a safe view of the primary Codex task.

“We built VoxPilot with Codex and GPT-5.6 as our engineering collaborator. Codex accelerated the WPF implementation, Windows integration debugging, provider support, visual iteration, packaging, and verification. We retained the product, privacy, architecture, and interaction decisions.”

## 2:40–2:50 — Close

“VoxPilot turns voice into text exactly where the work is happening: one shortcut, any ordinary Windows app.”

End on the working application and project name.
