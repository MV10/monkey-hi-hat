
using eyecandy;
using mhh.Utils;
using Microsoft.Extensions.Logging;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
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

    // not applicable to this renderer
    public GLResourceGroup OutputBuffers { get => null; }
    public Vector2 Resolution { get => Program.AppWindow.ClientSize; }
    public bool OutputIntercepted { set { } }

    // Maintains a pair of output framebuffers in case the old and/or new renderers
    // are not multi-pass (meaning they are designed to target the default OpenGL
    // swap buffers if they run stand-alone).
    private Guid OldOwnerName = Guid.NewGuid();
    private Guid NewOwnerName = Guid.NewGuid();
    private GLResourceGroup OldResourceGroup = null;
    private GLResourceGroup NewResourceGroup = null;

    private IVisualizer FragQuadViz;
    private Shader CrossfadeShader;
    private Action CompletionCallback;
    private float DurationMS;
    private Stopwatch Clock = new();

    public CrossfadeRenderer(IRenderer oldRenderer, IRenderer newRenderer, Action completionCallback)
    {
        CrossfadeShader = Caching.InternalShaders["crossfade"];

        FragQuadViz = new VisualizerFragmentQuad(); 
        FragQuadViz.Initialize(null, CrossfadeShader); // fragquad doesn't have settings, so null is safe
        
        OldRenderer = oldRenderer;
        NewRenderer = newRenderer;
        CreateResourceGroups();

        LogHelper.Logger?.LogDebug($"Crossfading old, filename: {OldRenderer.Filename}, multipass? {OldRenderer.OutputBuffers is not null}");
        LogHelper.Logger?.LogDebug($"Crossfading new, filename: {NewRenderer.Filename}, multipass? {NewRenderer.OutputBuffers is not null}");

        CompletionCallback = completionCallback;
        DurationMS = Program.AppConfig.CrossfadeSeconds * 1000f;
    }

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
            NewRenderer.OutputIntercepted = false;
            return;
        }

        // the old renderer draws to its own framebuffer or buffer #0 provided here
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        if (OldRenderer.OutputBuffers is null) GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, OldResourceGroup.FramebufferHandle);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        OldRenderer.RenderFrame();

        // the new renderer draws to its own framebuffer or buffer #1 provided here
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        if (OldRenderer.OutputBuffers is null) GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, NewResourceGroup.FramebufferHandle);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        NewRenderer.RenderFrame();

        // the GLResources can't be stored in the constructor because a multipass
        // renderer might be swapping front/back buffers after each frame
        var oldResource = OldRenderer.OutputBuffers ?? OldResourceGroup;
        var newResource = NewRenderer.OutputBuffers ?? NewResourceGroup;

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        CrossfadeShader.Use();
        CrossfadeShader.SetUniform("fadeLevel", fadeLevel);
        CrossfadeShader.SetTexture("oldBuffer", oldResource.TextureHandle, oldResource.TextureUnit);
        CrossfadeShader.SetTexture("newBuffer", newResource.TextureHandle, newResource.TextureUnit);
        FragQuadViz.RenderFrame(CrossfadeShader);

        //...and now AppWindow's OnRenderFrame swaps the back-buffer to the output
    }

    public void OnResize()
    {
        OldRenderer?.OnResize();
        NewRenderer?.OnResize();
        CreateResourceGroups();
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

    private void CreateResourceGroups()
    {
        RenderManager.ResourceManager.DestroyAllResources(OldOwnerName);
        RenderManager.ResourceManager.DestroyAllResources(NewOwnerName);
        if (OldRenderer.OutputBuffers is null) OldResourceGroup = RenderManager.ResourceManager.CreateResourceGroups(OldOwnerName, 1, OldRenderer.Resolution)[0];
        if (NewRenderer.OutputBuffers is null) NewResourceGroup = RenderManager.ResourceManager.CreateResourceGroups(NewOwnerName, 1, NewRenderer.Resolution)[0];
    }

    public void Dispose()
    {
        if (IsDisposed) return;

        FragQuadViz?.Dispose();

        RenderManager.ResourceManager.DestroyAllResources(OldOwnerName);
        RenderManager.ResourceManager.DestroyAllResources(NewOwnerName);
        OldResourceGroup = null;
        NewResourceGroup = null;

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
