using OpenTK.Mathematics;
using CAudioVisualizer.Visualizers;
using CAudioVisualizer.Configuration;

namespace CAudioVisualizer.Core;

public class VisualizerManager : IDisposable
{
    private readonly Dictionary<string, VisualizerInstance> _instances = new();
    private Vector2i _currentWindowSize = new Vector2i(800, 600); // Default fallback size
    private BackgroundRenderer? _backgroundRenderer;
    private PostProcessingRenderer? _postProcessingRenderer;
    private string _audioDeviceInfo = "";

    public VisualizerManager(Vector2i initialWindowSize)
    {
        _currentWindowSize = initialWindowSize;
    }

    public void RegisterVisualizerInstance(VisualizerInstance instance, bool initialize = true)
    {
        if (_instances.ContainsKey(instance.InstanceId))
        {
            Console.WriteLine($"Warning: Visualizer instance '{instance.InstanceId}' is already registered.");
            return;
        }

        _instances[instance.InstanceId] = instance;
        instance.Visualizer.SetVisualizerManager(this);

        // Set instance display name for visualizers that need unique identification
        if (instance.Visualizer is DebugInfoVisualizer debugVisualizer)
        {
            debugVisualizer.SetInstanceDisplayName(instance.DisplayName);

            if (!string.IsNullOrEmpty(_audioDeviceInfo))
            {
                debugVisualizer.SetAudioDeviceInfo(_audioDeviceInfo);
            }
        }

        // Initialize immediately when created dynamically (not during config load)
        if (initialize)
        {
            instance.Visualizer.Initialize();
        }

        Console.WriteLine($"Registered visualizer instance: {instance.DisplayName} ({instance.InstanceId})");
    }

    public bool RemoveVisualizerInstance(string instanceId)
    {
        if (_instances.TryGetValue(instanceId, out var instance))
        {
            instance.Visualizer.Dispose();
            _instances.Remove(instanceId);
            Console.WriteLine($"Removed visualizer instance: {instance.DisplayName} ({instanceId})");
            return true;
        }
        return false;
    }

    public void UpdateVisualizers(float[] waveformData, float[] fftData, double deltaTime)
    {
        _backgroundRenderer?.Update(waveformData, fftData, deltaTime);
        _postProcessingRenderer?.Update(waveformData, fftData, deltaTime);

        foreach (var instance in _instances.Values.OrderBy(i => i.CreatedAt))
        {
            if (instance.Visualizer.IsEnabled)
            {
                instance.Visualizer.Update(waveformData, fftData, deltaTime);
            }
        }
    }

    public void RenderVisualizers(Matrix4 projection, Vector2i windowSize)
    {
        // Update current window size for all visualizers to reference
        _currentWindowSize = windowSize;

        _postProcessingRenderer?.BeginCapture();

        _backgroundRenderer?.Render(projection);

        foreach (var instance in _instances.Values.OrderBy(i => i.CreatedAt))
        {
            if (instance.Visualizer.IsEnabled)
            {
                instance.Visualizer.Render(projection);
            }
        }

        _postProcessingRenderer?.EndCaptureAndRender();
    }

    public Vector2i GetCurrentWindowSize()
    {
        return _currentWindowSize;
    }

    public void SetAudioDeviceInfo(string audioDeviceInfo)
    {
        _audioDeviceInfo = audioDeviceInfo;

        // Update all existing DebugInfoVisualizers
        foreach (var instance in _instances.Values)
        {
            if (instance.Visualizer is DebugInfoVisualizer debugVisualizer)
            {
                debugVisualizer.SetAudioDeviceInfo(audioDeviceInfo);
            }
        }
    }

    public BackgroundRenderer? GetBackgroundRenderer()
    {
        return _backgroundRenderer;
    }

    public PostProcessingRenderer? GetPostProcessingRenderer()
    {
        return _postProcessingRenderer;
    }

    public Dictionary<string, VisualizerInstance> GetAllInstances()
    {
        return _instances;
    }

    public IEnumerable<string> GetAvailableVisualizerTypes()
    {
        return VisualizerFactory.GetAvailableTypes();
    }

    public void ToggleVisualizer(string instanceId, bool enabled)
    {
        if (_instances.TryGetValue(instanceId, out var instance))
        {
            instance.Visualizer.IsEnabled = enabled;
        }
    }

    public void SaveVisualizerConfigurations(Dictionary<string, string> configs, List<string> enabledVisualizers)
    {
        configs.Clear();
        enabledVisualizers.Clear();

        if (_backgroundRenderer != null)
        {
            configs["Background"] = _backgroundRenderer.SaveConfiguration();
        }

        if (_postProcessingRenderer != null)
        {
            configs["PostProcessing"] = _postProcessingRenderer.SaveConfiguration();
        }

        foreach (var kvp in _instances)
        {
            var instance = kvp.Value;
            var instanceId = kvp.Key;

            if (instance.Visualizer.IsEnabled)
            {
                enabledVisualizers.Add(instanceId);
            }

            if (instance.Visualizer is IConfigurable configurable)
            {
                try
                {
                    var config = configurable.SaveConfiguration();
                    configs[instanceId] = config;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to save configuration for {instanceId}: {ex.Message}");
                }
            }
        }
    }

    public void LoadVisualizerConfigurations(Dictionary<string, string> configs, List<string> enabledVisualizers)
    {
        // Initialize background renderer
        _backgroundRenderer = new BackgroundRenderer();
        _backgroundRenderer.SetVisualizerManager(this);
        if (configs.TryGetValue("Background", out var backgroundConfig))
        {
            _backgroundRenderer.LoadConfiguration(backgroundConfig);
        }
        _backgroundRenderer.Initialize();

        // Initialize post-processing renderer
        _postProcessingRenderer = new PostProcessingRenderer();
        _postProcessingRenderer.SetVisualizerManager(this);
        if (configs.TryGetValue("PostProcessing", out var postProcessingConfig))
        {
            _postProcessingRenderer.LoadConfiguration(postProcessingConfig);
        }
        _postProcessingRenderer.Initialize();

        // Recreate instances from configuration keys (skip "Background" key)
        foreach (var instanceId in configs.Keys)
        {
            if (instanceId == "Background" || instanceId == "PostProcessing") continue;

            if (!_instances.ContainsKey(instanceId))
            {
                var instance = CreateInstanceFromId(instanceId);
                if (instance != null)
                {
                    RegisterVisualizerInstance(instance, initialize: false);
                }
            }
        }

        // Also create instances for enabled visualizers that might not have configs yet
        foreach (var instanceId in enabledVisualizers)
        {
            if (!_instances.ContainsKey(instanceId))
            {
                var instance = CreateInstanceFromId(instanceId);
                if (instance != null)
                {
                    RegisterVisualizerInstance(instance, initialize: false);
                }
            }
        }

        foreach (var kvp in _instances)
        {
            kvp.Value.Visualizer.IsEnabled = enabledVisualizers.Contains(kvp.Key);
        }

        foreach (var configKvp in configs)
        {
            var instanceId = configKvp.Key;
            var configJson = configKvp.Value;

            if (_instances.TryGetValue(instanceId, out var instance) && instance.Visualizer is IConfigurable configurable)
            {
                try
                {
                    configurable.LoadConfiguration(configJson);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load configuration for {instanceId}: {ex.Message}");
                }
            }
        }

        // Initialize all visualizers
        foreach (var kvp in _instances)
        {
            kvp.Value.Visualizer.Initialize();
        }
    }

    private VisualizerInstance? CreateInstanceFromId(string instanceId)
    {
        // Parse instance ID format: "TypeName_InstanceNumber"
        var parts = instanceId.Split('_');
        if (parts.Length < 2) return null;

        var typeName = parts[0];
        if (!int.TryParse(parts[1], out var instanceNumber)) return null;

        var visualizer = VisualizerFactory.CreateVisualizer(typeName);
        if (visualizer == null) return null;

        var displayName = instanceNumber == 1 ? typeName : $"{typeName} {instanceNumber}";
        var instance = new VisualizerInstance(instanceId, typeName, displayName, visualizer);

        return instance;
    }

    public void Dispose()
    {
        foreach (var instance in _instances.Values)
        {
            instance.Visualizer.Dispose();
        }
        _instances.Clear();

        _backgroundRenderer?.Dispose();
        _postProcessingRenderer?.Dispose();

        GC.SuppressFinalize(this);
    }
}
