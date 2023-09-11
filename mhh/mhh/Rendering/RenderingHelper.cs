
using eyecandy;
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

    /// <summary>
    /// Retreives a shader from the cache, optionally replacing it with a newly loaded and
    /// compiled copy if the --reload command was used (which sets the ReplacedCachedShader
    /// flag).
    /// </summary>
    public static CachedShader GetShader(IRenderer renderer, VisualizerConfig visualizerConfig)
        => GetShader(renderer, visualizerConfig.VertexShaderPathname, visualizerConfig.FragmentShaderPathname);

    /// <summary>
    /// Retreives a shader from the cache, optionally replacing it with a newly loaded and
    /// compiled copy if the --reload command was used (which sets the ReplacedCachedShader
    /// flag).
    /// </summary>
    public static CachedShader GetShader(IRenderer renderer, string vertexShaderPathname, string fragmentShaderPathname)
    {
        if(string.IsNullOrWhiteSpace(vertexShaderPathname) 
            || string.IsNullOrWhiteSpace(fragmentShaderPathname))
        {
            LogInvalidReason("Invalid shader pathname", renderer);
            return null;
        }

        var shaderKey = CachedShader.KeyFrom(vertexShaderPathname, fragmentShaderPathname);

        if (ReplaceCachedShader)
        {
            Caching.Shaders.Remove(shaderKey);
            ReplaceCachedShader = false;
        }

        var shader = Caching.Shaders.Get(shaderKey);
        if (shader is null)
        {
            shader = new(vertexShaderPathname, fragmentShaderPathname);
            if (!shader.IsValid)
            {
                LogInvalidReason("Shader invalid", renderer);
                return null;
            }

            var cached = Caching.Shaders.TryAdd(shaderKey, shader);
            if (!cached) LogHelper.Logger.LogWarning($"Failed to cache shader for {vertexShaderPathname} and {fragmentShaderPathname}");
        }
        return shader;
    }

    /// <summary>
    /// If the shader isn't cached, it will be disposed. Otherwise, the cache will dispose of
    /// it later and the caller should simply set the local reference to null.
    /// </summary>
    public static void DisposeUncachedShader(CachedShader shader)
    {
        if (shader is not null && !Caching.Shaders.ContainsKey(shader.Key)) shader.Dispose();
    }

    /// <summary>
    /// Returns an IVisualizer matching the primary visualizer listed in the config file.
    /// The caller must invoke the Initialize method on the returned object instance.
    /// </summary>
    public static IVisualizer GetVisualizer(IRenderer renderer, VisualizerConfig visualizerConfig)
        => GetVisualizer(renderer, visualizerConfig.VisualizerTypeName);

    /// <summary>
    /// Returns an IVisualizer matching the requested type name. The caller must invoke the
    /// Initialize method on the returned object instance.
    /// </summary>
    public static IVisualizer GetVisualizer(IRenderer renderer, string visualizerTypeName)
    {
        var vizType = Caching.KnownVisualizers.FindType(visualizerTypeName);
        if (vizType is null)
        {
            LogInvalidReason($"Visualizer type not recognized: {visualizerTypeName}", renderer);
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
