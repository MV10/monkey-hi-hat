
using mhh.Utils;
using Microsoft.Extensions.Logging;

namespace mhh;

public static class RenderingHelper
{
    /// <summary>
    /// When true, GetShader will delete the matching shader key so that a new instance will
    /// be loaded, compiled, and cached. The flag is cleared after processing. This is set by
    /// the app window when the --reload command issued (primarily for shader dev/test).
    /// </summary>
    public static bool ReplaceCachedShader = false;

    public static CachedShader GetShader(IRenderer renderer, VisualizerConfig visualizerConfig)
    {
        var shaderKey = CachedShader.KeyFrom(visualizerConfig.VertexShaderPathname, visualizerConfig.FragmentShaderPathname);

        if(ReplaceCachedShader)
        {
            Caching.Shaders.Remove(shaderKey);
            ReplaceCachedShader = false;
        }

        var shader = Caching.Shaders.Get(shaderKey);
        if (shader is null)
        {
            shader = new(visualizerConfig.VertexShaderPathname, visualizerConfig.FragmentShaderPathname);
            if (!shader.IsValid)
            {
                LogInvalidReason("Shader invalid", renderer);
                return null;
            }

            var cached = Caching.Shaders.TryAdd(shaderKey, shader);
            if (!cached) LogHelper.Logger.LogWarning($"Failed to cache shader for {visualizerConfig.ConfigSource.Pathname}");
        }
        return shader;
    }

    public static IVisualizer GetVisualizer(IRenderer renderer, VisualizerConfig visualizerConfig)
    {
        var vizType = Caching.KnownVisualizers.FindType(visualizerConfig.VisualizerTypeName);
        if (vizType is null)
        {
            LogInvalidReason($"Visualizer type not recognized: {visualizerConfig.VisualizerTypeName}", renderer);
            return null;
        }

        var viz = Activator.CreateInstance(vizType) as IVisualizer;
        return viz;
    }

    private static void LogInvalidReason(string reason, IRenderer renderer)
    {
        renderer.IsValid = false;
        renderer.InvalidReason = reason;
        LogHelper.Logger.LogError(reason);
    }
}
