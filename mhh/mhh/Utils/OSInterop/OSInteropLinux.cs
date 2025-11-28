
using System.Text;
using OpenTK.Audio.OpenAL;
using Microsoft.Extensions.Logging;
using Mpris.DBus;
using Tmds.DBus.Protocol;

namespace mhh;

/// <inheritdocs/>
public class OSInteropLinux : IOSInteropFactory<OSInteropLinux>, IOSInterop
{
    private static OSInteropLinux Instance;

    private static ILogger Logger;

    private static Connection DBusConnection;
    private static MediaPlayer SelectedMediaPlayer;
    private static string SelectedMediaPlayerUri;
    private string MediaTrackMessage = Const.MediaTrackUnavailable;

    private const int MaxFailureCount = 10;
    private int ConnectionFailureCount = 0;

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
            await Instance.RefreshDBusConnection();
        }
        catch(Exception ex) 
        {
            // no logger exists yet
            Console.WriteLine($"Error trying to find media player:\n{ex.Message}");
            DBusConnection?.Dispose();
            DBusConnection = null;
            Instance.ConnectionFailureCount++;
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

        var task = GetMediaPlayerDescriptions();
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
        get => $"{Const.MusicNote} {MediaTrackMessage.Replace("\n", $"\n{Const.MusicNote} ")}";
    }

    /// <inheritdocs/>
    public void UpdateMediaTrackInfo()
    {
        var previousMessage = MediaTrackMessage;
        MediaTrackMessage = Const.MediaTrackUnavailable;

        if (SelectedMediaPlayer is null)
        {
            GetMediaPlayerServices(refreshConnection: (DBusConnection is null), updateSelectedMediaPlayer:true).Wait();
            if (SelectedMediaPlayer is null) return;
        }

        var mediaTask = SelectedMediaPlayer.GetPlaybackDetails(refresh:true);
        mediaTask.Wait();
        var media = mediaTask.Result;
        if (!string.IsNullOrWhiteSpace(media.title))
        {
            MediaTrackMessage = media.title;
            if (!string.IsNullOrWhiteSpace(media.album)) MediaTrackMessage += $"\n{media.album}";
            if (!string.IsNullOrWhiteSpace(media.artist)) MediaTrackMessage += $"\n{media.artist}";
        }

        if (MediaTrackMessage != previousMessage)
        {
            RenderManager.TextManager.SetPopupText(GetMediaTrackForDisplay);
        }
    }

    /// <summary>
    /// Disconnects if connected, then tries to establish a new connection. If that fails,
    /// a counter is incremented which limits the number of new attempts. Failures will raise
    /// an exception to the caller which should be handled. This does not automatically select
    /// a media player service. For a more complete configuration, call GetMediaPlayerServices
    /// with refreshConnection:true.
    /// </summary>
    private async Task RefreshDBusConnection()
    {
        if (ConnectionFailureCount >= MaxFailureCount) return;
        
        SelectedMediaPlayer = null;
        SelectedMediaPlayerUri = null;
        DBusConnection?.Dispose();
        try
        {
            DBusConnection = new Connection(Address.Session!);
            await DBusConnection.ConnectAsync();
        }
        catch(Exception ex)
        {
            DBusConnection?.Dispose();
            DBusConnection = null;
            ConnectionFailureCount++;
            Logger?.LogWarning($"Media player DBus connection failed {ConnectionFailureCount} times, retry limit is {MaxFailureCount}");
            throw;
        }
    }
    
    /// <summary>
    /// Returns a list of media player service URIs and optionally a matching list of service
    /// status values (MPRIS defines these as Playing, Paused, or Stopped). By default this
    /// will update SelectedMediaPlayer.
    /// </summary>
    private async Task<(List<string> service, List<string> status)> GetMediaPlayerServices(bool getStatus = true, bool refreshConnection = false, bool updateSelectedMediaPlayer = true)
    {
        List<string> service = new();
        List<string> status = new();

        try
        {
            if (refreshConnection || DBusConnection is null) await RefreshDBusConnection();
        }
        catch
        {
            return (service, status);
        }
        
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

    /// <summary>
    /// Applies naming and status rules to the provided list of service URIs and status values.
    /// </summary>
    private void UpdateMediaPlayerSelection(List<string> service, List<string> status)
    {
        SelectedMediaPlayer = null;
        SelectedMediaPlayerUri = null;
        
        var wildcardName = Program.AppConfig.LinuxMediaService.EndsWith('*');
        var targetName = wildcardName
            ? Program.AppConfig.LinuxMediaService.TrimEnd('*')
            : Program.AppConfig.LinuxMediaService;

        // remove non-matching service names
        if (targetName.Length > 0)
        {
            for (var i = service.Count - 1; i >= 0; i--)
            {
                var name = ServiceNameFromUri(service[i]);
                if ((wildcardName && !name.StartsWith(targetName)) || (!wildcardName && name != targetName))
                {
                    service.RemoveAt(i);
                    status.RemoveAt(i);
                }
            }
        }

        // any left?
        if (!service.Any()) return;
        
        // default to the first in the list
        int selected = 0;

        // look for one in Playing status
        for (var i = 0; i < service.Count; i++)
        {
            if (status[i] == "Playing")
            {
                selected = i;
                break;
            }
        }

        // set it up
        SelectedMediaPlayerUri = service[selected];
        SelectedMediaPlayer = new(new MprisService(DBusConnection, service[selected]));
        Logger?.LogInformation($"Selected media player: {SelectedMediaPlayerUri}");
    }

    /// <summary>
    /// Creates an output-formatted list of media player services for ListAudioDevices.
    /// </summary>
    private async Task<List<string>> GetMediaPlayerDescriptions(bool refreshConnection = false)
    {
        List<string> retval = new();
        var mpris = await GetMediaPlayerServices(getStatus: false, refreshConnection: refreshConnection, updateSelectedMediaPlayer: false);
        foreach (var serviceUri in mpris.service)
        {
            var svc = new MprisService(DBusConnection, serviceUri);
            var player = new MediaPlayer(svc);
            var status = await player.GetPlaybackStatus();
            StringBuilder sb = new("  ");
            sb.Append(ServiceNameFromUri(serviceUri));
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
    
    /// <summary>
    /// Strips the URI prefix from the service names.
    /// </summary>
    private string ServiceNameFromUri(string serviceUri)
        => serviceUri.Substring(Const.DBusMediaPlayer2Prefix.Length);
    
    /// <inheritdocs/>
    public void CreateLogger()
    {
        Logger = LogHelper.CreateLogger(nameof(IOSInterop));
    }
    
    /// <inheritdocs/>
    public void Dispose()
    {
        bool IsDisposed = true;

        SelectedMediaPlayer = null;
        SelectedMediaPlayerUri = null;
        DBusConnection?.Dispose();
        DBusConnection = null;
        
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
