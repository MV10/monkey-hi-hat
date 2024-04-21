
using Microsoft.Extensions.Logging;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace mhh;

public class SimpleRenderer : IRenderer
{
    public bool IsValid { get; set; } = true;
    public string InvalidReason { get; set; } = string.Empty;
    public string Filename { get; private set; }
    public string Description { get; private set; }

    public GLResourceGroup OutputBuffers { get => FinalDrawbuffers; }
    private GLResourceGroup FinalDrawbuffers;

    public Vector2 Resolution { get => ViewportResolution; }
    private Vector2 ViewportResolution;

    public bool OutputIntercepted { set => IsOutputIntercepted = value; }
    private bool IsOutputIntercepted = false;

    public ConfigFile ConfigSource { get => Config.ConfigSource; }
    public VisualizerConfig Config;

    public string OwnerName = RenderingHelper.MakeOwnerName("Drawbuffers");
    public IVertexSource VertexSource;
    public CachedShader Shader;

    private bool FullResolutionViewport;
    private Stopwatch Clock = new();
    private float ClockOffset = 0;
    private float FrameCount = 0;
    private Random RNG = new();
    private float RandomRun;
    private Vector4 RandomRun4;

    public SimpleRenderer(VisualizerConfig visualizerConfig)
    {
        ViewportResolution = new(RenderingHelper.ClientSize.X, RenderingHelper.ClientSize.Y);

        Config = visualizerConfig;
        Filename = Path.GetFileNameWithoutExtension(Config.ConfigSource.Pathname);
        Description = visualizerConfig.Description;
        if (Config.RandomTimeOffset != 0) ClockOffset = RNG.Next(0, Math.Abs(Config.RandomTimeOffset) + 1) * Math.Sign(Config.RandomTimeOffset);

        Shader = RenderingHelper.GetVisualizerShader(this, visualizerConfig);
        if (!IsValid) return;

        VertexSource = RenderingHelper.GetVertexSource(this, visualizerConfig);
        if (!IsValid) return;

        VertexSource.Initialize(Config, Shader);

        OnResize();

        RandomRun = (float)RNG.NextDouble();
        RandomRun4 = new((float)RNG.NextDouble(), (float)RNG.NextDouble(), (float)RNG.NextDouble(), (float)RNG.NextDouble());
    }

    public void RenderFrame(ScreenshotWriter screenshotHandler = null)
    {
        var timeUniform = TrueElapsedTime() + ClockOffset;

        Shader.Use();
        Shader.ResetUniforms();
        Program.AppWindow.Eyecandy.SetTextureUniforms(Shader);
        RenderingHelper.SetGlobalUniforms(Shader, Config.Uniforms);
        Shader.SetUniform("resolution", ViewportResolution);
        Shader.SetUniform("time", timeUniform);
        Shader.SetUniform("frame", FrameCount);
        Shader.SetUniform("randomrun", RandomRun);
        Shader.SetUniform("randomrun4", RandomRun4);

        if (FinalDrawbuffers is not null)
        {
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, FinalDrawbuffers.FramebufferHandle);
            GL.Viewport(0, 0, (int)ViewportResolution.X, (int)ViewportResolution.Y);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            VertexSource.RenderFrame(Shader);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                screenshotHandler?.SaveFramebuffer((int)ViewportResolution.X, (int)ViewportResolution.Y, FinalDrawbuffers.FramebufferHandle);

            // blit drawbuffer to OpenGL's backbuffer unless Crossfade or FXRenderer is intercepting the final draw buffer
            if (!IsOutputIntercepted)
            {
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, FinalDrawbuffers.FramebufferHandle);
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
                GL.BlitFramebuffer(
                    0, 0, (int)ViewportResolution.X, (int)ViewportResolution.Y,
                    0, 0, RenderingHelper.ClientSize.X, RenderingHelper.ClientSize.Y,
                    ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            }
        }
        else
        {
            GL.Viewport(0, 0, (int)ViewportResolution.X, (int)ViewportResolution.Y);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            VertexSource.RenderFrame(Shader);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                screenshotHandler?.SaveFramebuffer((int)ViewportResolution.X, (int)ViewportResolution.Y);
        }

        FrameCount++;
    }

    public void OnResize()
    {
        var oldResolution = ViewportResolution;
        (ViewportResolution, FullResolutionViewport) = RenderingHelper.CalculateViewportResolution(Config.RenderResolutionLimit, Config.FXResolutionLimit);

        // abort if nothing changed
        if (oldResolution.X == ViewportResolution.X && oldResolution.Y == ViewportResolution.Y) return;

        if(FinalDrawbuffers is not null)
        {
            if(FullResolutionViewport)
            {
                FinalDrawbuffers = null;
                RenderManager.ResourceManager.DestroyAllResources(OwnerName);
            }
            else
            {
                RenderManager.ResourceManager.ResizeTextures(OwnerName, ViewportResolution);
            }
        }
        else
        {
            if (!FullResolutionViewport)
            {
                FinalDrawbuffers = RenderManager.ResourceManager.CreateResourceGroups(OwnerName, 1, ViewportResolution)[0];
            }
        }

        VertexSource.BindBuffers(Shader);
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
        LogHelper.Logger?.LogTrace($"{GetType()}.Dispose() ----------------------------");

        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() VertexSource");
        VertexSource?.Dispose();
        VertexSource = null;

        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() Uncached Shader");
        RenderingHelper.DisposeUncachedShader(Shader);
        Shader = null;

        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() Resources");
        RenderManager.ResourceManager.DestroyAllResources(OwnerName);
        FinalDrawbuffers = null;

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
