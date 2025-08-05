
using eyecandy;
using Microsoft.Extensions.Logging;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace mhh;

public class CrossfadeRenderer : IRenderer
{
    public bool IsValid { get; set; } = true;
    public string InvalidReason { get; set; } = string.Empty;
    public string Filename { get; } = string.Empty;
    public string Description { get; } = string.Empty;

    // During crossfade, the Crossfade class becomes the RenderManager.ActiveRenderer.
    // Upon completion, RenderManager retrieves NewRenderer and makes it ActiveRenderer.
    // Crossfade only temporarily owns these, it does not handle disposal of either one.
    public IRenderer OldRenderer;
    public IRenderer NewRenderer;

    // IRenderer requirements which are not applicable to this renderer.
    public GLResourceGroup OutputBuffers { get => null; }
    public Vector2 Resolution { get => RenderingHelper.ClientSize; }
    public bool OutputIntercepted { set { } }
    public ConfigFile ConfigSource { get => null; }

    // Maintains a pair of output framebuffers in case the old and/or new renderers
    // are not multi-pass (meaning they are designed to target the default OpenGL
    // swap buffers if they run stand-alone).
    private string OldOwnerName = RenderingHelper.MakeOwnerName("OldRenderer");
    private string NewOwnerName = RenderingHelper.MakeOwnerName("NewRenderer");
    private GLResourceGroup OldResourceGroup = null;
    private GLResourceGroup NewResourceGroup = null;

    private IVertexSource VertQuad;
    private Shader CrossfadeShader;
    private Action CompletionCallback;
    private float DurationMS;
    private Stopwatch Clock = new();

    private float FrameCount = 0;
    private float RandomRun;
    private Vector4 RandomRun4;

    // Used to randomize cached shaders while avoiding repetition
    private static Random RNG = new();
    private static List<int> CacheIndexes;

    public CrossfadeRenderer(IRenderer oldRenderer, IRenderer newRenderer, Action completionCallback, string fragPathname = "")
    {
        // Handle crossfade test mode
        if(Program.AppWindow.Tester is not null && Program.AppWindow.Tester.Mode == TestMode.Fade)
        {
            CrossfadeShader = Program.AppWindow.Tester.CrossfadeShader;
        }
        else
        {
            // Handle explicitly queued crossfader
            if(!string.IsNullOrEmpty(fragPathname))
            {
                var key = CachedShader.KeyFrom(ApplicationConfiguration.PassthroughVertexPathname, fragPathname);
                CrossfadeShader = Caching.CrossfadeShaders.FirstOrDefault(s => s.Key.Equals(key)) ?? new(ApplicationConfiguration.PassthroughVertexPathname, fragPathname); ;
                if(!CrossfadeShader.IsValid)
                {
                    CrossfadeShader?.Dispose();
                    CrossfadeShader = Caching.InternalCrossfadeShader;
                }
            }
            else
            {
                // Handle randomized crossfades
                if (Program.AppConfig.RandomizeCrossfade)
                {
                    if (CacheIndexes is null || CacheIndexes.Count == 0)
                    {
                        var list = new int[Caching.CrossfadeShaders.Count];
                        for (int j = 0; j < list.Length; j++) list[j] = j;
                        RNG.Shuffle(list);
                        CacheIndexes = list.ToList();
                    }
                    int i = CacheIndexes[0];
                    CacheIndexes.RemoveAt(0);
                    CrossfadeShader = Caching.CrossfadeShaders[i];
                    LogHelper.Logger?.LogDebug($"Crossfade shader {((CachedShader)CrossfadeShader).Key}");
                }
                // Default to basic internal crossfade
                else
                {
                    CrossfadeShader = Caching.InternalCrossfadeShader;
                }
            }
        }

        VertQuad = new VertexQuad(); 
        VertQuad.Initialize(null, CrossfadeShader); // null is safe, fragquad has no viz/fx settings and crossfade doesn't support textures/videos
        
        OldRenderer = oldRenderer;
        NewRenderer = newRenderer;
        CreateResourceGroups();

        LogHelper.Logger?.LogDebug($"Crossfading old, filename: {OldRenderer.Filename}, multipass? {OldRenderer.OutputBuffers is not null}");
        LogHelper.Logger?.LogDebug($"Crossfading new, filename: {NewRenderer.Filename}, multipass? {NewRenderer.OutputBuffers is not null}");

        CompletionCallback = completionCallback;
        DurationMS = Program.AppConfig.CrossfadeSeconds * 1000f;

        // Force the application of any defined FXResolutionLimit
        RenderingHelper.UseCrossfadeResolutionLimit = true;
        OldRenderer.OnResize();
        NewRenderer.OnResize();

        RandomRun = (float)RNG.NextDouble();
        RandomRun4 = new((float)RNG.NextDouble(), (float)RNG.NextDouble(), (float)RNG.NextDouble(), (float)RNG.NextDouble());
    }

    public void PreRenderFrame()
    {
        if (OldRenderer is null || NewRenderer is null) return;
        OldRenderer?.PreRenderFrame();
        NewRenderer?.PreRenderFrame();
    }

    public void RenderFrame(ScreenshotWriter screenshotHandler = null)
    {
        if (CompletionCallback is null) return;

        float fadeLevel = (float)Clock.ElapsedMilliseconds / DurationMS;

        // the old renderer draws to its own framebuffer or buffer #0 provided here
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        if (OldRenderer.OutputBuffers is null) GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, OldResourceGroup.FramebufferHandle);
        GL.Viewport(0, 0, (int)OldRenderer.Resolution.X, (int)OldRenderer.Resolution.Y);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        OldRenderer.RenderFrame();

        // the new renderer draws to its own framebuffer or buffer #1 provided here
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        if (NewRenderer.OutputBuffers is null) GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, NewResourceGroup.FramebufferHandle);
        GL.Viewport(0, 0, (int)NewRenderer.Resolution.X, (int)NewRenderer.Resolution.Y);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        NewRenderer.RenderFrame();

        // the GLResources can't be stored in the constructor because a multipass
        // renderer might be swapping front/back buffers after each frame
        var oldResource = OldRenderer.OutputBuffers ?? OldResourceGroup;
        var newResource = NewRenderer.OutputBuffers ?? NewResourceGroup;

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.Viewport(0, 0, RenderingHelper.ClientSize.X, RenderingHelper.ClientSize.Y);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        CrossfadeShader.Use();
        CrossfadeShader.ResetUniforms();
        RenderingHelper.SetGlobalUniforms(CrossfadeShader);
        CrossfadeShader.SetUniform("fadeLevel", fadeLevel);
        CrossfadeShader.SetUniform("fadeDuration", (float)Program.AppConfig.CrossfadeSeconds);
        CrossfadeShader.SetTexture("oldBuffer", oldResource.TextureHandle, oldResource.TextureUnit);
        CrossfadeShader.SetTexture("newBuffer", newResource.TextureHandle, newResource.TextureUnit);
        CrossfadeShader.SetUniform("resolution", Resolution);
        CrossfadeShader.SetUniform("time", TrueElapsedTime());
        CrossfadeShader.SetUniform("frame", FrameCount);
        CrossfadeShader.SetUniform("randomrun", RandomRun);
        CrossfadeShader.SetUniform("randomrun4", RandomRun4);
        VertQuad.RenderFrame(CrossfadeShader);

        //...and now AppWindow's OnRenderFrame swaps the back-buffer to the output

        // after new renderer fade is 1.0, invoke the callback once, let the
        // new renderer know the final output is no longer being intercepted,
        // and stop rendering until the RenderManager takes over
        if (fadeLevel >= 1f)
        {
            RenderingHelper.UseCrossfadeResolutionLimit = false;
            CompletionCallback?.Invoke();
            CompletionCallback = null;
            NewRenderer.OutputIntercepted = false;
        }

        FrameCount++;
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

    public float TrueElapsedTime()
        => (float)Clock.Elapsed.TotalSeconds;

    public Dictionary<string, float> GetFXUniforms(string fxFilename)
        => new();

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
        LogHelper.Logger?.LogTrace($"{GetType()}.Dispose() ----------------------------");

        RenderingHelper.UseCrossfadeResolutionLimit = false;

        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() IVertexSource");
        VertQuad?.Dispose();

        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() OldRenderer Resources");
        RenderManager.ResourceManager.DestroyAllResources(OldOwnerName);

        LogHelper.Logger?.LogTrace($"  {GetType()}.Dispose() NewRenderer Resources");
        RenderManager.ResourceManager.DestroyAllResources(NewOwnerName);

        OldResourceGroup = null;
        NewResourceGroup = null;

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
