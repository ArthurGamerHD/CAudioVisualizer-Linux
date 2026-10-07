using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace CAudioVisualizer.Core;

public class AudioDeviceInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string DataFlow { get; set; } = "";
    public bool IsDefault { get; set; } = false;
    public MMDevice? Device { get; set; }
}

public static class AudioDeviceManager
{
    public static List<AudioDeviceInfo> GetAvailableAudioDevices()
    {
        if (!OperatingSystem.IsWindows())
            return GetPipeWireDevices();

        var devices = new List<AudioDeviceInfo>();

        try
        {
            var enumerator = new MMDeviceEnumerator();

            // Add default device first
            try
            {
                var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
                devices.Add(new AudioDeviceInfo
                {
                    Id = defaultDevice.ID,
                    Name = $"Default - {defaultDevice.FriendlyName}",
                    IsDefault = true,
                    DataFlow = "Playback ",
                    Device = defaultDevice
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get default audio device: {ex.Message}");
            }

            // Add all other render devices
            var deviceOutputCollection = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            foreach (var device in deviceOutputCollection)
            {
                // Skip if already added
                if (devices.Any(d => d.Id == device.ID))
                    continue;

                devices.Add(new AudioDeviceInfo
                {
                    Id = device.ID,
                    Name = device.FriendlyName,
                    IsDefault = false,
                    DataFlow = "Playback ",
                    Device = device
                });
            }

            var deviceInputCollection = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);

            foreach (var device in deviceInputCollection)
            {
                // Skip if already added
                if (devices.Any(d => d.Id == device.ID))
                    continue;

                devices.Add(new AudioDeviceInfo
                {
                    Id = device.ID,
                    Name = device.FriendlyName,
                    IsDefault = false,
                    DataFlow = "Recording",
                    Device = device
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to enumerate audio devices: {ex.Message}");

            // Fallback: add a default entry
            devices.Add(new AudioDeviceInfo
            {
                Id = "",
                Name = "Default Device",
                IsDefault = true,
                DataFlow = "Playback ",
                Device = null
            });
        }

        return devices;
    }

    public static IWaveIn CreateAudioCapture(string deviceId)
    {
        if (!OperatingSystem.IsWindows())
            return new PipeWireCapture(deviceId);

        try
        {
            if (string.IsNullOrEmpty(deviceId))
            {
                // Use default device
                return new WasapiLoopbackCapture();
            }
            else
            {
                // Use specific device
                var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDevice(deviceId);
                if (device.DataFlow == DataFlow.Render)
                {
                    return new WasapiLoopbackCapture(device);
                }
                else if (device.DataFlow == DataFlow.Capture)
                {
                    return new WasapiCapture(device);
                }
                else
                {
                    throw new InvalidOperationException("Selected device is neither render nor capture.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to create audio capture for device {deviceId}: {ex.Message}");
            Console.WriteLine("Falling back to default device...");

            // Fallback to default device
            return new WasapiLoopbackCapture();
        }
    }

    public static string GetDeviceName(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
            return "Default Device";

        if (!OperatingSystem.IsWindows())
        {
            var source = GetPipeWireDevices().FirstOrDefault(d => d.Id == deviceId);
            return source?.Name ?? "Unknown Device";
        }

        try
        {
            var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDevice(deviceId);
            return device.FriendlyName;
        }
        catch (Exception)
        {
            return "Unknown Device";
        }
    }

    private static List<AudioDeviceInfo> GetPipeWireDevices()
    {
        var devices = new List<AudioDeviceInfo>
        {
            new AudioDeviceInfo
            {
                Id = "",
                Name = "Default - System Audio",
                IsDefault = true,
                DataFlow = "Playback "
            }
        };

        try
        {
            foreach (var (name, description) in PipeWireCapture.ListSources().OrderBy(s => !s.Name.EndsWith(".monitor")))
            {
                bool isMonitor = name.EndsWith(".monitor");
                devices.Add(new AudioDeviceInfo
                {
                    Id = name,
                    Name = description,
                    IsDefault = false,
                    DataFlow = isMonitor ? "Playback " : "Recording"
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to enumerate PipeWire sources: {ex.Message}");
        }

        return devices;
    }
}
