
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace mhh;

/// <inheritdocs/>
public class OSInteropWindows : IOSInterop
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
}