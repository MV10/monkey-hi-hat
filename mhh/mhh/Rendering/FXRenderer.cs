
using eyecandy;
using Microsoft.Extensions.Logging;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace mhh;

public class FXRenderer : IRenderer
{
    public bool IsValid { get; set; } = true;
    public string InvalidReason { get; set; } = string.Empty;
    public string Filename { get; private set; }
    public string Description { get; private set; }

    public GLResourceGroup OutputBuffers { get => FinalDrawbuffers; }
    private GLResourceGroup FinalDrawbuffers;
    
    public Vector2 Resolution { get => ViewportResolution; }
    private Vector2 ViewportResolution;

    public ConfigFile ConfigSource { get => Config.ConfigSource; }
    public FXConfig Config;

    public IRenderer PrimaryRenderer;

    public bool OutputIntercepted { set => IsOutputIntercepted = value; }
    private bool IsOutputIntercepted = false;

    private string DrawbufferOwnerName = RenderingHelper.MakeOwnerName("Drawbuffers");
    private string BackbufferOwnerName = RenderingHelper.MakeOwnerName("Backbuffers");
    private IReadOnlyList<GLResourceGroup> DrawbufferResources;
    private IReadOnlyList<GLResourceGroup> BackbufferResources;
    private List<MultipassDrawCall> ShaderPasses;
    private IReadOnlyList<GLImageTexture> Textures;

    private Dictionary<string, float> PrimaryFXUniforms;

    private string FXCrossfadeOwnerName = RenderingHelper.MakeOwnerName("Crossfading");
    private GLResourceGroup FXCrossfadeResources;
    private Shader FXCrossfadeShader;
    private IVertexSource FXCrossfadeVerts;
    private float FXCrossfadeDurationMS;

    private Stopwatch Clock = new();
    private float FrameCount = 0;
    private Random RNG = new();
    private float RandomRun;
    private Vector4 RandomRun4;

    public FXRenderer(FXConfig fxConfig, IRenderer primaryRenderer)
    {
        ViewportResolution = new(RenderingHelper.ClientSize.X, RenderingHelper.ClientSize.Y);

        Config = fxConfig;
        Filename = Path.GetFileNameWithoutExtension(Config.ConfigSource.Pathname);
        Description = fxConfig.Description;
        PrimaryRenderer = primaryRenderer;
        PrimaryFXUniforms = PrimaryRenderer.GetFXUniforms(Filename);

        // only calculates ViewportResolution when called from the constructor
        RenderingHelper.UseFXResolutionLimit = Config.ApplyPrimaryResolutionLimit;
        OnResize();

        try
        {
            var parser = new MultipassSectionParser(this, DrawbufferOwnerName, BackbufferOwnerName);
            if (!IsValid)
            {
                // don't dispose the currently active visualization if we can't use it here
                PrimaryRenderer = null;
                return;
            }

            // copy parser results, then destroy the parser
            ShaderPasses = parser.ShaderPasses;
            DrawbufferResources = parser.DrawbufferResources;
            BackbufferResources = parser.BackbufferResources;
            parser = null;

            // if primary doesn't have buffers, ShaderPass[0] needs to match the primary's resolution (assuming it differs from the FX buffer resolution)
            if (PrimaryRenderer is not null && PrimaryRenderer.OutputBuffers is null
                && (PrimaryRenderer.Resolution.X != ViewportResolution.X || PrimaryRenderer.Resolution.Y != ViewportResolution.Y))
            {
                RenderManager.ResourceManager.ResizeTexture(ShaderPasses[0].Drawbuffers, (int)PrimaryRenderer.Resolution.X, (int)PrimaryRenderer.Resolution.Y);
                if (ShaderPasses[0].Backbuffers is not null) RenderManager.ResourceManager.ResizeTexture(ShaderPasses[0].Backbuffers, (int)PrimaryRenderer.Resolution.X, (int)PrimaryRenderer.Resolution.Y);
            }

            // initialize the output buffer
            FinalDrawbuffers = ShaderPasses[ShaderPasses.Count - 1].Drawbuffers;

            // prep for crossfade unless the program is doing a larger crossfade (typically happens on instant-apply FX)
            if (Config.Crossfade && Program.AppWindow.Renderer.ActiveRenderer is not CrossfadeRenderer)
            {
                FXCrossfadeShader = Caching.InternalCrossfadeShader;
                FXCrossfadeVerts = new VertexQuad();
                FXCrossfadeVerts.Initialize(null, FXCrossfadeShader); // null is safe, fragquad has no viz/fx settings and crossfade doesn't support textures/videos
                FXCrossfadeResources = RenderManager.ResourceManager.CreateResourceGroups(FXCrossfadeOwnerName, 1, ViewportResolution)[0];
                FXCrossfadeDurationMS = Program.AppConfig.CrossfadeSeconds * 1000f;
            }

            Textures = RenderingHelper.GetTextures(DrawbufferOwnerName, Config.ConfigSource);
        }
        catch (ArgumentException ex)
        {
            IsValid = false;
            InvalidReason = ex.Message;
            // don't dispose the currently active visualization if we can't use it here
            PrimaryRenderer = null;
            return;
        }

        PrimaryRenderer.OutputIntercepted = true;

        RandomRun = (float)RNG.NextDouble();
        RandomRun4 = new((float)RNG.NextDouble(), (float)RNG.NextDouble(), (float)RNG.NextDouble(), (float)RNG.NextDouble());
    }

    public void PreRenderFrame()
    {
        RenderingHelper.UpdateVideoTextures(Textures);
    }

    public void RenderFrame(ScreenshotWriter screenshotHandler = null)
    {
        var timeUniform = TrueElapsedTime();

        // pass 0 is special handling for the primary renderer
        if (PrimaryRenderer is not null)
        {
            // if the primary doesn't own framebuffers, use buffer 0 owned by FXRenderer
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            if (PrimaryRenderer.OutputBuffers is null)
            {
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, ShaderPasses[0].Drawbuffers.FramebufferHandle);
                GL.Viewport(0, 0, (int)ViewportResolution.X, (int)ViewportResolution.Y);
            }
            else
            {
                GL.Viewport(0, 0, (int)PrimaryRenderer.Resolution.X, (int)PrimaryRenderer.Resolution.Y);
            }
            GL.Clear(ClearBufferMask.ColorBufferBit);
            PrimaryRenderer.RenderFrame();

            // if the primary has framebuffers, copy results to FXRenderer buffer 0 since other passes need it as input
            // and if the primary is switched to snapshot mode (rendering stops), that copy is read by future frames
            if(PrimaryRenderer.OutputBuffers is not null)
            {
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, ShaderPasses[0].Drawbuffers.FramebufferHandle);
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, PrimaryRenderer.OutputBuffers.FramebufferHandle);
                GL.BlitFramebuffer(
                    0, 0, (int)PrimaryRenderer.Resolution.X, (int)PrimaryRenderer.Resolution.Y,
                    0, 0, (int)ViewportResolution.X, (int)ViewportResolution.Y,
                    ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
            }
        }

        // skip(1) because pass 0 is handled above
        foreach (var pass in ShaderPasses.Skip(1))
        {
            pass.Shader.Use();
            pass.Shader.ResetUniforms();
            Program.AppWindow.Eyecandy.SetTextureUniforms(pass.Shader);
            RenderingHelper.SetGlobalUniforms(pass.Shader, Config.Uniforms, PrimaryFXUniforms);
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
        var lastPass = ShaderPasses[ShaderPasses.Count - 1];
        FinalDrawbuffers = lastPass.Drawbuffers;

        screenshotHandler?.SaveFramebuffer((int)ViewportResolution.X, (int)ViewportResolution.Y, FinalDrawbuffers.FramebufferHandle);

        // is FX crossfade active?
        float fadeLevel = (float)Clock.ElapsedMilliseconds / FXCrossfadeDurationMS;
        if (Config.Crossfade && FXCrossfadeShader is not null)
        {
            // Cached, only HostWindow.Dispose releases this
            if (fadeLevel >= 1f)
            {
                FXCrossfadeShader = null;
                FXCrossfadeVerts.Dispose();
                FXCrossfadeVerts = null;
            }
        }

        // execute the FX crossfade
        if (FXCrossfadeShader is not null)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FXCrossfadeResources.FramebufferHandle);
            GL.Viewport(0, 0, (int)ViewportResolution.X, (int)ViewportResolution.Y);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            FXCrossfadeShader.Use();
            FXCrossfadeShader.ResetUniforms();
            FXCrossfadeShader.SetUniform("fadeLevel", fadeLevel);
            FXCrossfadeShader.SetTexture("oldBuffer", ShaderPasses[0].Drawbuffers.TextureHandle, ShaderPasses[0].Drawbuffers.TextureUnit);
            FXCrossfadeShader.SetTexture("newBuffer", FinalDrawbuffers.TextureHandle, FinalDrawbuffers.TextureUnit);
            FXCrossfadeVerts.RenderFrame(FXCrossfadeShader);

            FinalDrawbuffers = FXCrossfadeResources;
        }

        // blit drawbuffer to OpenGL's backbuffer unless CrossfadeRenderer is intercepting
        // the final draw buffer (not the same as internal FX crossfade support)
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
        (ViewportResolution, _) = RenderingHelper.CalculateViewportResolution(Config.RenderResolutionLimit);

        // we own the primary now, make sure it gets resized, and apply any defined FXResolutionLimit
        PrimaryRenderer?.OnResize();

        // abort if the constructor called this, or if nothing changed
        if (ShaderPasses is null || oldResolution.X == ViewportResolution.X && oldResolution.Y == ViewportResolution.Y) return;

        // resize draw buffers, and resize/copy back buffers
        RenderManager.ResourceManager.ResizeTextures(DrawbufferOwnerName, ViewportResolution);
        if (BackbufferResources?.Count > 0) RenderManager.ResourceManager.ResizeTextures(BackbufferOwnerName, ViewportResolution, true);
        if (Config.Crossfade) RenderManager.ResourceManager.ResizeTextures(FXCrossfadeOwnerName, ViewportResolution);

        // if primary doesn't have buffers, ShaderPass[0] needs to match the primary's resolution (assuming it differs from the FX buffer resolution)
        if(PrimaryRenderer is not null && PrimaryRenderer.OutputBuffers is null
            && (PrimaryRenderer.Resolution.X != ViewportResolution.X || PrimaryRenderer.Resolution.Y != ViewportResolution.Y))
        {
            RenderManager.ResourceManager.ResizeTexture(ShaderPasses[0].Drawbuffers, (int)PrimaryRenderer.Resolution.X, (int)PrimaryRenderer.Resolution.Y);
            if (ShaderPasses[0].Backbuffers is not null) RenderManager.ResourceManager.ResizeTexture(ShaderPasses[0].Backbuffers, (int)PrimaryRenderer.Resolution.X, (int)PrimaryRenderer.Resolution.Y, true);
        }

        // re-bind the visualizers
        foreach (var pass in ShaderPasses)
        {
            pass.VertexSource?.BindBuffers(pass.Shader);
        }
        FXCrossfadeVerts?.BindBuffers(FXCrossfadeShader);
    }

    public void StartClock()
    {
        Clock.Start();
        PrimaryRenderer?.StartClock();
    }

    public void StopClock()
    {
        Clock.Stop();
        PrimaryRenderer?.StopClock();
    }

    public float TrueElapsedTime()
        => (float)Clock.Elapsed.TotalSeconds;

    public Dictionary<string, float> GetFXUniforms(string fxFilename)
        => new();

    public void Dispose()
    {
        if (IsDisposed) return;
        LogHelper.Logger?.LogTrace($"{GetType()}.Dispose() ----------------------------");

        RenderingHelper.UseFXResolutionLimit = false;

        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() primary visualization renderer");
        PrimaryRenderer?.Dispose();

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

        // this also deletes any allocated textures
        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() Drawbuffer Resources");
        RenderManager.ResourceManager.DestroyAllResources(DrawbufferOwnerName);

        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() Backbuffer Resources");
        RenderManager.ResourceManager.DestroyAllResources(BackbufferOwnerName);

        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() internal crossfade Resources");
        RenderManager.ResourceManager.DestroyAllResources(FXCrossfadeOwnerName);

        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() internal crossfade VertexSource");
        FXCrossfadeVerts?.Dispose();

        DrawbufferResources = null;
        BackbufferResources = null;
        FXCrossfadeResources = null;

        // Cached, only HostWindow.Dispose() actually destroys this
        FXCrossfadeShader = null;

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
