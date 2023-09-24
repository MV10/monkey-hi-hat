
using Microsoft.Extensions.Logging;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace mhh;

public class SimpleRenderer : IRenderer
{
    public bool IsValid { get; set; } = true;
    public string InvalidReason { get; set; } = string.Empty;
    public string Filename { get; set; }

    public GLResourceGroup OutputBuffers { get => FinalDrawbuffers; }
    private GLResourceGroup FinalDrawbuffers;

    public Vector2 Resolution { get => ViewportResolution; }
    private Vector2 ViewportResolution;

    public bool OutputIntercepted { set => IsOutputIntercepted = value; }
    private bool IsOutputIntercepted = false;

    public ConfigFile ConfigSource { get => Config.ConfigSource; }
    public VisualizerConfig Config;

    public string OwnerName = RenderingHelper.MakeOwnerName("Drawbuffers");
    public IVisualizer Visualizer;
    public CachedShader Shader;

    private bool FullResolutionViewport;
    private Stopwatch Clock = new();
    private float ClockOffset = 0;
    private float FrameCount = 0;
    private Random RNG = new();

    public SimpleRenderer(VisualizerConfig visualizerConfig)
    {
        ViewportResolution = new(RenderingHelper.ClientSize.X, RenderingHelper.ClientSize.Y);

        Config = visualizerConfig;
        Filename = Path.GetFileNameWithoutExtension(Config.ConfigSource.Pathname);
        if (Config.RandomTimeOffset != 0) ClockOffset = RNG.Next(0, Math.Abs(Config.RandomTimeOffset) + 1) * Math.Sign(Config.RandomTimeOffset);

        Shader = RenderingHelper.GetShader(this, visualizerConfig);
        if (!IsValid) return;

        Visualizer = RenderingHelper.GetVisualizer(this, visualizerConfig);
        if (!IsValid) return;

        Visualizer.Initialize(Config, Shader);
        OnResize();
    }

    public void RenderFrame()
    {
        var timeUniform = TrueElapsedTime() + ClockOffset;

        Program.AppWindow.Eyecandy.SetTextureUniforms(Shader);
        Shader.SetUniform("resolution", ViewportResolution);
        Shader.SetUniform("time", timeUniform);
        Shader.SetUniform("frame", FrameCount);

        if(FinalDrawbuffers is not null)
        {
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, FinalDrawbuffers.FramebufferHandle);
            GL.Viewport(0, 0, (int)ViewportResolution.X, (int)ViewportResolution.Y);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            Visualizer.RenderFrame(Shader);

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
            Visualizer.RenderFrame(Shader);
        }

        FrameCount++;
    }

    public void OnResize()
    {
        var oldResolution = ViewportResolution;
        (ViewportResolution, FullResolutionViewport) = RenderingHelper.CalculateViewportResolution(Config.RenderResolutionLimit);
        
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

        Visualizer.BindBuffers(Shader);
    }

    public void StartClock()
        => Clock.Start();

    public void StopClock()
        => Clock.Stop();

    public float TrueElapsedTime()
        => (float)Clock.Elapsed.TotalSeconds;

    public void Dispose()
    {
        if (IsDisposed) return;
        LogHelper.Logger?.LogTrace($"{GetType()}.Dispose() ----------------------------");

        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() Visualizer");
        Visualizer?.Dispose();
        Visualizer = null;

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
