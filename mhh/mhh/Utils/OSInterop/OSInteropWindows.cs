
using NAudio.CoreAudioApi;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace mhh;

/// <inheritdocs/>
public class OSInteropWindows :  IOSInteropFactory<OSInteropWindows>, IOSInterop
{
    // Currently Windows Terminal will only minimize, not hide. Microsoft
    // is debating whether and how to fix that (not just about Powershell):
    // https://github.com/microsoft/terminal/issues/12464
    [DllImport("kernel32.dll")] static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    static readonly int SW_HIDE = 0;
    static readonly int SW_SHOW = 5;
    private static bool ConsoleVisible = true;
   
    private const string SpotifyProcessName = "SPOTIFY";
    private string MediaTrackMessage = Const.MediaTrackUnavailable;

    private static OSInteropWindows Instance;

    /// <inheritdocs/>
    public static OSInteropWindows Create()
    {
        if (Instance is null) Instance = new OSInteropWindows();
        return Instance;
    }

    /// <inheritdocs/>
    public static Task<OSInteropWindows> CreateAsync()
        => Task.FromResult(Create());
    
    private OSInteropWindows() 
    {}
    
    /// <inheritdocs/>
    public bool IsConsoleVisible
    {
        get => ConsoleVisible;
        set
        {
            ConsoleVisible = value;
            var hwnd = GetConsoleWindow();
            var flag = ConsoleVisible ? SW_SHOW : SW_HIDE;
            ShowWindow(hwnd, flag);
        }
    }

    /// <inheritdocs/>
    public void ListAudioDevices()
    {
        Console.WriteLine("\nWASAPI Device Information (excluding \"Not Present\" devices)");
        Console.WriteLine("---------------------------------------------------------------");

        using var enumerator = new MMDeviceEnumerator();

        var states = DeviceState.Active | DeviceState.Disabled | DeviceState.Unplugged;

        Console.Write("\nPlayback devices:\n  ");
        var playbackDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, states);
        if (playbackDevices.Count > 0) Console.WriteLine(string.Join("\n  ", playbackDevices.Select(d => $"{d.FriendlyName} ({d.State})")));
        if (playbackDevices.Count == 0) Console.WriteLine("  <none>");

        Console.Write("\nCapture devices:\n  ");
        var captureDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, states);
        if (captureDevices.Count > 0) Console.WriteLine(string.Join("\n  ", captureDevices.Select(d => $"{d.FriendlyName} ({d.State})")));
        if (captureDevices.Count == 0) Console.WriteLine("  <none>");

        Console.WriteLine("\nDefault devices:");
        try
        {
            var defaultPlayback = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            Console.WriteLine($"  Playback: {defaultPlayback.FriendlyName}");
        }
        catch
        {
            Console.WriteLine("  Playback: <none>");
        }
        try
        {
            var defaultCapture = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            Console.WriteLine($"  Capture:  {defaultCapture.FriendlyName}");
        }
        catch
        {
            Console.WriteLine("  Capture:  <none>");
        }
        
        Console.WriteLine("---------------------------------------------------------------\n");
    }

    /// <inheritdocs/>
    public string GetMediaTrackForDisplay
    {
        get => $"{Const.MusicNote} {MediaTrackMessage.Replace(" - ", $"\n{Const.MusicNote} ")}";
    }
    
    /// <inheritdocs/>
    public void UpdateMediaTrackInfo()
    {
        var p = Process.GetProcessesByName(SpotifyProcessName);
        if(p.Length > 0)
        {
            if (p[0].MainWindowTitle != MediaTrackMessage)
            {
                MediaTrackMessage = p[0].MainWindowTitle;
                if (MediaTrackMessage.StartsWith("Spotify"))
                {
                    MediaTrackMessage = Const.MediaTrackUnavailable;
                }
                else
                {
                    RenderManager.TextManager.SetPopupText(GetMediaTrackForDisplay);
                }
            }
        }
        else
        {
            MediaTrackMessage = Const.MediaTrackUnavailable;
        }
    }
    
    /// <inheritdocs/>
    public void Dispose()
    {
        bool IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}