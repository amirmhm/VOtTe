namespace VoxPilot.Models;

public enum ApiProvider
{
    OpenAI,
    OpenRouter
}

public enum InteractionMode
{
    Toggle,
    PushToTalk
}

public enum TextStyleMode
{
    Exact,
    Polished,
    Notes
}

public sealed class AppSettings
{
    public ApiProvider Provider { get; set; } = ApiProvider.OpenRouter;
    public string ModelId { get; set; } = "openai/whisper-large-v3";
    public string OpenAIModelId { get; set; } = "gpt-4o-mini-transcribe";
    public string SmartTextModelId { get; set; } = "gpt-5.6-terra";
    public string LanguageCode { get; set; } = "auto";
    public int AudioDeviceNumber { get; set; } = -1;
    public InteractionMode InteractionMode { get; set; } = InteractionMode.Toggle;
    public TextStyleMode TextStyleMode { get; set; } = TextStyleMode.Exact;
    public bool HasCompletedOnboarding { get; set; }
    public bool AutoType { get; set; } = true;
    public bool AutoCopy { get; set; }
    public bool AlwaysOnTop { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool AutoStopOnSilence { get; set; }
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public HotkeySettings RecordHotkey { get; set; } = new() { Modifiers = 0x0006, VirtualKey = 0x78 };
    public HotkeySettings StandbyHotkey { get; set; } = new() { Modifiers = 0x0006, VirtualKey = 0x79 };
}

public sealed class HotkeySettings
{
    public uint Modifiers { get; set; }
    public uint VirtualKey { get; set; }

    public string ToDisplayString()
    {
        var pieces = new List<string>();
        if ((Modifiers & 0x0002) != 0) pieces.Add("Ctrl");
        if ((Modifiers & 0x0001) != 0) pieces.Add("Alt");
        if ((Modifiers & 0x0004) != 0) pieces.Add("Shift");
        if ((Modifiers & 0x0008) != 0) pieces.Add("Win");
        pieces.Add(KeyName(VirtualKey));
        return string.Join(" + ", pieces);
    }

    private static string KeyName(uint key) => key switch
    {
        0x20 => "Space",
        0x0D => "Enter",
        0x09 => "Tab",
        0x1B => "Esc",
        >= 0x70 and <= 0x87 => $"F{key - 0x6F}",
        _ => ((char)key).ToString().ToUpperInvariant()
    };
}

public sealed record LanguageOption(string Name, string Code)
{
    public override string ToString() => Name;
}

public sealed record ModelOption(string Name, string Id)
{
    public override string ToString() => Name;
}

public sealed record ProviderOption(string Name, ApiProvider Id)
{
    public override string ToString() => Name;
}

public sealed record AudioDeviceOption(string Name, int Id)
{
    public override string ToString() => Name;
}

public sealed record InteractionOption(string Name, string Description, InteractionMode Id)
{
    public override string ToString() => Name;
}

public sealed record TextStyleOption(string Name, string Description, TextStyleMode Id)
{
    public override string ToString() => Name;
}
