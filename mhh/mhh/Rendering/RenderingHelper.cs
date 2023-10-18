
using eyecandy;
using Microsoft.Extensions.Logging;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using StbImageSharp;
using System.Runtime.CompilerServices;

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
    /// When true, CalculateViewportResolution will use fxResolutionLimit to restrict rendering size.
    /// </summary>
    public static bool UseFXResolutionLimit = false;

    /// <summary>
    /// When true, CalculateViewportResolution will use fxResolutionLimit to restrict rendering size.
    /// </summary>
    public static bool UseCrossfadeResolutionLimit = false;

    /// <summary>
    /// Use this instead of the window object's ClientSize property. This will remain
    /// stable during resize events (OnResize will fire many times as the user drags the
    /// window border, the new size is assigned by OnWindowUpdate which is suspended
    /// until resizing is completed).
    /// </summary>
    public static Vector2i ClientSize 
    { 
        get => StoredClientSize; 
        set => StoredClientSize = new(value.X, value.Y); 
    }
    private static Vector2i StoredClientSize;

    /// <summary>
    /// Retreives a visualizer shader from the cache, optionally replacing it with a newly loaded and
    /// compiled copy if the --reload command was used (which sets the ReplacedCachedShader flag).
    /// </summary>
    public static CachedShader GetVisualizerShader(IRenderer renderer, VisualizerConfig visualizerConfig)
        => GetVisualizerShader(renderer, visualizerConfig.VertexShaderPathname, visualizerConfig.FragmentShaderPathname, visualizerConfig.LibraryConfigs);

    /// <summary>
    /// Retreives a visualizer shader from the cache, optionally replacing it with a newly loaded and
    /// compiled copy if the --reload command was used (which sets the ReplacedCachedShader flag).
    /// </summary>
    public static CachedShader GetVisualizerShader(IRenderer renderer, string vertexShaderPathname, string fragmentShaderPathname, List<LibraryShaderConfig> libraryConfigs = null)
        => GetCachedShader(Caching.VisualizerShaders, renderer, vertexShaderPathname, fragmentShaderPathname, libraryConfigs);

    /// <summary>
    /// Retreives an FX shader from the cache, optionally replacing it with a newly loaded and
    /// compiled copy if the --reload command was used (which sets the ReplacedCachedShader flag).
    /// </summary>
    public static CachedShader GetFXShader(IRenderer renderer, string vertexShaderPathname, string fragmentShaderPathname, List<LibraryShaderConfig> libraryConfigs = null)
        => GetCachedShader(Caching.FXShaders, renderer, vertexShaderPathname, fragmentShaderPathname, libraryConfigs);

    /// <summary>
    /// If the shader isn't cached, it will be disposed. Otherwise, the cache will dispose of
    /// it later and the caller should simply set the local reference to null.
    /// </summary>
    public static void DisposeUncachedShader(CachedShader shader)
    {
        if (shader is null) return;
        LogHelper.Logger?.LogTrace("RenderingHelper.DisposeUncachedShader() ----------------------------");

        if (!Caching.VisualizerShaders.ContainsKey(shader.Key) && !Caching.FXShaders.ContainsKey(shader.Key))
        {
            LogHelper.Logger?.LogTrace($"  Disposed key {shader.Key}");
            shader.Dispose();
        }
        else
        {
            LogHelper.Logger?.LogTrace($"  Shader key is cached {shader.Key}");
        }
    }

    /// <summary>
    /// Returns an IVisualizer matching the primary visualizer listed in the config file.
    /// The caller must invoke the Initialize method on the returned object instance.
    /// </summary>
    public static IVertexSource GetVertexSource(IRenderer renderer, VisualizerConfig visualizerConfig)
        => GetVertexSource(renderer, visualizerConfig.VertexSourceTypeName);

    /// <summary>
    /// Returns an IVisualizer matching the requested type name. The caller must invoke the
    /// Initialize method on the returned object instance.
    /// </summary>
    public static IVertexSource GetVertexSource(IRenderer renderer, string vertexSourceTypeName)
    {
        var vsType = Caching.KnownVertexSources.FindType(vertexSourceTypeName);
        if (vsType is null)
        {
            LogInvalidReason($"VertexSource type not recognized: {vertexSourceTypeName}", renderer);
            return null;
        }

        var vs = Activator.CreateInstance(vsType) as IVertexSource;
        return vs;
    }

    /// <summary>
    /// Maps visualizer config [textures] data to GLImageTexture resource assignments and loads
    /// the indicated filenames.
    /// </summary>
    public static IReadOnlyList<GLImageTexture> GetTextures(string ownerName, ConfigFile configSource)
    {
        if (!configSource.Content.ContainsKey("textures")) return null;

        var textureDefs = configSource.Content["textures"];
        var resources = RenderManager.ResourceManager.CreateTextureResources(ownerName, textureDefs.Count);
        int i = 0;
        foreach (var tex in textureDefs)
        {
            var res = resources[i++];

            var parts = tex.Value.Split(':', Const.SplitOptions);
            if(parts.Length == 2)
            {
                res.Filename = parts[1];

                if (parts[0].EndsWith("!"))
                {
                    res.UniformName = parts[0].Substring(0, tex.Key.Length - 1);
                    res.ImageLoaded = LoadImageFile(res, TextureWrapMode.ClampToEdge);
                }
                else
                {
                    res.UniformName = parts[0];
                    res.ImageLoaded = LoadImageFile(res);
                }
            }
            else
            {
                res.ImageLoaded = false;
            }
        }

        return resources;
    }

    /// <summary>
    /// Called by VertexSource RenderFrame to set any loaded image texture uniforms before drawing.
    /// </summary>
    public static void SetTextureUniforms(IReadOnlyList<GLImageTexture> textures, Shader shader)
    {
        if (textures is null) return;
        foreach(var tex in textures)
        {
            if(tex.ImageLoaded) shader.SetTexture(tex.UniformName, tex.TextureHandle, tex.TextureUnit);
        }
    }

    /// <summary>
    /// Called by VertexSource RenderFrame to set any globally-defined uniforms like randomseed and date.
    /// </summary>
    public static void SetGlobalUniforms(Shader shader, params Dictionary<string, float>[] uniforms)
    {
        shader.SetUniform("randomseed", Program.AppWindow.UniformRandomSeed);
        shader.SetUniform("randomnumber", Program.AppWindow.UniformRandomNumber);
        shader.SetUniform("date", Program.AppWindow.UniformDate);
        shader.SetUniform("clocktime", Program.AppWindow.UniformClockTime);

        foreach(var list in uniforms)
        {
            if(list is not null)
            {
                foreach (var uniform in list)
                {
                    shader.SetUniform(uniform.Key, uniform.Value);
                }
            }
        }
    }

    /// <summary>
    /// When a resize event occurs (or a renderer is starting for the first time), this
    /// determines whether the full display area (viewport) size should be used, or if
    /// the upper resolution limit (if specified in the viz.conf) should apply, and what
    /// the resulting viewport/render-target resolution should be. Any FX limit is applied
    /// if the active renderer is either the FXRenderer or Crossfade.
    /// </summary>
    public static (Vector2 resolution, bool isFullResolution) CalculateViewportResolution(int renderResolutionLimit, int fxResolutionLimit = 0)
    {
        var w = ClientSize.X;
        var h = ClientSize.Y;

        var limit = (fxResolutionLimit == 0 || (!UseFXResolutionLimit && !UseCrossfadeResolutionLimit))
            ? renderResolutionLimit
            : fxResolutionLimit;

        double larger = Math.Max(w, h);
        double smaller = Math.Min(w, h);
        if(limit == 0 || larger <= limit) return (new(w, h), true);

        double aspect = smaller / larger;
        var scaled = (int)(limit * aspect);
        if(w > h)
        {
            w = limit;
            h = scaled;
        }
        else
        {
            w = scaled;
            h = limit;
        }
        return (new(w, h), false);
    }

    /// <summary>
    /// Produces a timestamped GL resource owner name unique to the type and purpose.
    /// Useful for debugging resource allocation leaks.
    /// </summary>
    public static string MakeOwnerName(string usage, [CallerFilePath] string owner = "")
        => $"{Path.GetFileNameWithoutExtension(owner)} {usage} {DateTime.Now:yyyy-MM-dd HH:mm:ss.ffff}";

    private static CachedShader GetCachedShader(CacheLRU<string, CachedShader> cache, IRenderer renderer, string vertexShaderPathname, string fragmentShaderPathname, List<LibraryShaderConfig> libraryConfigs = null)
    {
        if (string.IsNullOrWhiteSpace(vertexShaderPathname)
            || string.IsNullOrWhiteSpace(fragmentShaderPathname))
        {
            LogInvalidReason("Invalid shader pathname", renderer);
            return null;
        }

        var libraries = GetCachedLibraryShaders(renderer, libraryConfigs);
        if (!renderer.IsValid) return null;

        var shaderKey = CachedShader.KeyFrom(vertexShaderPathname, fragmentShaderPathname);

        if (ReplaceCachedShader)
        {
            cache.Remove(shaderKey);
            ReplaceCachedShader = false;
        }

        var shader = cache.Get(shaderKey);
        if (shader is null)
        {
            shader = new(vertexShaderPathname, fragmentShaderPathname, libraries);
            if (!shader.IsValid)
            {
                LogInvalidReason("Shader invalid", renderer);
                return null;
            }

            var cached = cache.TryAdd(shaderKey, shader);
            if (!cached && !cache.CachingDisabled) LogHelper.Logger.LogWarning($"{nameof(GetCachedShader)} TryAdd failed to store or find {vertexShaderPathname} and {fragmentShaderPathname}");
        }
        return shader;
    }

    private static ShaderLibrary[] GetCachedLibraryShaders(IRenderer renderer, List<LibraryShaderConfig> libraryConfigs)
    {
        if (libraryConfigs is null) return new ShaderLibrary[0];
        var libs = new ShaderLibrary[libraryConfigs.Count];

        for(int i = 0; i < libraryConfigs.Count; i++)
        {
            var shader = Caching.LibraryShaders.Get(libraryConfigs[i]);
            if(shader is null)
            {
                shader = new(libraryConfigs[i]);
                if(!shader.IsValid)
                {
                    LogInvalidReason($"Shader library {libraryConfigs[i].Pathname} invalid", renderer);
                    return null;
                }

                var cached = Caching.LibraryShaders.TryAdd(libraryConfigs[i], shader);
                if (!cached && !Caching.LibraryShaders.CachingDisabled) LogHelper.Logger.LogWarning($"{nameof(GetCachedLibraryShaders)} TryAdd failed to store or find {libraryConfigs[i].Pathname}");
            }
            libs[i] = shader;
        }
        return libs;
    }

    private static void LogInvalidReason(string reason, IRenderer renderer)
    {
        renderer.IsValid = false;
        renderer.InvalidReason = reason;
        LogHelper.Logger.LogError(reason);
    }

    private static bool LoadImageFile(GLImageTexture tex, TextureWrapMode wrapMode = TextureWrapMode.Repeat)
    {
        var pathname = PathHelper.FindFile(Program.AppConfig.TexturePath, tex.Filename);
        if (pathname is null) return false;

        // OpenGL origin is bottom left instead of top left
        StbImage.stbi_set_flip_vertically_on_load(1);

        GL.ActiveTexture(tex.TextureUnit);
        GL.BindTexture(TextureTarget.Texture2D, tex.TextureHandle);

        using var stream = File.OpenRead(pathname);
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, image.Width, image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, image.Data);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)wrapMode);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)wrapMode);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        return true;
    }
}
