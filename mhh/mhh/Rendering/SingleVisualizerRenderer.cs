
using mhh.Utils;

namespace mhh;

public class SingleVisualizerRenderer : IRenderer
{
    public bool IsValid { get; set; } = true;
    public string InvalidReason { get; set; } = string.Empty;
    public string Filename { get; set; }

    public VisualizerConfig Config;
    public IVisualizer Visualizer;
    public CachedShader Shader;

    public SingleVisualizerRenderer(VisualizerConfig visualizerConfig)
    {
        Config = visualizerConfig;
        Filename = Path.GetFileNameWithoutExtension(Config.ConfigSource.Pathname);

        Shader = RenderingHelper.GetShader(this, visualizerConfig);
        if (!IsValid) return;

        Visualizer = RenderingHelper.GetVisualizer(this, visualizerConfig);
        if (!IsValid) return;

        Visualizer.Initialize(Config, Shader);
    }

    public void RenderFrame()
    {
        Program.AppWindow.Eyecandy.SetTextureUniforms(Shader);
        Program.AppWindow.SetStandardUniforms(Shader);
        Visualizer.RenderFrame(Shader);
    }

    public void Dispose()
    {
        if (IsDisposed) return;

        Visualizer?.Dispose();
        Visualizer = null;

        RenderingHelper.DisposeUncachedShader(Shader);
        Shader = null;

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
