
using System.Reflection;
using System.Text;
using OpenTK.Audio.OpenAL;
using Mpris.DBus;
using Tmds.DBus.Protocol;

namespace mhh;

/// <inheritdocs/>
public class OSInteropLinux : IOSInteropFactory<OSInteropLinux>, IOSInterop
{
    private static OSInteropLinux Instance;

    private static Connection DBusConnection;
    private static MediaPlayer SelectedMediaPlayer;
    private string MediaTrackMessage = Const.MediaTrackUnavailable;

    /// <inheritdocs/>
    public static OSInteropLinux Create()
    {
        throw new NotImplementedException("Use CreateAsync instead");
    }

    /// <inheritdocs/>
    public static async Task<OSInteropLinux> CreateAsync()
    {
        if (Instance is null) Instance = new OSInteropLinux();

        try
        {
            Instance.MprisRefreshConnection();
        }
        catch(Exception ex) 
        {
            // no logger exists yet
            Console.WriteLine($"Error trying to find media player:\n{ex.Message}");
            DBusConnection?.Dispose();
            DBusConnection = null;
        }

        return Instance;
    }
    
    private OSInteropLinux()
    {}
    
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

        Console.WriteLine("\n---------------------------------------------------------------");
        
        Console.WriteLine("\nCurrently running media player services:");

        var task = MprisGetServiceDescriptions();
        task.Wait();
        var services = task.Result;
        if (!task.IsCompletedSuccessfully || !services.Any())
        {
            Console.WriteLine("  (none found)");
        }
        else
        {
            foreach(var svc in services) Console.WriteLine(svc);
        }
        
        Console.WriteLine("\n---------------------------------------------------------------\n");
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

    private async Task MprisRefreshConnection()
    {
        SelectedMediaPlayer = null;
        DBusConnection?.Dispose();
        DBusConnection = new Connection(Address.Session!);
        await DBusConnection.ConnectAsync();
    }
    
    private async Task<(List<string> service, List<string> status)> MprisGetServices(bool getStatus = true, bool refreshConnection = false, bool updateSelectedMediaPlayer = true)
    {
        if (refreshConnection || DBusConnection is null) await MprisRefreshConnection();
        List<string> service = new();
        List<string> status = new();
        
        // find all media players
        var svcarray = await DBusConnection.ListServicesAsync();
        service = svcarray.Where(service => service.StartsWith(Const.DBusMediaPlayer2Prefix, StringComparison.Ordinal)).ToList();

        // bail out if no players, or not requesting status and not updating media player
        if (!service.Any() || (!getStatus && !updateSelectedMediaPlayer)) return (service, status);

        // get media player statuses
        foreach (var serviceUri in service)
        {
            var mpris = new MprisService(DBusConnection, serviceUri);
            var player = new MediaPlayer(mpris);
            var playerStatus = await player.GetPlaybackStatus();
            status.Add(playerStatus);
        }
        if (!updateSelectedMediaPlayer) return (service, status);

        UpdateMediaPlayerSelection(service, status);
        return (service, status);
    }

    private void UpdateMediaPlayerSelection(List<string> service, List<string> status)
    {
        var useAnyService = string.IsNullOrEmpty(Program.AppConfig.LinuxMediaService);
        var wildcardName = Program.AppConfig.LinuxMediaService.EndsWith('*');
        var serviceName = wildcardName
            ? Program.AppConfig.LinuxMediaService.TrimEnd('*')
            : Program.AppConfig.LinuxMediaService;
        
        // TODO set SelectedMediaPlayer
    }

    // used by ListAudioDevices
    private async Task<List<string>> MprisGetServiceDescriptions(bool refreshConnection = false)
    {
        List<string> retval = new();
        var mpris = await MprisGetServices(getStatus: false, refreshConnection: refreshConnection, updateSelectedMediaPlayer: false);
        foreach (var serviceUri in mpris.service)
        {
            var svc = new MprisService(DBusConnection, serviceUri);
            var player = new MediaPlayer(svc);
            var status = await player.GetPlaybackStatus();
            StringBuilder sb = new("  ");
            sb.Append(serviceUri.Substring(Const.DBusMediaPlayer2Prefix.Length));
            sb.Append(" (");
            sb.Append(status);
            if (status == "Playing")
            {
                var media = await player.GetPlaybackDetails();
                if (!string.IsNullOrWhiteSpace(media.title))
                {
                    sb.Append(": ");
                    sb.Append(media.title);

                    if (!string.IsNullOrWhiteSpace(media.album))
                    {
                        sb.Append(" / ");
                        sb.Append(media.album);
                    }

                    if (!string.IsNullOrWhiteSpace(media.artist))
                    {
                        sb.Append(" / ");
                        sb.Append(media.artist);
                    }
                }

            }
            sb.Append(")");
            retval.Add(sb.ToString());
        }

        return retval;
    }
    
    /// <inheritdocs/>
    public void Dispose()
    {
        bool IsDisposed = true;

        SelectedMediaPlayer = null;
        DBusConnection?.Dispose();
        DBusConnection = null;
        
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}