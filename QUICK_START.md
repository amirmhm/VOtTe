# VoxPilot for Windows

VoxPilot is a compact, always-on-top voice-to-text application for Windows 10/11.

## Start

1. Run `VoxPilot.exe`.
2. Choose **OpenAI** or **OpenRouter**.
3. Paste the selected provider's API key and click **Save key**.
4. Choose and test your microphone.
5. Choose **Press to toggle** or **Hold to talk**.
6. Choose **Exact**, **Polished**, or **Notes** text.
7. Focus the Windows app where you want text to appear.
8. Press **Ctrl + Shift + F9** and speak. Press it again in toggle mode, or release it in hold-to-talk mode.

Use **Ctrl + Shift + F10** to toggle standby. Both shortcuts are configurable in the app.

Press **Esc** while recording to cancel. VoxPilot discards that in-memory recording without contacting a transcription provider.

To dictate into another application, click where the text should go and use **Ctrl + Shift + F9** without returning to VoxPilot. VoxPilot will keep that application as the typing target while it transcribes. Starting with the on-screen microphone button also returns to the application directly beneath VoxPilot.

When VoxPilot is minimized or hidden, the dictation shortcut displays a small animated waveform near the bottom of the active screen. It never takes keyboard focus, changes to **Transcribing…** when recording stops, and briefly confirms the result.

The model menu is limited to models compatible with the selected provider's transcription endpoint. OpenAI offers its supported transcription models; OpenRouter refreshes its available speech-to-text models.

**Exact** returns the transcription provider's result unchanged. **Polished** cleans filler words, punctuation, and obvious grammar, while **Notes** creates concise bullet points. The two smart styles require a saved OpenAI key and use GPT-5.6 Terra through the OpenAI Responses API. If smart formatting is unavailable, VoxPilot returns the exact transcript.

Drag the top title bar to reposition VoxPilot. The minimize button hides VoxPilot from the taskbar while it continues running in the Windows notification area. Double-click the purple tray icon to restore it. The middle title-bar button maximizes or restores the window; the X closes the application.

Each provider's API key is stored separately in Windows Credential Manager. Recordings remain in memory and are sent only to the selected provider after a normal stop; they are not saved by VoxPilot.

Note: Windows blocks non-elevated apps from typing into administrator-elevated windows.
