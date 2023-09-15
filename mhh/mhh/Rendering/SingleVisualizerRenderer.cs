﻿
using mhh.Utils;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace mhh;

public class SingleVisualizerRenderer : IRenderer
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

    public Guid OwnerName = Guid.NewGuid();
    public VisualizerConfig Config;
    public IVisualizer Visualizer;
    public CachedShader Shader;

    private bool FullResolutionViewport;
    private Stopwatch Clock = new();
    private float FrameCount = 0;

    public SingleVisualizerRenderer(VisualizerConfig visualizerConfig)
    {
        ViewportResolution = new(Program.AppWindow.ClientSize.X, Program.AppWindow.ClientSize.Y);

        Config = visualizerConfig;
        Filename = Path.GetFileNameWithoutExtension(Config.ConfigSource.Pathname);

        Shader = RenderingHelper.GetShader(this, visualizerConfig);
        if (!IsValid) return;

        Visualizer = RenderingHelper.GetVisualizer(this, visualizerConfig);
        if (!IsValid) return;

        Visualizer.Initialize(Config, Shader);
        OnResize();
    }

    public void RenderFrame()
    {
        Program.AppWindow.Eyecandy.SetTextureUniforms(Shader);
        Shader.SetUniform("resolution", ViewportResolution);
        Shader.SetUniform("time", ElapsedTime());
        Shader.SetUniform("frame", FrameCount);

        if(FinalDrawbuffers is not null)
        {
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, FinalDrawbuffers.FramebufferHandle);
            GL.Viewport(0, 0, (int)ViewportResolution.X, (int)ViewportResolution.Y);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            Visualizer.RenderFrame(Shader);

            // blit drawbuffer to OpenGL's backbuffer unless Crossfade is intercepting the final draw buffer
            if (!IsOutputIntercepted)
            {
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, FinalDrawbuffers.FramebufferHandle);
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
                GL.BlitFramebuffer(
                    0, 0, (int)ViewportResolution.X, (int)ViewportResolution.Y,
                    0, 0, Program.AppWindow.ClientSize.X, Program.AppWindow.ClientSize.Y,
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

    public float ElapsedTime()
        => (float)Clock.Elapsed.TotalSeconds;

    public void Dispose()
    {
        if (IsDisposed) return;

        Visualizer?.Dispose();
        Visualizer = null;

        RenderingHelper.DisposeUncachedShader(Shader);
        Shader = null;

        RenderManager.ResourceManager.DestroyAllResources(OwnerName);
        FinalDrawbuffers = null;

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
