using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.IO;

namespace VoxPilot;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, "VoxPilot.SingleInstance.6BC34DB7", out var createdNew);
        _ownsMutex = createdNew;
        if (!createdNew)
        {
            System.Windows.MessageBox.Show("VoxPilot is already running. Check the notification area.", "VoxPilot",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var widgetPreviewArgument = e.Args.FirstOrDefault(argument =>
            argument.StartsWith("--render-widget-preview=", StringComparison.OrdinalIgnoreCase));
        if (widgetPreviewArgument is not null)
        {
            var previewPath = widgetPreviewArgument["--render-widget-preview=".Length..].Trim('"');
            var widget = new DictationWidget { Opacity = 0 };
            widget.ShowListening(IntPtr.Zero, "Ctrl + Shift + F9");
            widget.Left = -20000;
            widget.Top = -20000;
            widget.Opacity = 1;
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                SaveVisualPreview(widget, previewPath);
                widget.CloseWidget();
                Shutdown();
            }));
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        var previewArgument = e.Args.FirstOrDefault(argument =>
            argument.StartsWith("--render-preview=", StringComparison.OrdinalIgnoreCase));
        if (previewArgument is not null)
        {
            var previewPath = previewArgument["--render-preview=".Length..].Trim('"');
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -20000;
            window.Top = -20000;
            window.Topmost = false;
            window.ShowInTaskbar = false;
            window.Show();
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                SaveVisualPreview(window, previewPath);
                window.CloseForPreview();
                Shutdown();
            }));
            return;
        }
        window.Show();
    }

    private static void SaveVisualPreview(Window window, string previewPath)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(window.ActualWidth)),
            Math.Max(1, (int)Math.Ceiling(window.ActualHeight)),
            96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(previewPath))!);
        using var stream = File.Create(previewPath);
        encoder.Save(stream);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex) _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
