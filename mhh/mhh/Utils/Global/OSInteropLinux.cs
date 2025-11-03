
using OpenTK.Audio.OpenAL;

namespace mhh;

/// <inheritdocs/>
public class OSInteropLinux :IOSInterop
{
    // TODO implement Linux terminal visibility control
    /// <inheritdocs/>
    public bool IsConsoleVisible { get; set; } = true;

    /// <inheritdocs/>
    public void ListAudioDevices()
    {
        Console.WriteLine("\nOpenAL Device Information");
        Console.WriteLine("---------------------------------------------------------------");

        var contextDevices = ALC.GetStringList(GetEnumerationStringList.DeviceSpecifier);
        Console.WriteLine($"\nContext devices:\n  {string.Join("\n  ", contextDevices)}");

        var defaultContext = ALC.GetString(ALDevice.Null, AlcGetString.DefaultDeviceSpecifier);
        Console.WriteLine($"\nDefault context device:\n  {defaultContext}");
        foreach (var d in contextDevices)
        {
            if (d.Contains("OpenAL Soft"))
            {
                defaultContext = d;
                Console.WriteLine($"  Using: \"{defaultContext}\"");
            }
        }

        var allDevices = ALC.EnumerateAll.GetStringList(GetEnumerateAllContextStringList.AllDevicesSpecifier);
        Console.WriteLine($"\nPlayback devices:\n  {string.Join("\n  ", allDevices)}");

        var list = ALC.GetStringList(GetEnumerationStringList.CaptureDeviceSpecifier);
        Console.WriteLine($"\nCapture devices:\n  {string.Join("\n  ", list)}");

        var defaultPlayback = ALC.GetString(ALDevice.Null, AlcGetString.DefaultAllDevicesSpecifier);
        var defaultCapture = ALC.GetString(ALDevice.Null, AlcGetString.CaptureDefaultDeviceSpecifier);
        Console.WriteLine($"\nDefault  devices:\n  Playback: {defaultPlayback}\n  Capture: {defaultCapture}");

        Console.WriteLine("---------------------------------------------------------------\n");
    }

    /// <inheritdocs/>
    public string GetMediaTrackForDisplay
    {
        // TODO read DBUS media metadata
        get => Const.MediaTrackUnavailable;
    }

    /// <inheritdocs/>
    public void UpdateMediaTrackInfo()
    {
        // TODO read DBUS media metadata
    }
}