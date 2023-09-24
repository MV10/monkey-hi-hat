
using eyecandy;
using Microsoft.Extensions.Logging;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace mhh;

public class FXRenderer : IRenderer
{
    public bool IsValid { get; set; } = true;
    public string InvalidReason { get; set; } = string.Empty;
    public string Filename { get; set; }

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

    private string CrossfadeOwnerName = RenderingHelper.MakeOwnerName("Crossfading");
    private GLResourceGroup CrossfadeResources;
    private Shader CrossfadeShader;
    private IVertexSource CrossfadeVerts;
    private float DurationMS;

    private Stopwatch Clock = new();
    private float FrameCount = 0;
    private float snapClockNextSecond;
    private int snapClockPercent = 0;
    private Random RNG = new();

    public FXRenderer(FXConfig fxConfig, IRenderer primaryRenderer)
    {
        ViewportResolution = new(RenderingHelper.ClientSize.X, RenderingHelper.ClientSize.Y);

        Config = fxConfig;
        Filename = Path.GetFileNameWithoutExtension(Config.ConfigSource.Pathname);
        PrimaryRenderer = primaryRenderer;

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

            // initialize the output buffer
            FinalDrawbuffers = ShaderPasses[ShaderPasses.Count - 1].Drawbuffers;

            // prep for crossfade
            if (Config.Crossfade)
            {
                CrossfadeShader = Caching.CrossfadeShader;
                CrossfadeVerts = new VertexQuad();
                CrossfadeVerts.Initialize(null, CrossfadeShader); // fragquad doesn't have settings, so null is safe
                CrossfadeResources = RenderManager.ResourceManager.CreateResourceGroups(CrossfadeOwnerName, 1, ViewportResolution)[0];
                DurationMS = Program.AppConfig.CrossfadeSeconds * 1000f;
            }

            Textures = RenderingHelper.GetTextures(DrawbufferOwnerName, Config.ConfigSource);

            parser = null;
        }
        catch (ArgumentException ex)
        {
            IsValid = false;
            InvalidReason = ex.Message;
            return;
        }

        PrimaryRenderer.OutputIntercepted = true;
        snapClockNextSecond = PrimaryRenderer.TrueElapsedTime() + 1f;
    }

    public void RenderFrame()
    {
        var timeUniform = TrueElapsedTime();

        // pass 0 is special handling as either the primary renderer or a snapshot
        if (PrimaryRenderer is not null)
        {
            // if the primary doesn't own framebuffers, use buffer 0 owned by FXRenderer
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            if(PrimaryRenderer.OutputBuffers is null) GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, ShaderPasses[0].Drawbuffers.FramebufferHandle);
            GL.Viewport(0, 0, (int)PrimaryRenderer.Resolution.X, (int)PrimaryRenderer.Resolution.Y);
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
            Program.AppWindow.Eyecandy.SetTextureUniforms(pass.Shader);
            RenderingHelper.SetGlobalUniforms(pass.Shader);
            RenderingHelper.SetTextureUniforms(Textures, pass.Shader);
            pass.Shader.SetUniform("resolution", ViewportResolution);
            pass.Shader.SetUniform("time", timeUniform);
            pass.Shader.SetUniform("frame", FrameCount);
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

        // is FX crossfade active?
        float fadeLevel = (float)Clock.ElapsedMilliseconds / DurationMS;
        if (Config.Crossfade && CrossfadeShader is not null)
        {
            // Cached, only HostWindow.Dispose releases this
            if (fadeLevel >= 1f)
            {
                CrossfadeShader = null;
                CrossfadeVerts.Dispose();
                CrossfadeVerts = null;
            }
        }

        // execute the FX crossfade
        if (CrossfadeShader is not null)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, CrossfadeResources.FramebufferHandle);
            GL.Viewport(0, 0, (int)ViewportResolution.X, (int)ViewportResolution.Y);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            CrossfadeShader.Use();
            CrossfadeShader.SetUniform("fadeLevel", fadeLevel);
            CrossfadeShader.SetTexture("oldBuffer", ShaderPasses[0].Drawbuffers.TextureHandle, ShaderPasses[0].Drawbuffers.TextureUnit);
            CrossfadeShader.SetTexture("newBuffer", FinalDrawbuffers.TextureHandle, FinalDrawbuffers.TextureUnit);
            CrossfadeVerts.RenderFrame(CrossfadeShader);

            FinalDrawbuffers = CrossfadeResources;
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

        // in SnapClock mode, each passing second adds another 10% chance to switch to Snapshot
        // mode and freeze the primary renderer (thus 11 seconds is 100%)
        if (PrimaryRenderer is not null)
        {
            if(Config.PrimaryDrawMode == FXPrimaryDrawMode.SnapClock)
            {
                if(PrimaryRenderer.TrueElapsedTime() >= snapClockNextSecond)
                {
                    snapClockNextSecond = PrimaryRenderer.TrueElapsedTime() + 1f;
                    snapClockPercent += 10;
                    if (RNG.Next(1, 101) <= snapClockPercent)
                    {
                        PrimaryRenderer.Dispose();
                        PrimaryRenderer = null;
                    }
                }
            }

            // if we're in Snapshot mode, the primary renderer stops running and the current
            // buffer 0 (and backbuffer A) contents are frozen for the duration of execution
            if (Config.PrimaryDrawMode == FXPrimaryDrawMode.Snapshot)
            {
                PrimaryRenderer.Dispose();
                PrimaryRenderer = null;
            }
        }

        FrameCount++;
    }

    public void OnResize()
    {
        var oldResolution = ViewportResolution;
        (ViewportResolution, _) = RenderingHelper.CalculateViewportResolution(Config.RenderResolutionLimit);

        // abort if the constructor called this, or if nothing changed
        if (ShaderPasses is null || oldResolution.X == ViewportResolution.X && oldResolution.Y == ViewportResolution.Y) return;

        // we own the primary now, make sure it gets resized (if it still exists)
        PrimaryRenderer?.OnResize();

        // resize draw buffers, and resize/copy back buffers
        RenderManager.ResourceManager.ResizeTextures(DrawbufferOwnerName, ViewportResolution);
        if (BackbufferResources?.Count > 0) RenderManager.ResourceManager.ResizeTextures(BackbufferOwnerName, ViewportResolution, oldResolution);
        if (Config.Crossfade) RenderManager.ResourceManager.ResizeTextures(CrossfadeOwnerName, ViewportResolution);

        // re-bind the visualizers
        foreach (var pass in ShaderPasses)
        {
            pass.VertexSource?.BindBuffers(pass.Shader);
        }
        CrossfadeVerts?.BindBuffers(CrossfadeShader);
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
        RenderManager.ResourceManager.DestroyAllResources(CrossfadeOwnerName);

        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() internal crossfade VertexSource");
        CrossfadeVerts?.Dispose();

        DrawbufferResources = null;
        BackbufferResources = null;
        CrossfadeResources = null;

        // Cached, only HostWindow.Dispose() actually destroys this
        CrossfadeShader = null;

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
