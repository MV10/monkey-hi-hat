
using Microsoft.Extensions.Logging;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace mhh;

public class SimpleRenderer : IRenderer
{
    public bool IsValid { get; set; } = true;
    public string InvalidReason { get; set; } = string.Empty;
    public string Filename { get; private set; }
    public string Description { get; private set; }

    public GLResourceGroup OutputBuffers { get => FinalDrawbuffers; }
    private GLResourceGroup FinalDrawbuffers;
    private IReadOnlyList<GLImageTexture> Textures;

    private VideoMediaProcessor VideoProcessor;

    public Vector2 Resolution { get => OutputResolution; }
    private Vector2 OutputResolution;

    public bool OutputIntercepted { set => IsOutputIntercepted = value; }
    private bool IsOutputIntercepted = false;

    public ConfigFile ConfigSource { get => Config.ConfigSource; }
    public VisualizerConfig Config;

    public string OwnerName = RenderingHelper.MakeOwnerName("Drawbuffers");
    public IVertexSource VertexSource;
    public CachedShader Shader;

    private bool isViewportResolution;
    private Stopwatch Clock = new();
    private float ClockOffset = 0;
    private float FrameCount = 0;
    private Random RNG = new();
    private float RandomRun;
    private Vector4 RandomRun4;

    private static readonly ILogger Logger = LogHelper.CreateLogger(nameof(SimpleRenderer));

    public SimpleRenderer(VisualizerConfig visualizerConfig)
    {
        Config = visualizerConfig;
        Filename = Path.GetFileNameWithoutExtension(Config.ConfigSource.Pathname);

        Logger?.LogTrace($"Constructor {Filename}");

        OutputResolution = new(RenderingHelper.ClientSize.X, RenderingHelper.ClientSize.Y);

        Description = visualizerConfig.Description;
        if (Config.RandomTimeOffset != 0) ClockOffset = RNG.Next(0, Math.Abs(Config.RandomTimeOffset) + 1) * Math.Sign(Config.RandomTimeOffset);

        Shader = RenderingHelper.GetVisualizerShader(this, visualizerConfig);
        if (!IsValid) return;

        VertexSource = RenderingHelper.GetVertexSource(this, visualizerConfig);
        if (!IsValid) return;

        VertexSource.Initialize(Config, Shader);

        Logger?.LogTrace("Parsing texture declarations");
        Textures = RenderingHelper.GetTextures(OwnerName, Config.ConfigSource);
        if(Textures?.Any(t => t.Loaded && t.VideoData is not null) ?? false) VideoProcessor = new(Textures);

        OnResize();

        RandomRun = (float)RNG.NextDouble();
        RandomRun4 = new((float)RNG.NextDouble(), (float)RNG.NextDouble(), (float)RNG.NextDouble(), (float)RNG.NextDouble());
    }

    public void PreRenderFrame()
    {
        VideoProcessor?.UpdateTextures();
    }

    public void RenderFrame(ScreenshotWriter screenshotHandler = null)
    {
        var timeUniform = TrueElapsedTime() + ClockOffset;

        Shader.Use();
        Shader.ResetUniforms();
        Program.AppWindow.Eyecandy.SetTextureUniforms(Shader);
        RenderingHelper.SetGlobalUniforms(Shader, Config.Uniforms);
        RenderingHelper.SetTextureUniforms(Textures, Shader);
        Shader.SetUniform("resolution", OutputResolution);
        Shader.SetUniform("time", timeUniform);
        Shader.SetUniform("frame", FrameCount);
        Shader.SetUniform("randomrun", RandomRun);
        Shader.SetUniform("randomrun4", RandomRun4);

        if (FinalDrawbuffers is not null)
        {
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, FinalDrawbuffers.FramebufferHandle);
            GL.Viewport(0, 0, (int)OutputResolution.X, (int)OutputResolution.Y);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            VertexSource.RenderFrame(Shader);

            screenshotHandler?.SaveFramebuffer((int)OutputResolution.X, (int)OutputResolution.Y, FinalDrawbuffers.FramebufferHandle);

            // blit drawbuffer to OpenGL's backbuffer unless Crossfade or FXRenderer is intercepting the final draw buffer
            if (!IsOutputIntercepted)
            {
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, FinalDrawbuffers.FramebufferHandle);
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
                GL.BlitFramebuffer(
                    0, 0, (int)OutputResolution.X, (int)OutputResolution.Y,
                    0, 0, RenderingHelper.ClientSize.X, RenderingHelper.ClientSize.Y,
                    ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            }
        }
        else
        {
            GL.Viewport(0, 0, (int)OutputResolution.X, (int)OutputResolution.Y);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            VertexSource.RenderFrame(Shader);

            screenshotHandler?.SaveFramebuffer((int)OutputResolution.X, (int)OutputResolution.Y);
        }

        FrameCount++;
    }

    public void OnResize()
    {
        Logger?.LogTrace($"OnResize {Filename}");

        var oldResolution = OutputResolution;
        (OutputResolution, isViewportResolution) = RenderingHelper.CalculateOutputResolution(Config.RenderResolutionLimit, Config.FXResolutionLimit);

        // abort if nothing changed
        if (oldResolution.X == OutputResolution.X && oldResolution.Y == OutputResolution.Y) return;

        if(FinalDrawbuffers is not null)
        {
            if(isViewportResolution)
            {
                FinalDrawbuffers = null;
                RenderManager.ResourceManager.DestroyAllResources(OwnerName, keepContentTextures:true);
            }
            else
            {
                RenderManager.ResourceManager.ResizeFramebufferTextures(OwnerName, OutputResolution);
            }
        }
        else
        {
            if (!isViewportResolution)
            {
                FinalDrawbuffers = RenderManager.ResourceManager.CreateResourceGroups(OwnerName, 1, OutputResolution)[0];
            }
        }

        VertexSource.BindBuffers(Shader);
    }

    public GLImageTexture GetStreamingTexture()
    {
        return Textures.FirstOrDefault(t => t.ResizeMode != StreamingResizeContentMode.NotStreaming);
    }

    public void StartClock()
        => Clock.Start();

    public void StopClock()
        => Clock.Stop();

    public float TrueElapsedTime()
        => (float)Clock.Elapsed.TotalSeconds;

    public Dictionary<string, float> GetFXUniforms(string fxFilename)
        => Config.FXUniforms.ContainsKey(fxFilename)
        ? Config.FXUniforms[fxFilename]
        : new();

    public void Dispose()
    {
        if (IsDisposed) return;
        Logger?.LogTrace($"Disposing {Filename}");

        VideoProcessor?.Dispose();
        VideoProcessor = null;

        Program.AppWindow.StreamReceiver?.TryDetachTexture(Textures);

        VertexSource?.Dispose();
        VertexSource = null;

        RenderingHelper.DisposeUncachedShader(Shader);
        Shader = null;

        RenderManager.ResourceManager.DestroyAllResources(OwnerName);
        FinalDrawbuffers = null;

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
