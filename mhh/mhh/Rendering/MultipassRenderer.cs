
using eyecandy;
using Microsoft.Extensions.Logging;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace mhh;

// Multipass also allows for double-buffering, which means some passes
// have a frontbuffer and a backbuffer, where the backbuffer contains
// the final output from the previous frame. This is handled by
// allocating two sets of GLResources, those for the normal multipass
// buffers, and those for the backbuffers, which are then swapped in
// the DrawCall objects that own double-buffers.

public class MultipassRenderer : IRenderer
{
    public bool IsValid { get; set; } = true;
    public string InvalidReason { get; set; } = string.Empty;
    public string Filename { get; private set; }
    public string Description { get; private set; }

    public GLResourceGroup OutputBuffers { get => FinalDrawbuffers; }
    private GLResourceGroup FinalDrawbuffers;

    public Vector2 Resolution { get => ViewportResolution;  }
    private Vector2 ViewportResolution;

    public bool OutputIntercepted { set => IsOutputIntercepted = value; }
    private bool IsOutputIntercepted = false;

    public ConfigFile ConfigSource { get => Config.ConfigSource; }
    public VisualizerConfig Config;

    private string DrawbufferOwnerName = RenderingHelper.MakeOwnerName("Drawbuffers");
    private string BackbufferOwnerName = RenderingHelper.MakeOwnerName("Backbuffers");
    private IReadOnlyList<GLResourceGroup> DrawbufferResources;
    private IReadOnlyList<GLResourceGroup> BackbufferResources;
    private List<MultipassDrawCall> ShaderPasses;
    private IReadOnlyList<GLImageTexture> Textures;
    private VideoMediaProcessor VideoProcessor;

    private Stopwatch Clock = new();
    private float ClockOffset = 0;
    private float FrameCount = 0;
    private Random RNG = new();
    private float RandomRun;
    private Vector4 RandomRun4;

    public MultipassRenderer(VisualizerConfig visualizerConfig)
    {
        ViewportResolution = new(RenderingHelper.ClientSize.X, RenderingHelper.ClientSize.Y);

        Config = visualizerConfig;
        Filename = Path.GetFileNameWithoutExtension(Config.ConfigSource.Pathname);
        Description = visualizerConfig.Description;
        if (Config.RandomTimeOffset != 0) ClockOffset = RNG.Next(0, Math.Abs(Config.RandomTimeOffset) + 1) * Math.Sign(Config.RandomTimeOffset);

        // only calculates ViewportResolution when called from the constructor
        OnResize();

        try
        {
            var parser = new MultipassSectionParser(this, DrawbufferOwnerName, BackbufferOwnerName);
            if (!IsValid) return;

            // copy references to the results
            ShaderPasses = parser.ShaderPasses;
            DrawbufferResources = parser.DrawbufferResources;
            BackbufferResources = parser.BackbufferResources;
            parser = null;

            // initialize the output buffer info
            FinalDrawbuffers = ShaderPasses[ShaderPasses.Count - 1].Drawbuffers;

            Textures = RenderingHelper.GetTextures(DrawbufferOwnerName, Config.ConfigSource);
            if (Textures?.Any(t => t.Loaded && t.VideoData is not null) ?? false) VideoProcessor = new(Textures);
        }
        catch (ArgumentException ex)
        {
            IsValid = false;
            InvalidReason = ex.Message;
            return;
        }

        RandomRun = (float)RNG.NextDouble();
        RandomRun4 = new((float)RNG.NextDouble(), (float)RNG.NextDouble(), (float)RNG.NextDouble(), (float)RNG.NextDouble());
    }

    public void PreRenderFrame()
    {
        VideoProcessor?.UpdateTextures(); // synchronous version
        //VideoProcessor?.BeginProcessing(); // async version
    }

    public void RenderFrame(ScreenshotWriter screenshotHandler = null)
    {
        var timeUniform = TrueElapsedTime() + ClockOffset;

        foreach (var pass in ShaderPasses)
        {
            pass.Shader.Use();
            pass.Shader.ResetUniforms();
            Program.AppWindow.Eyecandy.SetTextureUniforms(pass.Shader);
            RenderingHelper.SetGlobalUniforms(pass.Shader, Config.Uniforms, pass.Uniforms);
            RenderingHelper.SetTextureUniforms(Textures, pass.Shader);
            pass.Shader.SetUniform("resolution", ViewportResolution);
            pass.Shader.SetUniform("time", timeUniform);
            pass.Shader.SetUniform("frame", FrameCount);
            pass.Shader.SetUniform("randomrun", RandomRun);
            pass.Shader.SetUniform("randomrun4", RandomRun4);

            foreach (var index in pass.InputsDrawbuffers)
            {
                var resource = ShaderPasses[index].Drawbuffers;
                pass.Shader.SetTexture(resource.UniformName, resource.TextureHandle, resource.TextureUnit);
            }

            foreach (var index in pass.InputsBackbuffers)
            {
                var resource = ShaderPasses[index].Backbuffers;
                pass.Shader.SetTexture(resource.UniformName, resource.TextureHandle, resource.TextureUnit);
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, pass.Drawbuffers.FramebufferHandle);
            GL.Viewport(0, 0, (int)ViewportResolution.X, (int)ViewportResolution.Y);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            pass.VertexSource.RenderFrame(pass.Shader);
        }

        // store this now so that crossfade can find the output buffer (it may have
        // changed from the previous frame if that pass has a front/back buffer swap)
        FinalDrawbuffers = ShaderPasses[ShaderPasses.Count - 1].Drawbuffers;

        screenshotHandler?.SaveFramebuffer((int)ViewportResolution.X, (int)ViewportResolution.Y, FinalDrawbuffers.FramebufferHandle);

        // blit drawbuffer to OpenGL's backbuffer unless Crossfade or FXRenderer is intercepting the final draw buffer
        if (!IsOutputIntercepted)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, FinalDrawbuffers.FramebufferHandle);
            GL.BlitFramebuffer(
                0, 0, (int)ViewportResolution.X, (int)ViewportResolution.Y,
                0, 0, RenderingHelper.ClientSize.X, RenderingHelper.ClientSize.Y,
                ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        // rendering completed; swap front/back buffers
        foreach (var pass in ShaderPasses)
        {
            if (pass.Backbuffers is not null)
            {
                (pass.Drawbuffers, pass.Backbuffers) = (pass.Backbuffers, pass.Drawbuffers);
                (pass.Drawbuffers.UniformName, pass.Backbuffers.UniformName) = (pass.Backbuffers.UniformName, pass.Drawbuffers.UniformName);
            }
        }

        FrameCount++;
    }

    public void OnResize()
    {
        var oldResolution = ViewportResolution;
        (ViewportResolution, _) = RenderingHelper.CalculateViewportResolution(Config.RenderResolutionLimit, Config.FXResolutionLimit);

        // abort if the constructor called this, or if nothing changed
        if (ShaderPasses is null || oldResolution.X == ViewportResolution.X && oldResolution.Y == ViewportResolution.Y) return;

        // resize draw buffers, and resize/copy back buffers
        RenderManager.ResourceManager.ResizeTextures(DrawbufferOwnerName, ViewportResolution);
        if (BackbufferResources?.Count > 0) RenderManager.ResourceManager.ResizeTextures(BackbufferOwnerName, ViewportResolution, true);

        foreach (var pass in ShaderPasses)
        {
            pass.VertexSource.BindBuffers(pass.Shader);
        }
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

        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() VideoProcessor");
        VideoProcessor?.Dispose();
        VideoProcessor = null;

        if (ShaderPasses is not null)
        {
            foreach (var pass in ShaderPasses)
            {
                LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() shader pass VertexSource");
                pass.VertexSource?.Dispose();

                LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() shader pass Uncached Shader");
                RenderingHelper.DisposeUncachedShader(pass.Shader);
            }
            ShaderPasses = null;
        }

        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() shader pass Drawbuffer Resources");
        RenderManager.ResourceManager.DestroyAllResources(DrawbufferOwnerName);

        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() shader pass Backbuffer Resources");
        RenderManager.ResourceManager.DestroyAllResources(BackbufferOwnerName);

        DrawbufferResources = null;
        BackbufferResources = null;

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
