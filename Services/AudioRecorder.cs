using NAudio.Wave;
using System.IO;

namespace VoxPilot.Services;

public sealed class AudioRecorder : IDisposable
{
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private MemoryStream? _stream;
    private TaskCompletionSource<byte[]>? _stopSource;

    public event EventHandler<double>? LevelChanged;
    public bool IsRecording => _waveIn is not null;

    public void Start()
    {
        if (IsRecording) return;

        _stream = new MemoryStream();
        _writer = new WaveFileWriter(_stream, new WaveFormat(16000, 16, 1));
        _stopSource = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        _waveIn = new WaveInEvent
        {
            DeviceNumber = -1,
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 50,
            NumberOfBuffers = 3
        };
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;
        _waveIn.StartRecording();
    }

    public async Task<byte[]> StopAsync()
    {
        if (_waveIn is null || _stopSource is null) return [];
        var completion = _stopSource.Task;
        _waveIn.StopRecording();
        return await completion.WaitAsync(TimeSpan.FromSeconds(4));
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        _writer?.Write(e.Buffer, 0, e.BytesRecorded);
        _writer?.Flush();

        double peak = 0;
        for (var i = 0; i + 1 < e.BytesRecorded; i += 2)
        {
            var sample = BitConverter.ToInt16(e.Buffer, i) / 32768d;
            peak = Math.Max(peak, Math.Abs(sample));
        }
        LevelChanged?.Invoke(this, Math.Min(1, peak * 3.2));
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        try
        {
            _writer?.Dispose();
            var data = _stream?.ToArray() ?? [];
            if (e.Exception is not null) _stopSource?.TrySetException(e.Exception);
            else _stopSource?.TrySetResult(data);
        }
        finally
        {
            if (_waveIn is not null)
            {
                _waveIn.DataAvailable -= OnDataAvailable;
                _waveIn.RecordingStopped -= OnRecordingStopped;
                _waveIn.Dispose();
            }
            _waveIn = null;
            _writer = null;
            _stream?.Dispose();
            _stream = null;
        }
    }

    public void Dispose()
    {
        if (_waveIn is not null)
        {
            try { _waveIn.StopRecording(); } catch { }
        }
        _waveIn?.Dispose();
        _writer?.Dispose();
        _stream?.Dispose();
    }
}
