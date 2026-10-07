using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using NAudio.Wave;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;

using CAudioVisualizer.Core;
using CAudioVisualizer.GUI;
using CAudioVisualizer.Configuration;
using CAudioVisualizer.Visualizers;

namespace CAudioVisualizer;

public class AudioVisualizerWindow : GameWindow
{
    private IWaveIn? _capture;
    private readonly List<float> _audioBuffer = new();
    private readonly object _bufferLock = new();
    private const int BUFFER_SIZE = 2048;

    private float[] _waveformData = new float[BUFFER_SIZE];
    private Complex32[] _fftBuffer = new Complex32[BUFFER_SIZE];
    private float[] _fftData = new float[BUFFER_SIZE / 2]; // Only need first half of FFT
    private static readonly float[] HannWindow = CreateHannWindow(BUFFER_SIZE);

    private VisualizerManager _visualizerManager = null!;

    private ImGuiController _imGuiController = null!;
    private ConfigurationGui _configGui = null!;
    private AppConfig _appConfig = null!;
    private bool _showConfigWindow = false;

    private WindowState _lastWindowState = WindowState.Normal;

    public AudioVisualizerWindow(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
        : base(gameWindowSettings, nativeWindowSettings)
    {
    }

    public void SwitchToMonitor(int monitorIndex)
    {
        var monitors = Monitors.GetMonitors();

        if (_appConfig?.SpanAllMonitors == true && monitors.Count > 1)
        {
            int minX = monitors.Min(m => m.ClientArea.Min.X);
            int minY = monitors.Min(m => m.ClientArea.Min.Y);
            int maxX = monitors.Max(m => m.ClientArea.Max.X);
            int maxY = monitors.Max(m => m.ClientArea.Max.Y);

            Location = new Vector2i(minX, minY);
            ClientSize = new Vector2i(maxX - minX, maxY - minY);

            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
            _imGuiController?.WindowResized(ClientSize.X, ClientSize.Y);
        }
        else if (monitorIndex >= 0 && monitorIndex < monitors.Count)
        {
            var selectedMonitor = monitors[monitorIndex];
            Location = new Vector2i(selectedMonitor.WorkArea.Min.X, selectedMonitor.WorkArea.Min.Y);
            ClientSize = new Vector2i(selectedMonitor.HorizontalResolution, selectedMonitor.VerticalResolution);

            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
            _imGuiController?.WindowResized(ClientSize.X, ClientSize.Y);
        }
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        _imGuiController = new ImGuiController(ClientSize.X, ClientSize.Y);

        _appConfig = new AppConfig();
        _appConfig.LoadConfiguration(AppConfig.GetConfigFilePath());

        // Initialize audio device name if not set
        if (string.IsNullOrEmpty(_appConfig.SelectedAudioDeviceName))
        {
            _appConfig.SelectedAudioDeviceName = AudioDeviceManager.GetDeviceName(_appConfig.SelectedAudioDeviceId);
        }

        _visualizerManager = new VisualizerManager(ClientSize);
        _visualizerManager.LoadVisualizerConfigurations(_appConfig.VisualizerConfigs, _appConfig.EnabledVisualizers);

        _configGui = new ConfigurationGui(_visualizerManager, _appConfig, ApplyConfigurationSettings, this);

        SetupAudioCapture();

        ApplyConfigurationSettings();
    }

    private void ApplyConfigurationSettings()
    {
        // Set FPS: 0 means unlimited in OpenTK
        UpdateFrequency = _appConfig.UnlimitedFPS ? 0 : _appConfig.TargetFPS;
        VSync = _appConfig.EnableVSync ? VSyncMode.On : VSyncMode.Off;
    }

    private void SetupAudioCapture()
    {
        try
        {
            _capture = AudioDeviceManager.CreateAudioCapture(_appConfig.SelectedAudioDeviceId);

            string audioDeviceInfo =
                $"Audio Device: {_appConfig.SelectedAudioDeviceName}\n" +
                $"Sample Rate: {_capture.WaveFormat.SampleRate} Hz\n" +
                $"Channels: {_capture.WaveFormat.Channels}\n" +
                $"Bits Per Sample: {_capture.WaveFormat.BitsPerSample}\n" +
                $"Bytes Per Second: {_capture.WaveFormat.AverageBytesPerSecond}";

            Console.WriteLine(audioDeviceInfo.Replace("\n", Environment.NewLine));

            _visualizerManager.SetAudioDeviceInfo(audioDeviceInfo);

            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += (s, e) => Console.WriteLine("Recording stopped");
            _capture.StartRecording();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize audio capture: {ex.Message}");
        }
    }

    public void ChangeAudioDevice(string deviceId, string deviceName)
    {
        try
        {
            if (_capture != null)
            {
                _capture.StopRecording();
                _capture.DataAvailable -= OnDataAvailable;
                _capture.Dispose();
                _capture = null;
            }

            _appConfig.SelectedAudioDeviceId = deviceId;
            _appConfig.SelectedAudioDeviceName = deviceName;

            lock (_bufferLock)
            {
                _audioBuffer.Clear();
            }

            _waveformData = new float[BUFFER_SIZE];
            _fftData = new float[BUFFER_SIZE / 2];

            SetupAudioCapture();

            Console.WriteLine($"Switched to audio device: {deviceName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to change audio device: {ex.Message}");
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_bufferLock)
        {
            // Convert byte array to float array more efficiently
            int sampleCount = e.BytesRecorded / 4; // 4 bytes per float sample

            for (int i = 0; i < sampleCount; i++)
            {
                float sample = BitConverter.ToSingle(e.Buffer, i * 4);
                _audioBuffer.Add(sample);
            }

            if (_audioBuffer.Count > BUFFER_SIZE * 2)
            {
                int samplesToRemove = _audioBuffer.Count - BUFFER_SIZE * 2;
                _audioBuffer.RemoveRange(0, samplesToRemove);
            }
        }
    }

    private static float[] CreateHannWindow(int size)
    {
        var window = new float[size];
        for (int i = 0; i < size; i++)
        {
            float t = (float)i / (size - 1);
            window[i] = 0.5f - 0.5f * MathF.Cos(2 * MathF.PI * t);
        }
        return window;
    }

    private void ProcessAudioData()
    {
        lock (_bufferLock)
        {
            if (_audioBuffer.Count < BUFFER_SIZE) return;

            int startIndex = _audioBuffer.Count - BUFFER_SIZE;
            for (int i = 0; i < BUFFER_SIZE; i++)
            {
                _waveformData[i] = _audioBuffer[startIndex + i];
            }
        }

        // If waveform is silent, set FFT data to zero
        bool isSilent = _waveformData.All(x => MathF.Abs(x) < 0.0001f);
        int fftLen = BUFFER_SIZE / 2;
        if (isSilent)
        {
            _fftData = new float[fftLen];
            return;
        }

        // Apply Hann window to waveform data before FFT
        for (int i = 0; i < BUFFER_SIZE; i++)
        {
            _fftBuffer[i] = new Complex32(_waveformData[i] * HannWindow[i], 0);
        }
        // Perform FFT
        Fourier.Forward(_fftBuffer, FourierOptions.Matlab);

        // // Extract magnitudes for first half (avoid mirroring)
        // for (int i = 0; i < BUFFER_SIZE / 2; i++)
        // {
        //     _fftData[i] = _fftBuffer[i].Magnitude;
        //     // _fftData[i] = MathF.Sqrt(_fftBuffer[i].Magnitude);
        // }

        // cut out silent mid frequencies ...
        int belowThreshold = Enumerable.Range(0, fftLen)
            .Count(i => _fftBuffer[i].Magnitude < 0.005f);
        float cutFraction = Math.Min(0.25f, (float)belowThreshold / fftLen);
        int cutLen = (int)(cutFraction * fftLen);
        int midStart = (fftLen - cutLen) / 2;
        int midEnd = midStart + cutLen;
        _fftData = [.. Enumerable.Range(0, fftLen)
            .Where(i => i < midStart || i >= midEnd)
            .Select(i => _fftBuffer[i].Magnitude)];
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
        base.OnRenderFrame(e);

        ProcessAudioData();

        _imGuiController.Update(this, (float)e.Time);

        var projection = Matrix4.CreateOrthographicOffCenter(0, ClientSize.X, ClientSize.Y, 0, -1, 1);

        // Update and render all visualizers (including background & post-processing)
        _visualizerManager.UpdateVisualizers(_waveformData, _fftData, e.Time);
        _visualizerManager.RenderVisualizers(projection, ClientSize);

        if (_showConfigWindow)
        {
            _configGui.Render();
        }

        _imGuiController.Render();

        SwapBuffers();
    }

    protected override void OnUpdateFrame(FrameEventArgs e)
    {
        base.OnUpdateFrame(e);

        if (KeyboardState.IsKeyPressed(Keys.Escape))
        {
            Close();
        }

        if (KeyboardState.IsKeyPressed(Keys.F3))
        {
            _showConfigWindow = !_showConfigWindow;
        }
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        Console.WriteLine($"Window resized: {e.Width}x{e.Height}");
        base.OnResize(e);
        if (WindowState != WindowState.Minimized)
        {
            Console.WriteLine($"Window restored: {ClientSize.X}x{ClientSize.Y}");
            if (_lastWindowState == WindowState.Minimized)
            {
                Console.WriteLine("Restoring visualizers after minimize...");
                // Re-initialize all visualizers to restore them after minimize
                _visualizerManager.LoadVisualizerConfigurations(_appConfig.VisualizerConfigs, _appConfig.EnabledVisualizers);
            }
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
            _imGuiController?.WindowResized(ClientSize.X, ClientSize.Y);
        }
        _lastWindowState = WindowState;
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        _imGuiController?.PressChar((char)e.Unicode);
    }

    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);
        _imGuiController?.OnKeyDown(e.Key);
    }

    protected override void OnKeyUp(KeyboardKeyEventArgs e)
    {
        base.OnKeyUp(e);
        _imGuiController?.OnKeyUp(e.Key);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        _imGuiController?.MouseScroll(e.Offset);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);

        // Force ImGui to save its settings before shutdown
        ImGuiNET.ImGui.SaveIniSettingsToDisk(CAudioVisualizer.Configuration.AppConfig.GetImGuiConfigPath());

        if (_appConfig != null && _visualizerManager != null)
        {
            _visualizerManager.SaveVisualizerConfigurations(_appConfig.VisualizerConfigs, _appConfig.EnabledVisualizers);
            _appConfig.SaveConfiguration(CAudioVisualizer.Configuration.AppConfig.GetConfigFilePath());
        }
    }

    protected override void OnUnload()
    {
        base.OnUnload();

        _capture?.StopRecording();
        _capture?.Dispose();

        _visualizerManager?.Dispose();
        _imGuiController?.Dispose();
    }
}

static class Program
{
    private static readonly GLFWCallbacks.ErrorCallback GlfwErrorCallback =
        (error, description) => Console.WriteLine($"GLFW error {error}: {description}");

    static void Main()
    {
        GLFWProvider.SetErrorCallback(GlfwErrorCallback);

        var tempConfig = new CAudioVisualizer.Configuration.AppConfig();
        tempConfig.LoadConfiguration(CAudioVisualizer.Configuration.AppConfig.GetConfigFilePath());

        var gameWindowSettings = GameWindowSettings.Default;

        var monitors = Monitors.GetMonitors();

        NativeWindowSettings nativeWindowSettings;

        if (tempConfig.SpanAllMonitors && monitors.Count > 1)
        {
            int minX = monitors.Min(m => m.ClientArea.Min.X);
            int minY = monitors.Min(m => m.ClientArea.Min.Y);
            int maxX = monitors.Max(m => m.ClientArea.Max.X);
            int maxY = monitors.Max(m => m.ClientArea.Max.Y);

            nativeWindowSettings = new NativeWindowSettings()
            {
                Title = "Audio Visualizer - Made by Silas Kraume (Multi-Monitor)",
                Flags = ContextFlags.Default,
                Profile = ContextProfile.Core,
                APIVersion = new Version(4, 6),
                WindowBorder = WindowBorder.Hidden,
                WindowState = WindowState.Normal,
                ClientSize = new Vector2i(maxX - minX, maxY - minY),
                Location = new Vector2i(minX, minY)
            };
        }
        else
        {
            var selectedMonitor = monitors.Count > tempConfig.SelectedMonitorIndex && tempConfig.SelectedMonitorIndex >= 0
                ? monitors[tempConfig.SelectedMonitorIndex]
                : Monitors.GetPrimaryMonitor();

            nativeWindowSettings = new NativeWindowSettings()
            {
                Title = "Audio Visualizer - Made by Silas Kraume",
                Flags = ContextFlags.Default,
                Profile = ContextProfile.Core,
                APIVersion = new Version(4, 6),
                WindowBorder = WindowBorder.Hidden,
                WindowState = WindowState.Normal,
                ClientSize = new Vector2i(selectedMonitor.HorizontalResolution, selectedMonitor.VerticalResolution),
                Location = new Vector2i(selectedMonitor.WorkArea.Min.X, selectedMonitor.WorkArea.Min.Y)
            };
        }

        using var window = new AudioVisualizerWindow(gameWindowSettings, nativeWindowSettings);
        window.Run();
    }
}
