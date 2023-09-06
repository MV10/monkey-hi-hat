
using eyecandy;
using mhh.Utils;
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

    // Maintains a pair of output framebuffers in case the old and/or new renderers
    // are not multi-pass (meaning they are designed to target the default OpenGL
    // swap buffers if they run stand-alone).
    private Guid OwnerName = Guid.NewGuid();
    private IReadOnlyList<GLResources> Resources;

    // When either of these are null, a single-viz renderer is written
    // to target the default buffers so crossfade will bind an internal
    // one; otherwise these are the final outputs from each renderer to
    // feed into the crossfade mixing renderer
    private GLResources OldDrawTarget = null;
    private GLResources NewDrawTarget = null;

    // Copied from the old/new renderers or the internally-managed resources
    private int OldTextureHandle;
    private int NewTextureHandle;
    private TextureUnit OldTextureUnit;
    private TextureUnit NewTextureUnit;

    private IVisualizer FragQuadViz;
    private Shader CrossfadeShader;
    private Action CompletionCallback;
    private Stopwatch Clock = new();
    private float DurationMS;

    public CrossfadeRenderer(IRenderer oldRenderer, IRenderer newRenderer, Action completionCallback)
    {
        Resources = RenderManager.ResourceManager.CreateResources(OwnerName, 2);
        CrossfadeShader = Caching.InternalShaders["crossfade"];

        FragQuadViz = new VisualizerFragmentQuad(); 
        FragQuadViz.Initialize(null, CrossfadeShader);
        
        OldRenderer = oldRenderer;
        OldDrawTarget = (OldRenderer as IFramebufferOwner)?.GetFinalDrawTargetResource();
        OldTextureHandle = OldDrawTarget?.TextureHandle ?? Resources[0].TextureHandle;
        OldTextureUnit = OldDrawTarget?.TextureUnit ?? Resources[0].TextureUnit;
        
        NewRenderer = newRenderer;
        NewDrawTarget = (NewRenderer as IFramebufferOwner)?.GetFinalDrawTargetResource();
        NewTextureHandle = NewDrawTarget?.TextureHandle ?? Resources[1].TextureHandle;
        NewTextureUnit = NewDrawTarget?.TextureUnit ?? Resources[1].TextureUnit;

        CompletionCallback = completionCallback;
        DurationMS = Program.AppConfig.CrossfadeSeconds * 1000f;
        Clock.Start();
    }

    public void RenderFrame()
    {
        float fadeLevel = (float)Clock.ElapsedMilliseconds / DurationMS;

        // once we pass the 1.0 point, invoke the callback once then stop rendering
        if(fadeLevel > 1f)
        {
            CompletionCallback?.Invoke();
            CompletionCallback = null;
            return;
        }

        // the old renderer draws to its own framebuffer or buffer #0 provided here
        if (OldDrawTarget is null) GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, Resources[0].BufferHandle);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        OldRenderer.RenderFrame();

        // the new renderer draws to its own framebuffer or buffer #1 provided here
        if (NewDrawTarget is null) GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, Resources[1].BufferHandle);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        NewRenderer.RenderFrame();

        // crossfade draws to the default back-buffer using the old and new textures as inputs
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        CrossfadeShader.Use();
        CrossfadeShader.SetUniform("fadeLevel", fadeLevel);
        CrossfadeShader.SetTexture("oldBuffer", OldTextureHandle, OldTextureUnit);
        CrossfadeShader.SetTexture("newBuffer", NewTextureHandle, NewTextureUnit);
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
