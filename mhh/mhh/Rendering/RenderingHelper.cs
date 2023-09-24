
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
        if(shader is not null)
        {
            LogHelper.Logger?.LogTrace("RenderingHelper.DisposeUncachedShader() ----------------------------");
            if (!Caching.Shaders.ContainsKey(shader.Key))
            {
                LogHelper.Logger?.LogTrace($"  Disposed key {shader.Key}");
                shader.Dispose();
            }
            else
            {
                LogHelper.Logger?.LogTrace($"  Shader key is cached {shader.Key}");
            }
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
    public static void SetGlobalUniforms(Shader shader)
    {
        shader.SetUniform("randomseed", Program.AppWindow.UniformRandomSeed);
        shader.SetUniform("randomnumber", Program.AppWindow.UniformRandomNumber);
        shader.SetUniform("date", Program.AppWindow.UniformDate);
        shader.SetUniform("clocktime", Program.AppWindow.UniformClockTime);
    }

    /// <summary>
    /// When a resize event occurs (or a renderer is starting for the first time), this
    /// determines whether the full display area (viewport) size should be used, or if
    /// the upper resolution limit (if specified in the viz.conf) should apply, and what
    /// the resulting viewport/render-target resolution should be.
    /// </summary>
    public static (Vector2 resolution, bool isFullResolution) CalculateViewportResolution(int renderResolutionLimit)
    {
        var w = ClientSize.X;
        var h = ClientSize.Y;

        double larger = Math.Max(w, h);
        double smaller = Math.Min(w, h);
        if(renderResolutionLimit == 0 || larger <= renderResolutionLimit) return (new(w, h), true);

        double aspect = smaller / larger;
        var scaled = (int)(renderResolutionLimit * aspect);
        if(w > h)
        {
            w = renderResolutionLimit;
            h = scaled;
        }
        else
        {
            w = scaled;
            h = renderResolutionLimit;
        }
        return (new(w, h), false);
    }

    /// <summary>
    /// Produces a timestamped GL resource owner name unique to the type and purpose.
    /// Useful for debugging resource allocation leaks.
    /// </summary>
    public static string MakeOwnerName(string usage, [CallerFilePath] string owner = "")
        => $"{Path.GetFileNameWithoutExtension(owner)} {usage} {DateTime.Now:yyyy-MM-dd HH:mm:ss.ffff}";

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
