﻿
using Microsoft.Extensions.Logging;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System.Diagnostics;

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
    public string Filename { get; set; }

    public GLResourceGroup OutputBuffers { get => FinalDrawbuffers; }
    private GLResourceGroup FinalDrawbuffers;

    public Vector2 Resolution { get => ViewportResolution;  }
    private Vector2 ViewportResolution;

    public bool OutputIntercepted { set => IsOutputIntercepted = value; }
    private bool IsOutputIntercepted = false;

    public ConfigFile ConfigSource { get => Config.ConfigSource; }
    public VisualizerConfig Config;

    private Guid DrawbufferOwnerName = Guid.NewGuid();
    private Guid BackbufferOwnerName = Guid.NewGuid();
    private IReadOnlyList<GLResourceGroup> DrawbufferResources;
    private IReadOnlyList<GLResourceGroup> BackbufferResources;
    private List<MultipassDrawCall> ShaderPasses;

    private Stopwatch Clock = new();
    private float FrameCount = 0;

    public MultipassRenderer(VisualizerConfig visualizerConfig)
    {
        ViewportResolution = new(Program.AppWindow.ClientSize.X, Program.AppWindow.ClientSize.Y);

        Config = visualizerConfig;
        Filename = Path.GetFileNameWithoutExtension(Config.ConfigSource.Pathname);

        // only calculates ViewportResolution when called from the constructor
        OnResize();

        try
        {
            var parser = new MultipassSectionParser(this, DrawbufferOwnerName, BackbufferOwnerName);
            if(IsValid)
            {
                // copy references to the results
                ShaderPasses = parser.ShaderPasses;
                DrawbufferResources = parser.DrawbufferResources;
                BackbufferResources = parser.BackbufferResources;

                // initialize the output buffer info
                FinalDrawbuffers = ShaderPasses[ShaderPasses.Count - 1].Drawbuffers;
            }
            parser = null;
        }
        catch (ArgumentException ex)
        {
            IsValid = false;
            InvalidReason = ex.Message;
        }
    }

    public void RenderFrame()
    {
        var timeUniform = ElapsedTime();

        foreach (var pass in ShaderPasses)
        {
            Program.AppWindow.Eyecandy.SetTextureUniforms(pass.Shader);
            pass.Shader.SetUniform("resolution", ViewportResolution);
            pass.Shader.SetUniform("time", timeUniform);
            pass.Shader.SetUniform("frame", FrameCount);
            foreach (var index in pass.InputFrontbufferResources)
            {
                var resource = DrawbufferResources[index];
                pass.Shader.SetTexture(resource.UniformName, resource.TextureHandle, resource.TextureUnit);
            }
            foreach (var index in pass.InputBackbufferResources)
            {
                var resource = BackbufferResources[index];
                pass.Shader.SetTexture(resource.UniformName, resource.TextureHandle, resource.TextureUnit);
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, pass.Drawbuffers.FramebufferHandle);
            GL.Viewport(0, 0, (int)ViewportResolution.X, (int)ViewportResolution.Y);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            pass.Visualizer.RenderFrame(pass.Shader);
        }

        // store this now so that crossfade can find the output buffer (it may have
        // changed from the previous frame if that pass has a front/back buffer swap)
        FinalDrawbuffers = ShaderPasses[ShaderPasses.Count - 1].Drawbuffers;

        // blit drawbuffer to OpenGL's backbuffer unless Crossfade or FXRenderer is intercepting the final draw buffer
        if (!IsOutputIntercepted)
        {
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, FinalDrawbuffers.FramebufferHandle);
            GL.BlitFramebuffer(
                0, 0, (int)ViewportResolution.X, (int)ViewportResolution.Y,
                0, 0, Program.AppWindow.ClientSize.X, Program.AppWindow.ClientSize.Y,
                ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        // rendering completed; swap front/back buffers
        foreach (var pass in ShaderPasses)
        {
            if (pass.Backbuffers is not null)
                (pass.Drawbuffers, pass.Backbuffers) = (pass.Backbuffers, pass.Drawbuffers);
        }

        FrameCount++;
    }

    public void OnResize()
    {
        var oldResolution = ViewportResolution;
        (ViewportResolution, _) = RenderingHelper.CalculateViewportResolution(Config.RenderResolutionLimit);

        // abort if the constructor called this, or if nothing changed
        if (ShaderPasses is null || oldResolution.X == ViewportResolution.X && oldResolution.Y == ViewportResolution.Y) return;

        // resize draw buffers, and resize/copy back buffers
        RenderManager.ResourceManager.ResizeTextures(DrawbufferOwnerName, ViewportResolution);
        if (BackbufferResources.Count > 0) RenderManager.ResourceManager.ResizeTextures(BackbufferOwnerName, ViewportResolution, oldResolution);

        foreach (var pass in ShaderPasses)
        {
            pass.Visualizer.BindBuffers(pass.Shader);
        }
    }

    public void StartClock()
        => Clock.Start();

    public void StopClock()
        => Clock.Stop();

    public float ElapsedTime()
        => (float)Clock.Elapsed.TotalSeconds;

    public void Dispose()
    {
        if (IsDisposed) return;
        LogHelper.Logger?.LogTrace($"{GetType()}.Dispose() ----------------------------");

        if (ShaderPasses is not null)
        {
            foreach (var pass in ShaderPasses)
            {
                LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() shader pass Visualizer");
                pass.Visualizer?.Dispose();

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
