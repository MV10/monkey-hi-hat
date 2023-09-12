
using eyecandy;
using mhh.Utils;
using Microsoft.Extensions.Logging;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;

namespace mhh;

// Crossfade doesn't implement IFramebufferOwner because it isn't expected
// to be "interrogated" by any other renderer, it should always be the last
// in any rendering sequence. If another renderer doesn't implement the
// IFramebufferOwner interface, it simply renders to whatever is bound before
// RenderFrame is called, which means Crossfade can use an internally-owned
// Framebuffer.

public class CrossfadeRenderer : IRenderer
{
    public bool IsValid { get; set; } = true;
    public string InvalidReason { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;

    // During crossfade, the Crossfade class becomes the RenderManager.ActiveRenderer.
    // Upon completion, RenderManager retrieves NewRenderer and makes it ActiveRenderer.
    public IRenderer OldRenderer;
    public IRenderer NewRenderer;

    private IFramebufferOwner OldFramebufferOwner;
    private IFramebufferOwner NewFramebufferOwner;

    // Maintains a pair of output framebuffers in case the old and/or new renderers
    // are not multi-pass (meaning they are designed to target the default OpenGL
    // swap buffers if they run stand-alone).
    private Guid OwnerName = Guid.NewGuid();
    private IReadOnlyList<GLResources> Resources;

    private IVisualizer FragQuadViz;
    private Shader CrossfadeShader;
    private Action CompletionCallback;
    private float DurationMS;
    private Stopwatch Clock = new();

    public CrossfadeRenderer(IRenderer oldRenderer, IRenderer newRenderer, Action completionCallback)
    {
        Resources = RenderManager.ResourceManager.CreateResources(OwnerName, 2);
        CrossfadeShader = Caching.InternalShaders["crossfade"];

        FragQuadViz = new VisualizerFragmentQuad(); 
        FragQuadViz.Initialize(null, CrossfadeShader); // fragquad doesn't have settings, so null is safe
        
        OldRenderer = oldRenderer;
        OldFramebufferOwner = OldRenderer as IFramebufferOwner;
        
        NewRenderer = newRenderer;
        NewFramebufferOwner = NewRenderer as IFramebufferOwner;

        LogHelper.Logger?.LogDebug($"Crossfading old, filename: {OldRenderer.Filename}, multipass? {OldRenderer is IFramebufferOwner}");
        LogHelper.Logger?.LogDebug($"Crossfading new, filename: {NewRenderer.Filename}, multipass? {NewRenderer is IFramebufferOwner}");

        CompletionCallback = completionCallback;
        DurationMS = Program.AppConfig.CrossfadeSeconds * 1000f;
    }

    public void StartClock()
    {
        Clock.Start();
        OldRenderer?.StartClock();
        NewRenderer?.StartClock();
    }

    public void StopClock()
    {
        Clock.Stop();
        OldRenderer?.StopClock();
        NewRenderer?.StopClock();
    }

    public float ElapsedTime()
        => OldRenderer?.ElapsedTime() ?? 0f;

    public void RenderFrame()
    {
        float fadeLevel = (float)Clock.ElapsedMilliseconds / DurationMS;

        // after new renderer fade is 1.0, invoke the callback once, let the
        // new renderer know the final output is no longer being intercepted,
        // and stop rendering until the RenderManager takes over
        if(fadeLevel > 1f)
        {
            CompletionCallback?.Invoke();
            CompletionCallback = null;
            if (NewFramebufferOwner is not null) NewFramebufferOwner.OutputIntercepted = false;
            return;
        }

        // the old renderer draws to its own framebuffer or buffer #0 provided here
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        if (OldFramebufferOwner is null) GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, Resources[0].FramebufferHandle);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        OldRenderer.RenderFrame();

        // the new renderer draws to its own framebuffer or buffer #1 provided here
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        if (NewFramebufferOwner is null) GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, Resources[1].FramebufferHandle);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        NewRenderer.RenderFrame();

        // the GLResources can't be stored in the constructor because a multipass
        // renderer might be swapping front/back buffers after each frame
        var oldResource = OldFramebufferOwner?.OutputBuffers ?? Resources[0];
        var newResource = NewFramebufferOwner?.OutputBuffers ?? Resources[1];

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        CrossfadeShader.Use();
        CrossfadeShader.SetUniform("fadeLevel", fadeLevel);
        CrossfadeShader.SetTexture("oldBuffer", oldResource.TextureHandle, oldResource.TextureUnit);
        CrossfadeShader.SetTexture("newBuffer", newResource.TextureHandle, newResource.TextureUnit);
        FragQuadViz.RenderFrame(CrossfadeShader);

        //...and now AppWindow's OnRenderFrame swaps the back-buffer to the output
    }

    public void Dispose()
    {
        if (IsDisposed) return;

        FragQuadViz?.Dispose();

        if (Resources?.Count > 0) RenderManager.ResourceManager.DestroyResources(OwnerName);
        Resources = null;

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
