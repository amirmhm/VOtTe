# VoxPilot Demo Script

Target length: 2 minutes 35–45 seconds. Speak calmly and leave a little time for transcription. Keep every API key and personal notification hidden.

## Before recording

- Configure and test VoxPilot before starting the screen recording.
- Complete the Quick setup card so the main interface looks clean.
- Open Notepad with a large, readable font and leave it empty.
- Select **Press to toggle** and **Exact** for the first demonstration.
- Prepare **Polished** for the second demonstration.
- Disable notifications and close applications containing personal information.
- Keep the GitHub README open at the collaboration section for the final technical segment.

## 0:00–0:20 — Friendly introduction

### Show

VoxPilot beside an empty Notepad window.

### Say

“Hi, I’m [your name], and this is VoxPilot.

I built it because voice typing on Windows often interrupts the work itself. You record in one place, copy the result, switch applications, and paste it somewhere else.

With VoxPilot, I can simply leave my cursor where I’m working, use one shortcut, and speak.”

## 0:20–0:38 — What the app offers

### Show

Briefly point to the provider, microphone, shortcut behavior, and text-style controls. Do not open the API-key field.

### Say

“VoxPilot is a small native Windows companion. I can use OpenAI or OpenRouter for transcription, choose my microphone, and decide whether the shortcut toggles recording or works like push-to-talk.

The guided setup also lets me test my microphone locally before I dictate.”

## 0:38–1:12 — Core workflow

### Show

Click inside Notepad. Press **Ctrl + Shift + F9**, speak the sample sentence, and press the shortcut again. Let the complete result appear at the cursor.

### Dictate

“VoxPilot lets me dictate directly into the application I am already using, so I can keep my attention on the work instead of moving text between windows.”

### Say after the result appears

“And the transcript appears exactly where my cursor was. I didn’t need to reopen VoxPilot, copy anything, or restore the window myself.”

## 1:12–1:48 — Push-to-talk and GPT-5.6

### Show

Select **Hold to talk** and **Polished**, then minimize VoxPilot. Keep Notepad focused. Hold **Ctrl + Shift + F9** while speaking, then release it.

The compact waveform should appear without taking focus. Wait for the polished result.

### Dictate

“Um, I wanted to remind the team that the design review is tomorrow at ten, and, uh, please bring the latest screenshots.”

### Say after the result appears

“Polished mode uses GPT-5.6 Terra to remove filler words and clean punctuation while preserving my meaning.

Notes turns longer thoughts into bullet points, while Exact returns the transcription unchanged.”

## 1:48–2:05 — Privacy and control

### Show

Start another recording and press **Esc**. Show the “Recording cancelled” message.

### Say

“I also wanted cancellation to be unambiguous. Pressing Escape immediately discards the in-memory recording, and nothing is sent to a transcription provider.

API keys are stored in Windows Credential Manager, not in the app’s settings file.”

## 2:05–2:30 — How it was built

### Show

Open the GitHub repository and briefly show the README collaboration section and project files.

### Say

“VoxPilot is a native C# and WPF application using in-memory audio, global shortcuts, foreground-window tracking, and Unicode typing.

I built it with Codex and GPT-5.6 as my engineering collaborator. I defined the product, privacy, and architecture. Codex helped me implement and debug the Windows integration, provider support, interface, testing, and packaging.”

## 2:30–2:42 — Closing

### Show

Return to Notepad with the successfully dictated text and keep the VoxPilot tray widget or main window visible.

### Say

“VoxPilot makes voice input part of Windows: put the cursor where you want the words, speak, and continue working.

That’s VoxPilot. Thank you.”

## Delivery notes

- Speak as if you are showing the app to one person, not reading a feature list.
- Pause after each shortcut so viewers can see the state change.
- If transcription takes longer during recording, shorten the technical paragraph rather than rushing.
- Record the core workflow again if a transcript contains a visible error.
- Keep the final video below three minutes.
