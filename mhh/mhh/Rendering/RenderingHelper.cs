
using eyecandy;
using FFMediaToolkit.Decoding;
using FFMediaToolkit.Graphics;
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
    /// Settings that control FFMediaToolkit video decoding and rendering.
    /// </summary>
    public static MediaOptions VideoMediaOptions = new()
    {
        StreamsToLoad = MediaMode.Video,            // no audio support planned
        VideoPixelFormat = ImagePixelFormat.Rgba32, // RGBA for direct OpenGL compatibility
        FlipVertically = false,                     // default; Program.InitializeAndWait can change if AppConfig VideoFlip is FFmpeg
    };

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
    /// Returns an IVertexSource matching the primary visualizer listed in the config file.
    /// The caller must invoke the Initialize method on the returned object instance.
    /// </summary>
    public static IVertexSource GetVertexSource(IRenderer renderer, VisualizerConfig visualizerConfig)
        => GetVertexSource(renderer, visualizerConfig.VertexSourceTypeName);

    /// <summary>
    /// Returns an IVertexSource matching the requested type name. The caller must invoke the
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
    /// Maps visualizer config [textures] and [videos] data to GLImageTexture resource assignments
    /// and loads the indicated files.
    /// </summary>
    public static IReadOnlyList<GLImageTexture> GetTextures(string ownerName, ConfigFile configSource)
    {
        if (!configSource.Content.ContainsKey("textures") && !configSource.Content.ContainsKey("videos")) return null;

        var rand = new Random();

        // key is uniform name, List is filenames (>1 means choose one at random)
        var textureDefs = LoadTextureDefinitions(configSource, "textures");
        var videoDefs = LoadTextureDefinitions(configSource, "videos");

        var totalRequired = (textureDefs?.Count ?? 0) + (videoDefs?.Count ?? 0);
        if (totalRequired == 0) return null;
        var resources = RenderManager.ResourceManager.CreateTextureResources(ownerName, totalRequired);

        int resourceIndex = 0;

        if(textureDefs is not null)
        {
            foreach (var tex in textureDefs)
            {
                var res = resources[resourceIndex++];

                res.Filename = tex.Value[rand.Next(tex.Value.Count)];

                var uniformName = tex.Key;
                if (uniformName.EndsWith("!"))
                {
                    res.UniformName = uniformName.Substring(0, uniformName.Length - 1);
                    res.Loaded = LoadImageFile(res, TextureWrapMode.ClampToEdge);
                }
                else
                {
                    res.UniformName = uniformName;
                    res.Loaded = LoadImageFile(res);
                }
            }
        }

        if(videoDefs is not null)
        {
            foreach (var vid in videoDefs)
            {
                var res = resources[resourceIndex++];

                res.Filename = vid.Value[rand.Next(vid.Value.Count)];

                var uniformName = vid.Key;
                res.UniformName = uniformName;
                res.Loaded = LoadVideoFile(res);
            }
        }

        return resources;
    }

    /// <summary>
    /// Prepares a texture resource with the file identified in GLImageTexture.
    /// </summary>
    public static bool LoadImageFile(GLImageTexture tex, TextureWrapMode wrapMode = TextureWrapMode.Repeat, string pathspec = "")
    {
        var paths = (pathspec == "") ? Program.AppConfig.TexturePath : pathspec;
        var pathname = PathHelper.FindFile(paths, tex.Filename);
        if (pathname is null) return false;

        using var stream = File.OpenRead(pathname);
        StbImage.stbi_set_flip_vertically_on_load(1); // OpenGL origin is bottom left instead of top left
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        GL.ActiveTexture(tex.TextureUnit);
        GL.BindTexture(TextureTarget.Texture2D, tex.TextureHandle);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, image.Width, image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, image.Data);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)wrapMode);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)wrapMode);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        return true;
    }

    /// <summary>
    /// Prepares a texture resource with the file identified in GLImageTexture.
    /// </summary>
    public static bool LoadVideoFile(GLImageTexture tex, string pathspec = "")
    {
        var paths = (pathspec == "") ? Program.AppConfig.TexturePath : pathspec;
        var pathname = PathHelper.FindFile(paths, tex.Filename);
        if (pathname is null) return false;

        try
        {
            tex.VideoData = new();
            tex.VideoData.File = MediaFile.Open(pathname, VideoMediaOptions);
            tex.VideoData.Stream = tex.VideoData.File.Video;

            if (tex.VideoData.Stream == null)
            {
                // TODO log the error instead of writing to console
                Console.WriteLine("No video stream found in the file.");
                return false;
            }

            tex.VideoData.Width = tex.VideoData.Stream.Info.FrameSize.Width;
            tex.VideoData.Height = tex.VideoData.Stream.Info.FrameSize.Height;
            tex.VideoData.Resolution = new(tex.VideoData.Width, tex.VideoData.Height);

            GL.ActiveTexture(tex.TextureUnit);
            GL.BindTexture(TextureTarget.Texture2D, tex.TextureHandle);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, tex.VideoData.Width, tex.VideoData.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        }
        catch (Exception ex)
        {
            // TODO log the error instead of writing to console
            Console.WriteLine($"Error loading video:\n{ex.Message}\n{ex.InnerException?.Message}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Called by IVertexSource RenderFrame to set any loaded image/video texture uniforms before drawing.
    /// Also updates any video textures that are currently playing.
    /// </summary>
    public static void SetTextureUniforms(IReadOnlyList<GLImageTexture> textures, Shader shader)
    {
        if (textures is null) return;

        foreach (var tex in textures)
        {
            if (tex.Loaded)
            {
                if (tex.VideoData is not null)
                {
                    if (Program.AppWindow.Renderer.TimePaused)
                    {
                        if (!tex.VideoData.IsPaused)
                        {
                            tex.VideoData.IsPaused = true;
                            tex.VideoData.Clock.Stop();
                        }
                    }
                    else
                    {
                        if (tex.VideoData.IsPaused)
                        {
                            tex.VideoData.IsPaused = false;
                            tex.VideoData.Clock.Start();
                        }

                        UpdateVideoTexture(tex);
                    }
                    shader.SetUniform($"{tex.UniformName}_duration", (float)tex.VideoData.Duration.TotalSeconds);
                    shader.SetUniform($"{tex.UniformName}_progress", (float)tex.VideoData.Clock.Elapsed.TotalSeconds / (float)tex.VideoData.Duration.TotalSeconds);
                    shader.SetUniform($"{tex.UniformName}_resolution", tex.VideoData.Resolution);
                }

                shader.SetTexture(tex.UniformName, tex.TextureHandle, tex.TextureUnit);
            }
        }
    }

    /// <summary>
    /// Called by Renderer implementations' RenderFrame to set any globally-defined uniforms like randomseed and date.
    /// </summary>
    public static void SetGlobalUniforms(Shader shader, params Dictionary<string, float>[] uniforms)
    {
        shader.SetUniform("randomseed", Program.AppWindow.UniformRandomSeed);
        shader.SetUniform("randomnumber", Program.AppWindow.UniformRandomNumber);
        shader.SetUniform("date", Program.AppWindow.UniformDate);
        shader.SetUniform("clocktime", Program.AppWindow.UniformClockTime);
        shader.SetUniform("fxactive", Program.AppWindow.UniformFXActive);

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

    private static Dictionary<string, List<string>> LoadTextureDefinitions(ConfigFile configSource, string sectionName)
    {
        // return dictionary key is uniform name, List is filenames (>1 means choose one at random)

        if (!configSource.Content.ContainsKey(sectionName)) return null;
        var definitions = new Dictionary<string, List<string>>();
        foreach (var def in configSource.Content[sectionName])
        {
            var parts = def.Value.Split(':', Const.SplitOptions);
            if (parts.Length == 2)
            {
                var uniform = parts[0];
                var filename = parts[1];
                if (!definitions.ContainsKey(uniform)) definitions.Add(uniform, new());
                if (!definitions[uniform].Contains(filename)) definitions[uniform].Add(filename);
            }
        }
        return definitions;
    }

    private static void UpdateVideoTexture(GLImageTexture tex)
    {
        if (tex.VideoData is null || !tex.Loaded || tex.VideoData.Stream is null) return;

        if (!tex.VideoData.Clock.IsRunning)
        {
            if (tex.VideoData.IsPaused) return;
            tex.VideoData.Clock.Start();
        }

        if (tex.VideoData.Clock.Elapsed == tex.VideoData.LastUpdateTime) return; // abort if time hasn't changed

        if (tex.VideoData.Clock.Elapsed > tex.VideoData.Duration)
        {
            tex.VideoData.Clock.Restart(); // always loop
        }

        /*
        Performance Considerations: For sequential playback without seeking, consider switching to 
        TryGetNextFrame(out ImageData frame) which returns a boolean indicating success. The current 
        implementation uses GetFrame for flexible time-based updates, suitable for looping or non-linear playback.

        Error Handling: In production, add try-catch around GetFrame as it may throw exceptions on severe
        errors (e.g., corrupt files). The empty check handles most end-of-stream cases gracefully.
        */
        try
        {
            var frame = tex.VideoData.Stream.GetFrame(tex.VideoData.Clock.Elapsed);

            tex.VideoData.LastUpdateTime = tex.VideoData.Clock.Elapsed;

            if (!frame.Data.IsEmpty && tex.VideoData.Stream.Position != tex.VideoData.LastStreamPosition)
            {
                tex.VideoData.LastStreamPosition = tex.VideoData.Stream.Position;

                if (Program.AppConfig.VideoFlip == VideoFlipMode.Internal)
                {
                    int rowBytes = tex.VideoData.Width * 4; // 4 bytes per pixel for RGBA
                    byte[] flippedData = new byte[frame.Data.Length];
                    for (int y = 0; y < tex.VideoData.Height; y++)
                    {
                        int sourceOffset = y * frame.Stride;
                        int destOffset = (tex.VideoData.Height - 1 - y) * rowBytes;
                        frame.Data.Slice(sourceOffset, rowBytes).CopyTo(flippedData.AsSpan(destOffset, rowBytes));
                    }

                    GL.BindTexture(TextureTarget.Texture2D, tex.TextureHandle);
                    unsafe
                    {
                        fixed (byte* ptr = flippedData)
                        {
                            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, tex.VideoData.Width, tex.VideoData.Height, PixelFormat.Rgba, PixelType.UnsignedByte, (IntPtr)ptr);
                        }
                    }
                }
                else
                {
                    GL.BindTexture(TextureTarget.Texture2D, tex.TextureHandle);
                    unsafe
                    {
                        fixed (byte* ptr = frame.Data)
                        {
                            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, tex.VideoData.Width, tex.VideoData.Height, PixelFormat.Rgba, PixelType.UnsignedByte, (IntPtr)ptr);
                        }
                    }
                }
            }
        }
        catch (EndOfStreamException e)
        {
            // If we reach the end of the stream, restart the clock to loop the video and abort
            tex.VideoData.Clock.Restart();
            return;
        }
        catch (Exception ex)
        {
            // TODO better error handling
        }
    }

}
