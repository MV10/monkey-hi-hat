
using eyecandy;

namespace mhh;

/// <summary>
/// Testing uses +/- to page through content. It shows a permanent text overlay
/// and temporarily disables shader caching.
/// </summary>
public class TestModeManager : IDisposable
{
    /// <summary>
    /// What is being tested, or None if the manager is unloading.
    /// </summary>
    public TestMode Mode;

    /// <summary>
    /// The viz or FX conf filename, or a crossfade frag filename to test.
    /// </summary>
    public string Filename;

    /// <summary>
    /// A list of visualizer or FX filenames to test against.
    /// </summary>
    public IReadOnlyList<string> TestContent;

    /// <summary>
    /// Which TestContent entry is being loaded or displayed
    /// </summary>
    public int ContentIndex = -1;

    /// <summary>
    /// Populated when TestMode is Fade
    /// </summary>
    public Shader CrossfadeShader;

    /// <summary>
    /// Verifies the target file exists
    /// </summary>
    public static string Validate(TestMode mode, string filename)
    {
        switch(mode)
        {
            case TestMode.Viz:
            {
                if (string.IsNullOrEmpty(PathHelper.FindConfigFile(Program.AppConfig.VisualizerPath, filename))) return "ERR: Visualizer .conf not found";
                break;
            }

            case TestMode.FX:
            {
                if (string.IsNullOrEmpty(PathHelper.FindConfigFile(Program.AppConfig.FXPath, filename))) return "ERR: FX .conf not found";
                break;
            }

            case TestMode.Fade:
            {
                if (string.IsNullOrEmpty(PathHelper.FindFile(Program.AppConfig.VisualizerPath, GetFragFilename(filename)))) return "ERR: Crossfade .frag not found";
                break;
            }
        }

        return string.Empty;
    }

    private static string GetFragFilename(string filename)
        => (!filename.EndsWith(".frag", Const.CompareFlags)) ? filename += ".frag" : filename;

    private bool VizCacheSetting = Caching.VisualizerShaders.CachingDisabled;
    private bool FxCacheSetting = Caching.FXShaders.CachingDisabled;
    private bool LibCacheSetting = Caching.LibraryShaders.CachingDisabled;

    public TestModeManager(TestMode mode, string filename)
    {
        Mode = mode;
        Filename = filename;
        switch (mode)
        {
            case TestMode.Viz:
            {
                TestContent = PathHelper.GetConfigFiles(Program.AppConfig.FXPath).Skip(Program.AppConfig.TestingSkipFXCount).ToList();
                break;
            }

            case TestMode.FX:
            {
                TestContent = PathHelper.GetConfigFiles(Program.AppConfig.VisualizerPath).Skip(Program.AppConfig.TestingSkipVizCount).ToList();
                break;
            }

            case TestMode.Fade:
            {
                var fragPathname = PathHelper.FindFile(Program.AppConfig.VisualizerPath, GetFragFilename(filename));
                if (string.IsNullOrEmpty(fragPathname))
                {
                    mode = TestMode.None;
                    break;
                }

                var vertPathname = Path.Combine(ApplicationConfiguration.InternalShaderPath, "passthrough.vert");

                var key = CachedShader.KeyFrom(vertPathname, fragPathname);
                CrossfadeShader = Caching.CrossfadeShaders.FirstOrDefault(s => s.Key.Equals(key)) ?? new(vertPathname, fragPathname); ;
                if (!CrossfadeShader.IsValid)
                {
                    mode = TestMode.None;
                    CrossfadeShader?.Dispose();
                    CrossfadeShader = null;
                    break;
                }

                TestContent = PathHelper.GetConfigFiles(Program.AppConfig.VisualizerPath).Skip(Program.AppConfig.TestingSkipVizCount).ToList();
                break;
            }
        }

        Caching.VisualizerShaders.CachingDisabled = true;
        Caching.FXShaders.CachingDisabled = true;
        Caching.LibraryShaders.CachingDisabled = true;

        RenderManager.TextManager.SetOverlayText(OverlayText, forcePermanence: true);

        Program.AppWindow.Focus();
    }

    /// <summary>
    /// Provides current status to TextManager
    /// </summary>
    public string OverlayText()
    {
        var message = "";
        switch (Mode)
        {
            case TestMode.None:
            {
                message = "Test mode exiting or failed to initialize!";
                break;
            }
            case TestMode.Viz:
            {
                message = $"Testing Viz: {Filename}\nShowing FX:  ";
                break;
            }

            case TestMode.FX:
            {
                message = $"Testing FX:  {Filename}\nShowing Viz: ";
                break;
            }

            case TestMode.Fade:
            {
                message = $"Testing crossfade: {Filename}\nShowing Viz: ";
                break;
            }
        }
        
        if(ContentIndex == -1)
        {
            message += "(press +/- to begin testing)";
        }
        else
        {
            message += $"{TestContent[ContentIndex]} ({ContentIndex + 1}/{TestContent.Count})";
        }
        return message;
    }

    /// <summary>
    /// Advances to the next test-content entry. Will wrap-around.
    /// </summary>
    public void Next()
    {
        if (Mode == TestMode.None || TestContent?.Count == 0) return;
        ContentIndex++;
        if (ContentIndex == TestContent.Count) ContentIndex = 0;
        QueueTestContent();
    }

    /// <summary>
    /// Reverses to the previous test-content entry. Will wrap-around.
    /// </summary>
    public void Previous()
    {
        if (Mode == TestMode.None || TestContent?.Count == 0) return;
        ContentIndex--;
        if (ContentIndex == -1) ContentIndex = TestContent.Count - 1;
        QueueTestContent();
    }

    /// <summary>
    /// Terminates testing.
    /// </summary>
    public void EndTest()
    {
        Dispose();
        Program.AppWindow.Command_Idle();
    }

    private void QueueTestContent()
    {
        switch (Mode)
        {
            case TestMode.Viz:
            {
                Program.ProcessSwitches(["--load", Filename, TestContent[ContentIndex]]);
                break;
            }

            case TestMode.FX:
            {
                Program.ProcessSwitches(["--load", TestContent[ContentIndex], Filename]);
                break;
            }

            case TestMode.Fade:
            {
                Program.ProcessSwitches(["--load", TestContent[ContentIndex]]);
                break;
            }
        }
    }

    public void Dispose()
    {
        if (IsDisposed) return;

        Mode = TestMode.None;
        ContentIndex = -1;
        TestContent = null;
        Filename = null;
        if(CrossfadeShader is not CachedShader) CrossfadeShader?.Dispose();
        CrossfadeShader = null;
        RenderManager.TextManager.Clear();
        RenderManager.TextManager.TogglePermanence();
        Caching.VisualizerShaders.CachingDisabled = VizCacheSetting;
        Caching.FXShaders.CachingDisabled = FxCacheSetting;
        Caching.LibraryShaders.CachingDisabled = LibCacheSetting;

        IsDisposed = true;
        GC.SuppressFinalize(true);
    }
    private bool IsDisposed = false;
}
