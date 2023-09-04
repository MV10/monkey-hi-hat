
using mhh.Utils;
using Microsoft.Extensions.Logging;

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

    private void LogInvalidReason(string reason)
    {
        IsValid = false;
        InvalidReason = reason;
        LogHelper.Logger.LogError(reason);
    }

    private bool IsDisposed = false;
    public void Dispose()
    {
        if (IsDisposed) return;

        Visualizer?.Dispose();
        if(Shader is not null && !Caching.Shaders.ContainsKey(Shader.Key)) Shader.Dispose();

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}
