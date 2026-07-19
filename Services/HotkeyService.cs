using System.Runtime.InteropServices;
using System.Windows.Interop;
using VoxPilot.Models;

namespace VoxPilot.Services;

public sealed class HotkeyService : IDisposable
{
    public const int RecordId = 0x5101;
    public const int StandbyId = 0x5102;
    private const int WmHotkey = 0x0312;
    private const uint NoRepeat = 0x4000;

    private readonly IntPtr _windowHandle;
    private readonly HwndSource _source;

    public event EventHandler<int>? Pressed;

    public HotkeyService(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
        _source = HwndSource.FromHwnd(windowHandle) ?? throw new InvalidOperationException("Window source is unavailable.");
        _source.AddHook(WindowHook);
    }

    public bool Register(int id, HotkeySettings hotkey)
    {
        UnregisterHotKey(_windowHandle, id);
        return RegisterHotKey(_windowHandle, id, hotkey.Modifiers | NoRepeat, hotkey.VirtualKey);
    }

    public void UnregisterAll()
    {
        UnregisterHotKey(_windowHandle, RecordId);
        UnregisterHotKey(_windowHandle, StandbyId);
    }

    private IntPtr WindowHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey)
        {
            handled = true;
            Pressed?.Invoke(this, wParam.ToInt32());
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.RemoveHook(WindowHook);
    }

    public static bool IsKeyDown(uint virtualKey) =>
        (GetAsyncKeyState((int)virtualKey) & 0x8000) != 0;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
