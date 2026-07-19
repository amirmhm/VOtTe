using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace VoxPilot;

public partial class DictationWidget : Window
{
    private const int ExtendedStyleIndex = -20;
    private const long NoActivateStyle = 0x08000000L;
    private const long ToolWindowStyle = 0x00000080L;
    private const long TransparentStyle = 0x00000020L;

    private readonly DispatcherTimer _animationTimer;
    private CancellationTokenSource? _hideCancellation;
    private double _audioLevel;
    private double _phase;
    private WidgetMode _mode;

    public DictationWidget()
    {
        InitializeComponent();
        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(55) };
        _animationTimer.Tick += AnimationTimer_Tick;
    }

    public void ShowListening(IntPtr targetWindow, string shortcut)
    {
        CancelPendingHide();
        _mode = WidgetMode.Listening;
        WidgetTitle.Text = "Listening…";
        WidgetHint.Text = $"{shortcut} to stop";
        WidgetGlyph.Text = "\uE720";
        WidgetIcon.Background = BrushFrom("#7C5CFC");
        SetWaveColor("#A28FFF");
        ShowWithoutActivation(targetWindow);
        _animationTimer.Start();
    }

    public void ShowProcessing(IntPtr targetWindow)
    {
        CancelPendingHide();
        _mode = WidgetMode.Processing;
        WidgetTitle.Text = "Transcribing…";
        WidgetHint.Text = "Your transcription provider is processing your voice";
        WidgetGlyph.Text = "\uE895";
        WidgetIcon.Background = BrushFrom("#D99A31");
        SetWaveColor("#FFC857");
        ShowWithoutActivation(targetWindow);
        _animationTimer.Start();
    }

    public void ShowResult(string title, string hint, bool success, IntPtr targetWindow)
    {
        _mode = WidgetMode.Result;
        WidgetTitle.Text = title;
        WidgetHint.Text = hint;
        WidgetGlyph.Text = success ? "\uE73E" : "!";
        WidgetIcon.Background = BrushFrom(success ? "#35B987" : "#D65A67");
        SetWaveColor(success ? "#52D0A0" : "#FF7B88");
        ShowWithoutActivation(targetWindow);
        _animationTimer.Stop();
        _ = HideAfterAsync(success ? 1300 : 2300);
    }

    public void SetAudioLevel(double level) => _audioLevel = Math.Clamp(level, 0, 1);

    public void Dismiss()
    {
        CancelPendingHide();
        _animationTimer.Stop();
        Hide();
    }

    public void CloseWidget()
    {
        CancelPendingHide();
        _animationTimer.Stop();
        if (IsLoaded) Close();
    }

    private void ShowWithoutActivation(IntPtr targetWindow)
    {
        if (!IsVisible) Show();
        PositionOnScreen(targetWindow);
    }

    private void PositionOnScreen(IntPtr targetWindow)
    {
        var handle = targetWindow != IntPtr.Zero ? targetWindow : new WindowInteropHelper(this).Handle;
        var screen = Forms.Screen.FromHandle(handle);
        var dpi = VisualTreeHelper.GetDpi(this);
        var left = screen.WorkingArea.Left / dpi.DpiScaleX +
                   (screen.WorkingArea.Width / dpi.DpiScaleX - ActualWidth) / 2;
        var top = screen.WorkingArea.Bottom / dpi.DpiScaleY - ActualHeight - 18;
        Left = left;
        Top = top;
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        _phase += _mode == WidgetMode.Processing ? 0.42 : 0.28;
        var bars = WidgetWave.Children.OfType<Rectangle>().ToArray();
        var energy = _mode == WidgetMode.Processing ? 0.62 : 0.22 + _audioLevel * 0.78;
        for (var index = 0; index < bars.Length; index++)
        {
            var wave = (Math.Sin(_phase + index * 0.82) + 1) / 2;
            var centerBoost = 1 - Math.Abs(index - (bars.Length - 1) / 2d) / bars.Length;
            bars[index].Height = 4 + wave * 22 * energy * centerBoost;
            bars[index].Opacity = 0.55 + wave * 0.45;
        }
        _audioLevel *= 0.91;
    }

    private async Task HideAfterAsync(int milliseconds)
    {
        CancelPendingHide();
        _hideCancellation = new CancellationTokenSource();
        var token = _hideCancellation.Token;
        try
        {
            await Task.Delay(milliseconds, token);
            if (!token.IsCancellationRequested) Hide();
        }
        catch (OperationCanceledException) { }
    }

    private void CancelPendingHide()
    {
        _hideCancellation?.Cancel();
        _hideCancellation?.Dispose();
        _hideCancellation = null;
    }

    private void SetWaveColor(string color)
    {
        var brush = BrushFrom(color);
        foreach (var rectangle in WidgetWave.Children.OfType<Rectangle>()) rectangle.Fill = brush;
    }

    private static SolidColorBrush BrushFrom(string color) =>
        new((Color)ColorConverter.ConvertFromString(color));

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLongPtr(handle, ExtendedStyleIndex).ToInt64();
        SetWindowLongPtr(handle, ExtendedStyleIndex,
            new IntPtr(style | NoActivateStyle | ToolWindowStyle | TransparentStyle));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr newValue);

    private enum WidgetMode
    {
        Listening,
        Processing,
        Result
    }
}
