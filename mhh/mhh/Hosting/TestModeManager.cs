
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
    public string TestFilename;

    /// <summary>
    /// The complete pathname of the test target.
    /// </summary>
    public string TestPathname;

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
                if (string.IsNullOrEmpty(PathHelper.FindFile(Program.AppConfig.CrossfadePath, PathHelper.MakeFragFilename(filename)))) return "ERR: Crossfade .frag not found";
                break;
            }
        }

        return string.Empty;
    }

    private bool VizCacheSetting = Caching.VisualizerShaders.CachingDisabled;
    private bool FxCacheSetting = Caching.FXShaders.CachingDisabled;
    private bool LibCacheSetting = Caching.LibraryShaders.CachingDisabled;

    public TestModeManager(TestMode mode, string filename)
    {
        Mode = mode;
        TestFilename = filename;
        
        switch (mode)
        {
            case TestMode.Viz:
            {
                TestPathname = PathHelper.FindConfigFile(Program.AppConfig.VisualizerPath, TestFilename);
                var paths = FilteredPathList(Program.AppConfig.FXPath);
                TestContent = PathHelper.GetConfigFiles(paths);
                break;
            }

            case TestMode.FX:
            {
                TestPathname = PathHelper.FindConfigFile(Program.AppConfig.FXPath, TestFilename);
                var paths = FilteredPathList(Program.AppConfig.VisualizerPath);
                TestContent = PathHelper.GetConfigFiles(paths);
                break;
            }

            case TestMode.Fade:
            {
                var TestPathname = PathHelper.FindFile(Program.AppConfig.CrossfadePath, PathHelper.MakeFragFilename(filename));
                if (string.IsNullOrEmpty(TestPathname))
                {
                    mode = TestMode.None;
                    break;
                }

                var key = CachedShader.KeyFrom(ApplicationConfiguration.PassthroughVertexPathname, TestPathname);
                CrossfadeShader = Caching.CrossfadeShaders.FirstOrDefault(s => s.Key.Equals(key)) ?? new(ApplicationConfiguration.PassthroughVertexPathname, TestPathname); ;
                if (!CrossfadeShader.IsValid)
                {
                    mode = TestMode.None;
                    CrossfadeShader?.Dispose();
                    CrossfadeShader = null;
                    break;
                }

                var paths = FilteredPathList(Program.AppConfig.VisualizerPath);
                TestContent = PathHelper.GetConfigFiles(paths);
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
                message = $"Testing Viz: {TestFilename}\nShowing FX: ";
                break;
            }

            case TestMode.FX:
            {
                message = $"Testing FX: {TestFilename}\nShowing Viz: ";
                break;
            }

            case TestMode.Fade:
            {
                message = $"Testing crossfade: {TestFilename}\nShowing Viz: ";
                break;
            }
        }
        
        if(ContentIndex == -1)
        {
            message += "\n(press +/- to begin testing)";
        }
        else
        {
            message += $@"{TestContent[ContentIndex]} ({ContentIndex + 1}/{TestContent.Count})
+/- show next/prev combo
 R  reload test shader
 Q  quit testing";
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
        if (ContentIndex < 0) ContentIndex = TestContent.Count - 1;
        QueueTestContent();
    }

    /// <summary>
    /// Forces current combination to reload.
    /// </summary>
    public void Reload()
    {
        QueueTestContent();
    }

    /// <summary>
    /// Terminates testing. Only Command_Test should call this.
    /// </summary>
    public void EndTest()
    {
        // this calls HostWindow.AbortTestMode which calls Dispose
        Program.AppWindow.Command_Idle();
    }

    private string FilteredPathList(string pathspec)
    {
        var exclusions = PathHelper.GetIndividualPaths(Program.AppConfig.TestingExcludePaths).ToList();
        var targets = pathspec.Split(Path.PathSeparator, Const.SplitOptions)
            .Where(p => !exclusions.Any(e => p.StartsWith(e)));
        var result = string.Join(Path.PathSeparator, targets);
        return result;
    }
    
    private void QueueTestContent()
    {
        switch (Mode)
        {
            case TestMode.Viz:
            {
                var contentPathname = PathHelper.FindConfigFile(Program.AppConfig.FXPath, TestContent[ContentIndex]);
                Program.AppWindow.Command_Load(TestPathname, contentPathname, forTestMode: true);
                break;
            }

            case TestMode.FX:
            {
                var contentPathname = PathHelper.FindConfigFile(Program.AppConfig.VisualizerPath, TestContent[ContentIndex]);
                Program.AppWindow.Command_Load(contentPathname, TestPathname, forTestMode: true);
                break;
            }

            case TestMode.Fade:
            {
                var contentPathname = PathHelper.FindConfigFile(Program.AppConfig.VisualizerPath, TestContent[ContentIndex]);
                Program.AppWindow.Command_Load(contentPathname, forTestMode: true);
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
        TestFilename = null;
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
