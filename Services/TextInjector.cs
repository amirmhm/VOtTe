using System.ComponentModel;
using System.Runtime.InteropServices;

namespace VoxPilot.Services;

public static class TextInjector
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventUnicode = 0x0004;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint GetWindowNext = 2;
    private const uint GetWindowOwner = 4;

    public static IntPtr GetForegroundWindowHandle() => GetForegroundWindow();

    public static bool IsValidTargetWindow(IntPtr window, IntPtr excludedWindow)
    {
        if (window == IntPtr.Zero || window == excludedWindow || !IsWindow(window) || !IsWindowVisible(window))
            return false;
        GetWindowThreadProcessId(window, out var processId);
        return processId != (uint)Environment.ProcessId;
    }

    public static IntPtr FindNextApplicationWindow(IntPtr excludedWindow)
    {
        var candidate = GetWindow(excludedWindow, GetWindowNext);
        while (candidate != IntPtr.Zero)
        {
            GetWindowThreadProcessId(candidate, out var processId);
            var isApplicationWindow = processId != (uint)Environment.ProcessId &&
                                      IsWindowVisible(candidate) &&
                                      GetWindow(candidate, GetWindowOwner) == IntPtr.Zero &&
                                      GetWindowTextLength(candidate) > 0;
            if (isApplicationWindow) return candidate;
            candidate = GetWindow(candidate, GetWindowNext);
        }
        return IntPtr.Zero;
    }

    public static async Task TypeAsync(string text, IntPtr targetWindow)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (targetWindow == IntPtr.Zero || !IsWindow(targetWindow))
            throw new InvalidOperationException("The target application is no longer open.");

        if (IsIconic(targetWindow)) ShowWindow(targetWindow, 9);
        TryActivateWindow(targetWindow);
        await Task.Delay(140);
        if (GetForegroundWindow() != targetWindow)
        {
            TryActivateWindow(targetWindow);
            await Task.Delay(100);
        }
        if (GetForegroundWindow() != targetWindow)
            throw new InvalidOperationException("Windows prevented VoxPilot from focusing the target application.");

        const int chunkSize = 80;
        foreach (var chunk in text.Chunk(chunkSize))
        {
            var inputs = new List<Input>(chunk.Length * 2);
            foreach (var character in chunk)
            {
                inputs.Add(CreateUnicodeInput(character, false));
                inputs.Add(CreateUnicodeInput(character, true));
            }
            var array = inputs.ToArray();
            var sent = SendInput((uint)array.Length, array, Marshal.SizeOf<Input>());
            if (sent != array.Length)
            {
                var error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error,
                    $"Windows accepted {sent} of {array.Length} keyboard inputs.");
            }
            await Task.Delay(5);
        }
    }

    private static bool TryActivateWindow(IntPtr targetWindow)
    {
        if (GetForegroundWindow() == targetWindow) return true;
        SetForegroundWindow(targetWindow);
        if (GetForegroundWindow() == targetWindow) return true;

        var foregroundWindow = GetForegroundWindow();
        var currentThread = GetCurrentThreadId();
        var foregroundThread = foregroundWindow == IntPtr.Zero
            ? 0
            : GetWindowThreadProcessId(foregroundWindow, out _);
        var targetThread = GetWindowThreadProcessId(targetWindow, out _);
        var attachedForeground = foregroundThread != 0 && foregroundThread != currentThread &&
                                 AttachThreadInput(currentThread, foregroundThread, true);
        var attachedTarget = targetThread != 0 && targetThread != currentThread && targetThread != foregroundThread &&
                             AttachThreadInput(currentThread, targetThread, true);
        try
        {
            BringWindowToTop(targetWindow);
            SetForegroundWindow(targetWindow);
            return GetForegroundWindow() == targetWindow;
        }
        finally
        {
            if (attachedTarget) AttachThreadInput(currentThread, targetThread, false);
            if (attachedForeground) AttachThreadInput(currentThread, foregroundThread, false);
        }
    }

    private static Input CreateUnicodeInput(char character, bool keyUp) => new()
    {
        Type = InputKeyboard,
        Union = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                Scan = character,
                Flags = KeyEventUnicode | (keyUp ? KeyEventKeyUp : 0)
            }
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct Input { public uint Type; public InputUnion Union; }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public HardwareInput Hardware;
    }

    // INPUT's union must include MOUSEINPUT. On 64-bit Windows this makes INPUT 40 bytes;
    // a keyboard-only 32-byte declaration is rejected by SendInput with ERROR_INVALID_PARAMETER.
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Message;
        public ushort ParameterLow;
        public ushort ParameterHigh;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr window, uint command);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr window);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint attachThread, uint attachToThread, bool attach);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);
}
