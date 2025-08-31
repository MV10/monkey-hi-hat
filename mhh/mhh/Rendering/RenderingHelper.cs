
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
    // created by LogHelper after initialization
    internal static ILogger Logger;

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
    /// Video file playback involves frequently updating the texture with new frames.
    /// Since OpenGL is not thread-safe, this mutex prevents overlapping calls with
    /// the eyecandy background thread that updates audio texture data.
    /// </summary>
    private static readonly Mutex GLTextureLockMutex = new(false, AudioTextureEngine.GLTextureMutexName);

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
        Logger?.LogTrace($"{nameof(DisposeUncachedShader)}");

        if (!Caching.VisualizerShaders.ContainsKey(shader.Key) && !Caching.FXShaders.ContainsKey(shader.Key))
        {
            Logger?.LogTrace($"  Disposed shader {shader.Key}");
            shader.Dispose();
        }
        else
        {
            Logger?.LogTrace($"  Not disposed; shader {shader.Key} is cached");
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
        Logger?.LogTrace($"{nameof(GetVertexSource)} type {vertexSourceTypeName}");

        var vsType = Caching.KnownVertexSources.FindType(vertexSourceTypeName);
        if (vsType is null)
        {
            ShaderInvalid($"VertexSource type not recognized: {vertexSourceTypeName}", renderer);
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

        Logger?.LogTrace($"{nameof(GetTextures)} for {ownerName} from {configSource}");

        var rand = new Random();

        // When RandomImageSync is true, image uniforms (in the [textures] section) which
        // have multiple filenames listed will be assigned the same randomly-chosen index
        // as long as randSyncCount matches the number of filenames, which is derived from
        // the first randomized uniform found. Does not apply to [videos] section.
        bool randomImageSync = 
            configSource.ReadValue("shader", "randomimagesync").ToBool(false) 
            || configSource.ReadValue("fx", "randomimagesync").ToBool(false);
        int randSyncCount = -1;
        int randSyncIndex = -1;

        // key is uniform name, List is filenames (>1 means choose one at random)
        var imageDefs = LoadTextureDefinitions(configSource, "textures");
        var videoDefs = LoadTextureDefinitions(configSource, "videos");

        var totalRequired = (imageDefs?.Count ?? 0) + (videoDefs?.Count ?? 0);
        if (totalRequired == 0) return null;
        var resources = RenderManager.ResourceManager.CreateTextureResources(ownerName, totalRequired);

        int resourceIndex = 0;

        // handle images
        if(imageDefs is not null)
        {
            foreach (var tex in imageDefs)
            {
                var res = resources[resourceIndex++];

                // uniform name ending with '!' means clamp to edge rather than repeat
                var uniformName = tex.Key;
                if (uniformName.EndsWith("!"))
                {
                    res.UniformName = uniformName.Substring(0, uniformName.Length - 1);
                    res.WrapMode = TextureWrapMode.ClampToEdge;
                }
                else
                {
                    res.UniformName = uniformName;
                }

                // pick a random filename if multiple are listed, with optional sync
                var index = rand.Next(tex.Value.Count);
                if (tex.Value.Count > 1 && randomImageSync)
                {
                    if(randSyncCount == -1) randSyncCount = tex.Value.Count;
                    if(tex.Value.Count == randSyncCount)
                    {
                        if(randSyncIndex == -1) randSyncIndex = index;
                        index = randSyncIndex;
                    }
                }
                res.Filename = tex.Value[index];

                res.Loaded = LoadImageFile(res);
            }
        }

        // handle videos
        if(videoDefs is not null)
        {
            foreach (var vid in videoDefs)
            {
                var res = resources[resourceIndex++];

                var uniformName = vid.Key;
                res.UniformName = uniformName;

                res.Filename = vid.Value[rand.Next(vid.Value.Count)];
                res.Loaded = LoadVideoFile(res);
            }
        }

        return resources;
    }

    /// <summary>
    /// Prepares a texture resource with the file identified in GLImageTexture.
    /// </summary>
    public static bool LoadImageFile(GLImageTexture tex, string pathspec = "")
    {
        var paths = (pathspec == "") ? Program.AppConfig.TexturePath : pathspec;
        var pathname = PathHelper.FindFile(paths, tex.Filename);
        if (pathname is null) return false;

        Logger?.LogDebug($"{nameof(LoadImageFile)} loading {pathname}");

        try
        {
            using var stream = File.OpenRead(pathname);
            StbImage.stbi_set_flip_vertically_on_load(1); // OpenGL origin is bottom left instead of top left
            var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

            GL.ActiveTexture(tex.TextureUnit);
            GL.BindTexture(TextureTarget.Texture2D, tex.TextureHandle);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, image.Width, image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, image.Data);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)tex.WrapMode);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)tex.WrapMode);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }
        catch (Exception ex)
        {
            Logger?.LogError($"{nameof(LoadImageFile)}: Error loading image {tex.Filename}\n{ex.Message}\n{ex.InnerException?.Message}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Prepares a texture resource with the file identified in GLImageTexture.
    /// </summary>
    public static bool LoadVideoFile(GLImageTexture tex, string pathspec = "")
    {
        var paths = (pathspec == "") ? Program.AppConfig.TexturePath : pathspec;
        var pathname = PathHelper.FindFile(paths, tex.Filename);
        if (pathname is null) throw new FileNotFoundException();

        Logger?.LogDebug($"{nameof(LoadVideoFile)} loading {pathname}");

        try
        {
            tex.VideoData = new();
            tex.VideoData.File = MediaFile.Open(pathname, VideoMediaOptions);
            tex.VideoData.Stream = tex.VideoData.File.Video;
            tex.VideoData.Pathname = pathname;

            if (tex.VideoData.Stream == null)
            {
                Logger?.LogError($"{nameof(LoadVideoFile)}: No video stream found in the file {tex.Filename}");
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
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }
        catch (Exception ex)
        {
            Logger?.LogError($"{nameof(LoadVideoFile)}: Error loading video {tex.Filename}\n{ex.Message}\n{ex.InnerException?.Message}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Called by IVertexSource RenderFrame to set any loaded image/video texture uniforms before drawing.
    /// </summary>
    public static void SetTextureUniforms(IReadOnlyList<GLImageTexture> textures, Shader shader)
    {
        if (textures is null) return;

        foreach (var tex in textures)
        {
            if (tex.Loaded)
            {
                shader.SetTexture(tex.UniformName, tex.TextureHandle, tex.TextureUnit);
                if (tex.VideoData is not null)
                {
                    shader.SetUniform($"{tex.UniformName}_duration", (float)tex.VideoData.Duration.TotalSeconds);
                    shader.SetUniform($"{tex.UniformName}_progress", (float)tex.VideoData.Clock.Elapsed.TotalSeconds / (float)tex.VideoData.Duration.TotalSeconds);
                    shader.SetUniform($"{tex.UniformName}_resolution", tex.VideoData.Resolution);
                }
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
        shader.SetUniform("silent", Program.AppWindow.UniformSilenceDetected);

        foreach (var list in uniforms)
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
            ShaderInvalid("Invalid shader pathname", renderer);
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
                ShaderInvalid("Shader invalid", renderer);
                return null;
            }

            var cached = cache.TryAdd(shaderKey, shader);
            if (!cached && !cache.CachingDisabled) Logger?.LogWarning($"{nameof(GetCachedShader)} TryAdd failed to store or find {vertexShaderPathname} and {fragmentShaderPathname}");
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
                    ShaderInvalid($"Shader library {libraryConfigs[i].Pathname} invalid", renderer);
                    return null;
                }

                var cached = Caching.LibraryShaders.TryAdd(libraryConfigs[i], shader);
                if (!cached && !Caching.LibraryShaders.CachingDisabled) Logger?.LogWarning($"{nameof(GetCachedLibraryShaders)} TryAdd failed to store or find {libraryConfigs[i].Pathname}");
            }
            libs[i] = shader;
        }
        return libs;
    }

    private static void ShaderInvalid(string reason, IRenderer renderer)
    {
        renderer.IsValid = false;
        renderer.InvalidReason = reason;
        Logger?.LogError(reason);
    }

    private static Dictionary<string, List<string>> LoadTextureDefinitions(ConfigFile configSource, string sectionName)
    {
        // sectionName is either "textures" or "videos"
        // return dictionary key is uniform name, List is filenames (>1 means choose one at random)

        if (!configSource.Content.ContainsKey(sectionName)) return null;
        var definitions = new Dictionary<string, List<string>>();
        foreach (var def in configSource.Content[sectionName])
        {
            // Split count of 2 means split only on first colon; necessary for URIs
            var parts = def.Value.Split(':', 2, Const.SplitOptions); 
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

    // 2025-08-20 Replaced with StbImage's faster buffer flip code inside the pinned section in DecodeVideoFrame
    //private static byte[] FlipVideoFrame(VideoMediaData video, ImageData frame)
    //{
    //    int rowBytes = video.Width * 4; // 4 bytes per pixel for RGBA
    //    byte[] flippedData = new byte[frame.Data.Length];
    //    for (int y = 0; y < video.Height; y++)
    //    {
    //        int sourceOffset = y * frame.Stride;
    //        int destOffset = (video.Height - 1 - y) * rowBytes;
    //        frame.Data.Slice(sourceOffset, rowBytes).CopyTo(flippedData.AsSpan(destOffset, rowBytes));
    //    }
    //    return flippedData;
    //}
}
