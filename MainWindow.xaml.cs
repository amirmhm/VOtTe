using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using VoxPilot.Models;
using VoxPilot.Services;
using Forms = System.Windows.Forms;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using Brush = System.Windows.Media.Brush;
using Clipboard = System.Windows.Clipboard;
using ColorConverter = System.Windows.Media.ColorConverter;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace VoxPilot;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly AudioRecorder _audioRecorder = new();
    private readonly OpenAIClient _openAI = new();
    private readonly OpenRouterClient _openRouter = new();
    private readonly DispatcherTimer _silenceTimer;
    private readonly Stopwatch _recordingTime = new();
    private readonly AppSettings _settings;
    private readonly System.Drawing.Icon _appIcon;
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly DictationWidget _dictationWidget;
    private readonly bool _previewMode = Environment.GetCommandLineArgs().Any(argument =>
        argument.StartsWith("--render-preview=", StringComparison.OrdinalIgnoreCase));

    private HotkeyService? _hotkeys;
    private AppState _state = AppState.Ready;
    private IntPtr _windowHandle;
    private IntPtr _lastExternalWindow;
    private IntPtr _dictationTargetWindow;
    private DateTime _lastVoiceAt;
    private bool _silenceStopQueued;
    private bool _initialized;
    private bool _isExiting;
    private bool _hasShownTrayHint;
    private string? _capturingHotkey;

    private static readonly LanguageOption[] Languages =
    [
        new("Auto detect", "auto"),
        new("English", "en"),
        new("Arabic", "ar"),
        new("Turkish", "tr"),
        new("Spanish", "es"),
        new("French", "fr"),
        new("German", "de"),
        new("Italian", "it"),
        new("Portuguese", "pt"),
        new("Russian", "ru"),
        new("Chinese", "zh"),
        new("Japanese", "ja"),
        new("Korean", "ko"),
        new("Hindi", "hi"),
        new("Persian", "fa"),
        new("Urdu", "ur")
    ];

    private static readonly ProviderOption[] Providers =
    [
        new("OpenAI", ApiProvider.OpenAI),
        new("OpenRouter", ApiProvider.OpenRouter)
    ];

    public MainWindow()
    {
        InitializeComponent();
        _dictationWidget = new DictationWidget();
        _settings = _previewMode ? new AppSettings() : _settingsService.Load();

        ProviderCombo.ItemsSource = Providers;
        ProviderCombo.SelectedValue = _settings.Provider;
        if (ProviderCombo.SelectedItem is null) ProviderCombo.SelectedIndex = 0;

        LanguageCombo.ItemsSource = Languages;
        LanguageCombo.SelectedValue = _settings.LanguageCode;
        if (LanguageCombo.SelectedItem is null) LanguageCombo.SelectedIndex = 0;

        LoadProviderUi();

        AutoTypeCheck.IsChecked = _settings.AutoType;
        AutoCopyCheck.IsChecked = _settings.AutoCopy;
        AlwaysOnTopCheck.IsChecked = _settings.AlwaysOnTop;
        StartWithWindowsCheck.IsChecked = _settings.StartWithWindows;
        AutoStopCheck.IsChecked = _settings.AutoStopOnSilence;
        Topmost = _settings.AlwaysOnTop;
        RecordHotkeyText.Text = _settings.RecordHotkey.ToDisplayString();
        StandbyHotkeyText.Text = _settings.StandbyHotkey.ToDisplayString();

        if (!double.IsNaN(_settings.WindowLeft) && !double.IsNaN(_settings.WindowTop))
        {
            Left = _settings.WindowLeft;
            Top = _settings.WindowTop;
            WindowStartupLocation = WindowStartupLocation.Manual;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        _audioRecorder.LevelChanged += AudioRecorder_LevelChanged;
        _silenceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _silenceTimer.Tick += SilenceTimer_Tick;

        _appIcon = LoadAppIcon();
        _trayIcon = CreateTrayIcon();
        _initialized = true;
        UpdateState(AppState.Ready);
    }

    private Forms.NotifyIcon CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        var show = new Forms.ToolStripMenuItem("Show VoxPilot");
        show.Click += (_, _) => Dispatcher.Invoke(ShowFromTray);
        var toggle = new Forms.ToolStripMenuItem("Start / stop dictation");
        toggle.Click += (_, _) => Dispatcher.Invoke(async () => await ToggleRecordingAsync());
        var standby = new Forms.ToolStripMenuItem("Standby / resume");
        standby.Click += (_, _) => Dispatcher.Invoke(async () => await ToggleStandbyAsync());
        var exit = new Forms.ToolStripMenuItem("Exit");
        exit.Click += (_, _) => Dispatcher.Invoke(ExitApplication);
        menu.Items.AddRange([show, toggle, standby, new Forms.ToolStripSeparator(), exit]);

        var icon = new Forms.NotifyIcon
        {
            Text = "VoxPilot — ready",
            Icon = (System.Drawing.Icon)_appIcon.Clone(),
            Visible = !_previewMode,
            ContextMenuStrip = menu
        };
        icon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
        return icon;
    }

    private static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            var resource = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/VoxPilot.ico"));
            if (resource?.Stream is not null)
            {
                using var icon = new System.Drawing.Icon(resource.Stream);
                return (System.Drawing.Icon)icon.Clone();
            }
        }
        catch
        {
            // The executable icon remains available as a safe fallback.
        }
        return (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_previewMode)
        {
            UpdateMicAppearance();
            return;
        }
        EnsureWindowOnScreen();
        UpdateMicAppearance();
        if (SelectedProvider == ApiProvider.OpenAI || !string.IsNullOrWhiteSpace(ApiKeyBox.Password))
            await RefreshModelsAsync(false);
        if (Environment.GetCommandLineArgs().Any(a => a.Equals("--background", StringComparison.OrdinalIgnoreCase)))
            HideToTray(false);
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        _hotkeys = new HotkeyService(_windowHandle);
        _hotkeys.Pressed += Hotkeys_Pressed;
        RegisterAllHotkeys();
    }

    private async void Hotkeys_Pressed(object? sender, int id)
    {
        if (_capturingHotkey is not null) return;
        if (id == HotkeyService.RecordId) await ToggleRecordingAsync();
        if (id == HotkeyService.StandbyId) await ToggleStandbyAsync();
    }

    private async void MicButton_Click(object sender, RoutedEventArgs e) => await ToggleRecordingAsync();

    private async Task ToggleRecordingAsync()
    {
        if (_state == AppState.Processing) return;
        if (_state == AppState.Standby)
        {
            SetStatusMessage("VoxPilot is in standby", $"Press {_settings.StandbyHotkey.ToDisplayString()} to resume");
            return;
        }

        if (_audioRecorder.IsRecording)
        {
            await StopAndTranscribeAsync();
            return;
        }

        if (string.IsNullOrWhiteSpace(ApiKeyBox.Password))
        {
            ExpandWindow();
            SettingsExpander.IsExpanded = false;
            ApiKeyBox.Focus();
            UpdateState(AppState.Error, $"Add your {ProviderDisplayName} API key", "Save it securely, then try again");
            return;
        }

        var currentWindow = TextInjector.GetForegroundWindowHandle();
        if (TextInjector.IsValidTargetWindow(currentWindow, _windowHandle))
        {
            // A global hotkey leaves the destination application in the foreground.
            _dictationTargetWindow = currentWindow;
        }
        else
        {
            // If the microphone button was clicked, the window directly beneath VoxPilot is
            // normally the application the user just came from; prefer it over stale history.
            var nextWindow = TextInjector.FindNextApplicationWindow(_windowHandle);
            _dictationTargetWindow = TextInjector.IsValidTargetWindow(nextWindow, _windowHandle)
                ? nextWindow
                : _lastExternalWindow;
        }
        if (!TextInjector.IsValidTargetWindow(_dictationTargetWindow, _windowHandle))
            _dictationTargetWindow = TextInjector.FindNextApplicationWindow(_windowHandle);

        try
        {
            _audioRecorder.Start();
            _recordingTime.Restart();
            _lastVoiceAt = DateTime.UtcNow;
            _silenceStopQueued = false;
            _silenceTimer.Start();
            UpdateState(AppState.Listening);
        }
        catch (Exception exception)
        {
            UpdateState(AppState.Error, "Microphone unavailable", FriendlyError(exception));
        }
    }

    private async Task StopAndTranscribeAsync()
    {
        _silenceTimer.Stop();
        _recordingTime.Stop();
        UpdateState(AppState.Processing);

        try
        {
            var audio = await _audioRecorder.StopAsync();
            ResetWaveform();
            if (_recordingTime.Elapsed < TimeSpan.FromMilliseconds(280) || audio.Length < 1000)
            {
                UpdateState(AppState.Ready, "That was too short", "Speak for a moment, then stop");
                return;
            }

            var model = GetSelectedModelId();
            var language = (LanguageCombo.SelectedItem as LanguageOption)?.Code ?? "auto";
            var transcript = SelectedProvider == ApiProvider.OpenAI
                ? await _openAI.TranscribeAsync(audio, model, language, ApiKeyBox.Password)
                : await _openRouter.TranscribeAsync(audio, model, language, ApiKeyBox.Password);
            if (string.IsNullOrWhiteSpace(transcript))
            {
                UpdateState(AppState.Ready, "No speech detected", "Try again a little closer to the microphone");
                return;
            }

            TranscriptBox.Text = transcript;
            TranscriptBox.Foreground = (Brush)FindResource("TextPrimaryBrush");

            if (_settings.AutoCopy)
            {
                try { Clipboard.SetText(transcript); } catch { }
            }

            if (_settings.AutoType && _dictationTargetWindow != IntPtr.Zero)
            {
                try
                {
                    await TextInjector.TypeAsync(transcript, _dictationTargetWindow);
                    UpdateState(AppState.Ready, "Typed successfully", "Ready for the next thought");
                }
                catch (Exception typingException)
                {
                    try { Clipboard.SetText(transcript); } catch { }
                    UpdateState(AppState.Ready, "Transcript copied", FriendlyTypingError(typingException));
                }
            }
            else if (_settings.AutoType)
            {
                try { Clipboard.SetText(transcript); } catch { }
                UpdateState(AppState.Ready, "Transcript copied", "Focus another app, then use the dictation shortcut");
            }
            else
            {
                UpdateState(AppState.Ready, "Transcript ready", "Copy it or start another dictation");
            }
        }
        catch (Exception exception)
        {
            UpdateState(AppState.Error, "Transcription failed", FriendlyError(exception));
        }
    }

    private async Task ToggleStandbyAsync()
    {
        if (_state == AppState.Processing) return;
        if (_audioRecorder.IsRecording)
        {
            _silenceTimer.Stop();
            try { await _audioRecorder.StopAsync(); } catch { }
        }

        if (_state == AppState.Standby) UpdateState(AppState.Ready, "Ready to listen", $"{_settings.RecordHotkey.ToDisplayString()} to start");
        else UpdateState(AppState.Standby);
    }

    private void AudioRecorder_LevelChanged(object? sender, double level)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_state != AppState.Listening) return;
            if (level > 0.12) _lastVoiceAt = DateTime.UtcNow;
            var heights = new[] { .48, .78, .62, 1.0, .67, .84, .52 };
            var bars = Waveform.Children.OfType<Rectangle>().ToArray();
            for (var i = 0; i < bars.Length; i++)
                bars[i].Height = Math.Max(3, 4 + level * 16 * heights[i]);
            if (_dictationWidget.IsVisible) _dictationWidget.SetAudioLevel(level);
        });
    }

    private async void SilenceTimer_Tick(object? sender, EventArgs e)
    {
        if (!_settings.AutoStopOnSilence || _state != AppState.Listening || _silenceStopQueued) return;
        if (_recordingTime.Elapsed < TimeSpan.FromSeconds(1.6)) return;
        if (DateTime.UtcNow - _lastVoiceAt < TimeSpan.FromSeconds(1.2)) return;
        _silenceStopQueued = true;
        await StopAndTranscribeAsync();
    }

    private async void SaveKeyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CredentialManager.SaveApiKey(SelectedProvider, ApiKeyBox.Password.Trim());
            if (string.IsNullOrWhiteSpace(ApiKeyBox.Password))
            {
                KeySavedText.Text = "Not saved";
                KeySavedText.Foreground = BrushFrom("#FFB75E");
                UpdateState(AppState.Ready, $"{ProviderDisplayName} key removed", "Add a key to use transcription");
                return;
            }

            KeySavedText.Text = "Saved securely";
            KeySavedText.Foreground = BrushFrom("#52D0A0");
            UpdateState(AppState.Ready, $"{ProviderDisplayName} key saved", "Refreshing compatible audio models...");
            await RefreshModelsAsync(true);
        }
        catch (Exception exception)
        {
            UpdateState(AppState.Error, "Could not save API key", FriendlyError(exception));
        }
    }

    private async void RefreshModelsButton_Click(object sender, RoutedEventArgs e) => await RefreshModelsAsync(true);

    private async Task RefreshModelsAsync(bool showFeedback)
    {
        try
        {
            if (showFeedback)
                SetStatusMessage("Refreshing models...", $"Finding audio-to-text models for {ProviderDisplayName}");
            var models = SelectedProvider == ApiProvider.OpenAI
                ? await _openAI.GetAudioModelsAsync()
                : await _openRouter.GetAudioModelsAsync(ApiKeyBox.Password);
            var selected = GetStoredModelId(SelectedProvider);
            var list = models.ToList();
            if (list.Count == 0)
                throw new InvalidOperationException($"{ProviderDisplayName} returned no transcription models.");
            if (list.All(m => !m.Id.Equals(selected, StringComparison.OrdinalIgnoreCase)))
            {
                selected = GetDefaultModelId(SelectedProvider, list);
                SetStoredModelId(SelectedProvider, selected);
                SaveSettings();
            }
            ModelCombo.ItemsSource = list;
            ModelCombo.SelectedValue = selected;
            if (showFeedback)
                UpdateState(AppState.Ready, $"{models.Count} transcription models available",
                    $"Choose a {ProviderDisplayName} model from the list");
        }
        catch (Exception exception)
        {
            if (showFeedback) UpdateState(AppState.Error, "Could not refresh models", FriendlyError(exception));
        }
    }

    private string GetSelectedModelId()
    {
        var model = ModelCombo.SelectedValue as string;
        if (string.IsNullOrWhiteSpace(model)) model = ModelCombo.Text.Trim();
        if (string.IsNullOrWhiteSpace(model))
            model = SelectedProvider == ApiProvider.OpenAI
                ? "gpt-4o-mini-transcribe"
                : "openai/whisper-large-v3";
        SetStoredModelId(SelectedProvider, model);
        SaveSettings();
        return model;
    }

    private void ModelCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_initialized || ModelCombo.SelectedValue is not string model) return;
        SetStoredModelId(SelectedProvider, model);
        SaveSettings();
    }

    private void ModelCombo_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_initialized || string.IsNullOrWhiteSpace(ModelCombo.Text)) return;
        if (ModelCombo.SelectedValue is null) SetStoredModelId(SelectedProvider, ModelCombo.Text.Trim());
        SaveSettings();
    }

    private async void ProviderCombo_SelectionChanged(
        object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_initialized || ProviderCombo.SelectedValue is not ApiProvider provider) return;
        _settings.Provider = provider;
        LoadProviderUi();
        SaveSettings();

        if (provider == ApiProvider.OpenAI || !string.IsNullOrWhiteSpace(ApiKeyBox.Password))
            await RefreshModelsAsync(false);

        UpdateState(AppState.Ready, $"{ProviderDisplayName} selected",
            string.IsNullOrWhiteSpace(ApiKeyBox.Password)
                ? $"Add your {ProviderDisplayName} API key to begin"
                : "Ready for your next dictation");
    }

    private ApiProvider SelectedProvider =>
        ProviderCombo.SelectedValue is ApiProvider provider ? provider : _settings.Provider;

    private string ProviderDisplayName =>
        SelectedProvider == ApiProvider.OpenAI ? "OpenAI" : "OpenRouter";

    private void LoadProviderUi()
    {
        var provider = SelectedProvider;
        var modelId = GetStoredModelId(provider);
        ModelCombo.ItemsSource = new[] { new ModelOption(modelId, modelId) };
        ModelCombo.SelectedValue = modelId;

        var apiKey = _previewMode ? null : CredentialManager.ReadApiKey(provider);
        ApiKeyBox.Password = apiKey ?? string.Empty;
        KeySavedText.Text = string.IsNullOrWhiteSpace(apiKey) ? "Not saved" : "Saved securely";
        KeySavedText.Foreground = BrushFrom(string.IsNullOrWhiteSpace(apiKey) ? "#FFB75E" : "#52D0A0");

        ApiKeyLabel.Text = $"{ProviderDisplayName.ToUpperInvariant()} API KEY";
        ApiKeyHelpText.Text =
            $"Stored separately in Windows Credential Manager. Audio is sent to {ProviderDisplayName} only when you stop recording.";
    }

    private string GetStoredModelId(ApiProvider provider) =>
        provider == ApiProvider.OpenAI ? _settings.OpenAIModelId : _settings.ModelId;

    private void SetStoredModelId(ApiProvider provider, string modelId)
    {
        if (provider == ApiProvider.OpenAI) _settings.OpenAIModelId = modelId;
        else _settings.ModelId = modelId;
    }

    private static string GetDefaultModelId(ApiProvider provider, IReadOnlyList<ModelOption> models)
    {
        var preferred = provider == ApiProvider.OpenAI
            ? "gpt-4o-mini-transcribe"
            : "openai/whisper-large-v3";
        return models.FirstOrDefault(model =>
            model.Id.Equals(preferred, StringComparison.OrdinalIgnoreCase))?.Id ?? models[0].Id;
    }

    private void LanguageCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_initialized || LanguageCombo.SelectedItem is not LanguageOption language) return;
        _settings.LanguageCode = language.Code;
        SaveSettings();
    }

    private void BehaviorCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        _settings.AutoType = AutoTypeCheck.IsChecked == true;
        _settings.AutoCopy = AutoCopyCheck.IsChecked == true;
        SaveSettings();
    }

    private void OptionsCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        _settings.AlwaysOnTop = AlwaysOnTopCheck.IsChecked == true;
        _settings.AutoStopOnSilence = AutoStopCheck.IsChecked == true;
        Topmost = _settings.AlwaysOnTop;

        var startWithWindows = StartWithWindowsCheck.IsChecked == true;
        if (_settings.StartWithWindows != startWithWindows)
        {
            try
            {
                SettingsService.SetStartWithWindows(startWithWindows);
                _settings.StartWithWindows = startWithWindows;
            }
            catch (Exception exception)
            {
                StartWithWindowsCheck.IsChecked = _settings.StartWithWindows;
                UpdateState(AppState.Error, "Startup setting failed", FriendlyError(exception));
            }
        }
        SaveSettings();
    }

    private void RecordHotkeyButton_Click(object sender, RoutedEventArgs e) => BeginHotkeyCapture("record");
    private void StandbyHotkeyButton_Click(object sender, RoutedEventArgs e) => BeginHotkeyCapture("standby");

    private void BeginHotkeyCapture(string target)
    {
        _capturingHotkey = target;
        _hotkeys?.UnregisterAll();
        RecordHotkeyButton.Content = target == "record" ? "Press keys…" : "Change";
        StandbyHotkeyButton.Content = target == "standby" ? "Press keys…" : "Change";
        SetStatusMessage("Press your shortcut", "Use a modifier plus a key · Esc to cancel");
        Focus();
        Keyboard.Focus(this);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_capturingHotkey is null) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        e.Handled = true;

        if (key == Key.Escape)
        {
            EndHotkeyCapture();
            UpdateState(AppState.Ready);
            return;
        }

        if (key is Key.LeftAlt or Key.RightAlt or Key.LeftCtrl or Key.RightCtrl or
            Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;

        uint modifiers = 0;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= 0x0001;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= 0x0002;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= 0x0004;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= 0x0008;

        var isFunctionKey = key is >= Key.F1 and <= Key.F24;
        if (modifiers == 0 && !isFunctionKey)
        {
            SetStatusMessage("Add Ctrl, Alt, Shift, or Win", "Function keys can be used by themselves");
            return;
        }

        var hotkey = new HotkeySettings
        {
            Modifiers = modifiers,
            VirtualKey = (uint)KeyInterop.VirtualKeyFromKey(key)
        };
        var captureTarget = _capturingHotkey;
        var previous = captureTarget == "record" ? _settings.RecordHotkey : _settings.StandbyHotkey;
        if (captureTarget == "record") _settings.RecordHotkey = hotkey;
        else _settings.StandbyHotkey = hotkey;

        EndHotkeyCapture(false);
        if (!RegisterAllHotkeys())
        {
            if (captureTarget == "record") _settings.RecordHotkey = previous;
            else _settings.StandbyHotkey = previous;
            RegisterAllHotkeys();
            EndHotkeyCapture();
            UpdateState(AppState.Error, "Shortcut is already in use", "Choose a different key combination");
            return;
        }

        RecordHotkeyText.Text = _settings.RecordHotkey.ToDisplayString();
        StandbyHotkeyText.Text = _settings.StandbyHotkey.ToDisplayString();
        SaveSettings();
        EndHotkeyCapture();
        UpdateState(AppState.Ready, "Shortcut updated", hotkey.ToDisplayString());
    }

    private void EndHotkeyCapture(bool register = true)
    {
        _capturingHotkey = null;
        RecordHotkeyButton.Content = "Change";
        StandbyHotkeyButton.Content = "Change";
        if (register) RegisterAllHotkeys();
    }

    private bool RegisterAllHotkeys()
    {
        if (_hotkeys is null) return true;
        _hotkeys.UnregisterAll();
        var record = _hotkeys.Register(HotkeyService.RecordId, _settings.RecordHotkey);
        var standby = _hotkeys.Register(HotkeyService.StandbyId, _settings.StandbyHotkey);
        if (!record || !standby)
        {
            SetStatusMessage("One shortcut is unavailable", "Open Settings & shortcuts to change it");
            return false;
        }
        return true;
    }

    private async void StandbyButton_Click(object sender, RoutedEventArgs e) => await ToggleStandbyAsync();

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TranscriptBox.Text) || TranscriptBox.Text.StartsWith("Your transcription")) return;
        try
        {
            Clipboard.SetText(TranscriptBox.Text);
            SetStatusMessage("Copied", "Transcript is on the clipboard");
        }
        catch (Exception exception)
        {
            UpdateState(AppState.Error, "Could not copy", FriendlyError(exception));
        }
    }

    private void UpdateState(AppState state, string? title = null, string? subtitle = null)
    {
        _state = state;
        var (label, color, defaultTitle, defaultSubtitle) = state switch
        {
            AppState.Ready => ("READY", "#52D0A0", "Ready to listen", $"{_settings.RecordHotkey.ToDisplayString()} to start"),
            AppState.Listening => ("LISTENING", "#FF6680", "Listening…", "Speak naturally · press again to stop"),
            AppState.Processing => ("WORKING", "#FFC857", "Transcribing…",
                $"Sending audio securely to {ProviderDisplayName}"),
            AppState.Standby => ("STANDBY", "#8A91A3", "Standing by", $"{_settings.StandbyHotkey.ToDisplayString()} to resume"),
            _ => ("ERROR", "#FF6B6B", "Something went wrong", "Check the details and try again")
        };

        HeaderStateText.Text = label;
        HeaderStateText.Foreground = BrushFrom(color);
        StatusTitle.Text = title ?? defaultTitle;
        StatusSubtitle.Text = subtitle ?? defaultSubtitle;
        MicButton.IsEnabled = state != AppState.Processing;
        _trayIcon.Text = $"VoxPilot — {label.ToLowerInvariant()}";
        UpdateMicAppearance();
        UpdateDictationWidget(state, title ?? defaultTitle, subtitle ?? defaultSubtitle);
    }

    private void UpdateDictationWidget(AppState state, string title, string subtitle)
    {
        var mainWindowUnavailable = WindowState == WindowState.Minimized || !IsVisible;
        switch (state)
        {
            case AppState.Listening when mainWindowUnavailable:
                _dictationWidget.ShowListening(_dictationTargetWindow, _settings.RecordHotkey.ToDisplayString());
                break;
            case AppState.Processing when mainWindowUnavailable || _dictationWidget.IsVisible:
                _dictationWidget.ShowProcessing(_dictationTargetWindow);
                break;
            case AppState.Ready when _dictationWidget.IsVisible:
                _dictationWidget.ShowResult(title, subtitle, true, _dictationTargetWindow);
                break;
            case AppState.Error when mainWindowUnavailable || _dictationWidget.IsVisible:
                _dictationWidget.ShowResult(title, subtitle, false, _dictationTargetWindow);
                break;
            case AppState.Standby when mainWindowUnavailable || _dictationWidget.IsVisible:
                _dictationWidget.ShowResult("VoxPilot is in standby", subtitle, false, _dictationTargetWindow);
                break;
            default:
                if (!mainWindowUnavailable) _dictationWidget.Dismiss();
                break;
        }
    }

    private void SetStatusMessage(string title, string subtitle)
    {
        StatusTitle.Text = title;
        StatusSubtitle.Text = subtitle;
    }

    private void UpdateMicAppearance()
    {
        MicButton.ApplyTemplate();
        if (MicButton.Template.FindName("Inner", MicButton) is not Ellipse inner ||
            MicButton.Template.FindName("OuterGlow", MicButton) is not Ellipse outer ||
            MicButton.Template.FindName("MicGlyph", MicButton) is not System.Windows.Controls.TextBlock glyph) return;

        var (innerColor, outerColor, symbol) = _state switch
        {
            AppState.Listening => ("#FF5F78", "#542B3A", "■"),
            AppState.Processing => ("#FFC857", "#4E4328", "…"),
            AppState.Standby => ("#5A6070", "#292D37", "×"),
            AppState.Error => ("#E95D67", "#4B2A32", "!"),
            _ => ("#7C5CFC", "#2E2658", "\uE720")
        };
        inner.Fill = BrushFrom(innerColor);
        outer.Fill = BrushFrom(outerColor);
        glyph.Text = symbol;
    }

    private void ResetWaveform()
    {
        var heights = new double[] { 4, 7, 5, 10, 6, 8, 4 };
        var bars = Waveform.Children.OfType<Rectangle>().ToArray();
        for (var i = 0; i < bars.Length; i++) bars[i].Height = heights[i];
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.ClickCount == 2)
        {
            ToggleMaximizeRestore();
            e.Handled = true;
            return;
        }

        if (WindowState == WindowState.Maximized)
        {
            var mousePosition = e.GetPosition(this);
            var screenPosition = PointToScreen(mousePosition);
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget is not null)
                screenPosition = source.CompositionTarget.TransformFromDevice.Transform(screenPosition);
            var horizontalRatio = ActualWidth > 0 ? mousePosition.X / ActualWidth : 0.5;
            WindowState = WindowState.Normal;
            Left = screenPosition.X - RestoreBounds.Width * horizontalRatio;
            Top = screenPosition.Y - 20;
        }

        try
        {
            DragMove();
            e.Handled = true;
        }
        catch (InvalidOperationException)
        {
            // The mouse button can be released between the event and the native drag loop.
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        HideToTray();
    }

    private void ExpandWindow()
    {
        ExpandedContent.Visibility = Visibility.Visible;
        Height = 590;
        EnsureWindowOnScreen();
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) => ToggleMaximizeRestore();

    private void ToggleMaximizeRestore()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            HideToTray();
            return;
        }

        if (WindowState == WindowState.Maximized)
        {
            WindowShell.Margin = new Thickness(0);
            WindowShell.CornerRadius = new CornerRadius(0);
            MaximizeIcon.Data = Geometry.Parse("M 4,2 L 12,2 L 12,10 M 2,4 L 10,4 L 10,12 L 2,12 Z");
            MaximizeButton.ToolTip = "Restore down";
        }
        else
        {
            WindowShell.Margin = new Thickness(12);
            WindowShell.CornerRadius = new CornerRadius(20);
            MaximizeIcon.Data = Geometry.Parse("M 2,2 L 12,2 L 12,12 L 2,12 Z");
            MaximizeButton.ToolTip = "Maximize";
            if (WindowState == WindowState.Normal) _dictationWidget.Dismiss();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => ExitApplication();

    private void ShowFromTray()
    {
        _dictationWidget.Dismiss();
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Focus();
    }

    private void HideToTray(bool showHint = true)
    {
        SaveSettings();
        ShowInTaskbar = false;
        Hide();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;

        if (!showHint || _previewMode || _hasShownTrayHint) return;
        _hasShownTrayHint = true;
        _trayIcon.BalloonTipTitle = "VoxPilot is still running";
        _trayIcon.BalloonTipText = "Use the notification-area icon or your dictation shortcut to keep working.";
        _trayIcon.BalloonTipIcon = Forms.ToolTipIcon.Info;
        _trayIcon.ShowBalloonTip(2500);
    }

    private async void Window_Deactivated(object? sender, EventArgs e)
    {
        await Task.Delay(75);
        var foreground = TextInjector.GetForegroundWindowHandle();
        if (TextInjector.IsValidTargetWindow(foreground, _windowHandle)) _lastExternalWindow = foreground;
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (_previewMode || !_initialized || WindowState != WindowState.Normal) return;
        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
    }

    private void EnsureWindowOnScreen()
    {
        var area = SystemParameters.WorkArea;
        Left = Math.Clamp(Left, area.Left - Width + 80, area.Right - 80);
        Top = Math.Clamp(Top, area.Top, area.Bottom - 80);
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        SaveSettings();
        _silenceTimer.Stop();
        _hotkeys?.Dispose();
        _audioRecorder.Dispose();
        _openAI.Dispose();
        _openRouter.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.ContextMenuStrip?.Dispose();
        _trayIcon.Dispose();
        _appIcon.Dispose();
        _dictationWidget.CloseWidget();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    public void CloseForPreview()
    {
        _isExiting = true;
        Close();
    }

    private void SaveSettings()
    {
        try { _settingsService.Save(_settings); } catch { }
    }

    private static SolidColorBrush BrushFrom(string color) =>
        new((System.Windows.Media.Color)ColorConverter.ConvertFromString(color));

    private string FriendlyError(Exception exception)
    {
        var message = exception.Message.Trim();
        if (message.Contains("401", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
            return $"The API key was rejected by {ProviderDisplayName}.";
        if (message.Contains("402", StringComparison.OrdinalIgnoreCase))
            return SelectedProvider == ApiProvider.OpenRouter
                ? "OpenRouter credits are required for this model."
                : "OpenAI billing or credits are required for this model.";
        if (message.Contains("429", StringComparison.OrdinalIgnoreCase))
            return $"{ProviderDisplayName} is rate-limiting requests. Try again shortly.";
        if (message.Contains("404", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("transcription model", StringComparison.OrdinalIgnoreCase))
            return "This model cannot transcribe audio. Refresh and choose a transcription model.";
        if (message.Contains("microphone", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("device", StringComparison.OrdinalIgnoreCase))
            return "Check Windows microphone access and your default input device.";
        return message.Length > 105 ? message[..102] + "…" : message;
    }

    private static string FriendlyTypingError(Exception exception)
    {
        if (exception is Win32Exception { NativeErrorCode: 5 })
            return "The target may be running as administrator. Paste with Ctrl+V instead.";
        if (exception.Message.Contains("focusing", StringComparison.OrdinalIgnoreCase))
            return "Windows blocked focus switching. Click the target and paste with Ctrl+V.";
        return "Typing was blocked, so the transcript was copied. Paste with Ctrl+V.";
    }
}
