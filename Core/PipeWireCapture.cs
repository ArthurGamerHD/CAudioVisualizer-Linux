using System.Diagnostics;
using NAudio.Wave;

namespace CAudioVisualizer.Core;

/// <summary>
/// Captures audio on Linux by streaming raw float samples from `parec`
/// </summary>
public class PipeWireCapture : IWaveIn
{
    public const string DefaultMonitor = "@DEFAULT_MONITOR@";

    private const int SampleRate = 48000;
    private const int Channels = 2;
    private const int ChunkBytes = SampleRate / 100 * Channels * sizeof(float);

    private readonly string _device;
    private Process? _process;
    private Thread? _readThread;
    private volatile bool _recording;

    public WaveFormat WaveFormat { get; set; } = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, Channels);

    public event EventHandler<WaveInEventArgs>? DataAvailable;
    public event EventHandler<StoppedEventArgs>? RecordingStopped;

    public PipeWireCapture(string? device = null)
    {
        _device = string.IsNullOrEmpty(device) ? DefaultMonitor : device;
    }

    public void StartRecording()
    {
        if (_recording) return;

        var psi = new ProcessStartInfo("parec")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add($"--device={_device}");
        psi.ArgumentList.Add("--format=float32le");
        psi.ArgumentList.Add($"--rate={SampleRate}");
        psi.ArgumentList.Add($"--channels={Channels}");
        psi.ArgumentList.Add("--latency-msec=10");
        psi.ArgumentList.Add("--raw");

        _process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start parec");
        _process.ErrorDataReceived += (_, e) => { if (e.Data != null) Console.WriteLine($"parec: {e.Data}"); };
        _process.BeginErrorReadLine();

        _recording = true;
        _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "PipeWireCapture" };
        _readThread.Start();
    }

    private void ReadLoop()
    {
        Exception? error = null;
        var stream = _process!.StandardOutput.BaseStream;

        try
        {
            while (_recording)
            {
                var buffer = new byte[ChunkBytes];
                stream.ReadExactly(buffer);
                DataAvailable?.Invoke(this, new WaveInEventArgs(buffer, buffer.Length));
            }
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException or ObjectDisposedException)
        {
            if (_recording) error = ex;
        }

        _recording = false;
        RecordingStopped?.Invoke(this, new StoppedEventArgs(error));
    }

    public void StopRecording()
    {
        _recording = false;

        try
        {
            if (_process is { HasExited: false })
                _process.Kill();
        }
        catch (InvalidOperationException)
        {
            // Process already exited
        }

        if (_readThread != null && _readThread != Thread.CurrentThread)
            _readThread.Join(1000);
        _readThread = null;
    }

    public void Dispose()
    {
        StopRecording();
        _process?.Dispose();
        _process = null;
    }

    public static List<(string Name, string Description)> ListSources()
    {
        var sources = new List<(string, string)>();

        var psi = new ProcessStartInfo("pactl")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("list");
        psi.ArgumentList.Add("sources");
        psi.Environment["LC_ALL"] = "C";

        using var process = Process.Start(psi);
        if (process == null) return sources;

        string? name = null;
        foreach (var rawLine in process.StandardOutput.ReadToEnd().Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("Name: "))
            {
                name = line["Name: ".Length..];
            }
            else if (line.StartsWith("Description: ") && name != null)
            {
                sources.Add((name, line["Description: ".Length..]));
                name = null;
            }
        }

        process.WaitForExit();
        return sources;
    }
}
