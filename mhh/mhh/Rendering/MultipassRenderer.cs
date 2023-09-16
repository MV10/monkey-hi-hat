
using mhh.Utils;
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

        // only calculate ViewportResolution
        // when called from the constructor
        OnResize();

        try
        {
            ParseMultipassConfig();
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

        // blit drawbuffer to OpenGL's backbuffer unless Crossfade is intercepting the final draw buffer
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

    // the [multipass] section is documented by comments in multipass.conf and doublebuffer.conf in the TestContent directory
    private void ParseMultipassConfig()
    {
        // [multipass] exists because RenderManager looks for it to create this class
        var shaderPasses = Config.ConfigSource.SequentialSection("multipass");
        ShaderPasses = new(shaderPasses.Count);

        // indicates how many resources to request and simple validation
        int maxDrawbuffer = -1;
        int maxBackbuffer = -1;
        string backbufferKeys = string.Empty;

        var err = $"Error in {Filename} [multipass] section: ";
        foreach (var kvp in Config.ConfigSource.Content["multipass"])
        {
            MultipassDrawCall shaderPass = new();

            var column = kvp.Value.Split(' ', Const.SplitOptions);
            if (column.Length < 4 || column.Length > 6) throw new ArgumentException($"{err} Invalid entry, 4 to 6 parameters required; content {kvp.Value}");

            //---------------------------------------------------------------------------------------
            // column 0: draw buffer number
            //---------------------------------------------------------------------------------------

            if (!int.TryParse(column[0], out var drawBuffer) || drawBuffer < 0) throw new ArgumentException($"{err} The draw buffer number is not valid; content {column[0]}");
            if (drawBuffer > maxDrawbuffer + 1) throw new ArgumentException($"{err} Each new draw buffer number can only increment by 1 at most; content {column[0]}");
            maxDrawbuffer = Math.Max(maxDrawbuffer, drawBuffer);
            shaderPass.DrawbufferIndex = drawBuffer;

            //---------------------------------------------------------------------------------------
            // column 1: comma-delimited input buffer numbers and letters, or * for no buffer inputs
            //---------------------------------------------------------------------------------------

            shaderPass.InputFrontbufferResources = new();
            shaderPass.InputBackbufferResources = new();
            if (!column[1].Equals("*"))
            {
                var inputs = column[1].Split(',', Const.SplitOptions);
                foreach (var i in inputs)
                {
                    if (!int.TryParse(i, out var inputBuffer))
                    {
                        // previous-frame backbuffers are A-Z
                        inputBuffer = i.IsAlpha() ? i.ToOrdinal() : -1;
                        if (i.Length > 1 || inputBuffer < 0 || inputBuffer > 25) throw new ArgumentException($"{err} The input buffer number is not valid; content {column[1]}");
                        var key = i.ToUpper();
                        if(!backbufferKeys.Contains(key)) backbufferKeys += key;
                        maxBackbuffer = Math.Max(maxBackbuffer, inputBuffer);
                        // store the frontbuffer index, later this will be remapped to the actual resource list index
                        shaderPass.InputBackbufferResources.Add(inputBuffer);
                    }
                    else
                    {
                        // current-frame frontbuffers are integers
                        if(inputBuffer < 0 || inputBuffer > maxDrawbuffer) throw new ArgumentException($"{err} The input buffer number is not valid; content {column[1]}");
                        // for front buffers, there is a 1:1 match of frontbuffer and resource indexes
                        shaderPass.InputFrontbufferResources.Add(inputBuffer);
                    }
                }
            }

            //---------------------------------------------------------------------------------------
            // column 2 & 3: vertex and frag shader filenames
            //---------------------------------------------------------------------------------------

            var vert = (column[2].Equals("*")) ? Path.GetFileNameWithoutExtension(Config.VertexShaderPathname) : column[2];
            if (!vert.EndsWith(".vert", StringComparison.InvariantCultureIgnoreCase)) vert += ".vert";
            var vertPathname = PathHelper.FindFile(Program.AppConfig.VisualizerPath, vert);
            if (vertPathname is null) throw new ArgumentException($"{err} Failed to find vertex shader source file {vert}");

            var frag = (column[3].Equals("*")) ? Path.GetFileNameWithoutExtension(Config.FragmentShaderPathname) : column[3];
            if (!frag.EndsWith(".frag", StringComparison.InvariantCultureIgnoreCase)) frag += ".frag";
            var fragPathname = PathHelper.FindFile(Program.AppConfig.VisualizerPath, frag);
            if (fragPathname is null) throw new ArgumentException($"{err} Failed to find fragment shader source file {frag}");

            // when a --reload command is in effect, reload all shaders used by this renderer
            var replaceCachedShader = RenderingHelper.ReplaceCachedShader;
            shaderPass.Shader = RenderingHelper.GetShader(this, vertPathname, fragPathname);
            if (!IsValid) return;
            RenderingHelper.ReplaceCachedShader = replaceCachedShader;

            //---------------------------------------------------------------------------------------
            // column 4 & 5: visualizer class and settings
            //---------------------------------------------------------------------------------------

            if (column.Length > 4)
            {
                var viz = RenderingHelper.GetVisualizer(this, column[4]);
                if (viz is null) throw new ArgumentException($"{err} Failed to recognize / create visualizer type name {column[4]}");

                // column 5: visualizer settings (very hard-coded for now)
                bool isVertIntArray = (column[4].Equals(nameof(VisualizerVertexIntegerArray), StringComparison.InvariantCultureIgnoreCase));
                if (column.Length == 6 && !isVertIntArray) throw new ArgumentException($"{err} Visualizer type {column[4]} does not require settings");
                if (column.Length == 5 && isVertIntArray) throw new ArgumentException($"{err} Visualizer type {column[4]} is missing required settings");

                if(column.Length == 6)
                {
                    // VisualizerVertexIntegerArray
                    var settings = column[5].Split(';', Const.SplitOptions);
                    if (settings.Length != 2) throw new ArgumentException($"{err} Visualizer type {column[4]} required settings are missing or invalid");
                    var s0 = settings[0].Split('=', Const.SplitOptions);
                    var s1 = settings[1].Split('=', Const.SplitOptions);
                    if (s0.Length != 2 || s1.Length != 2) throw new ArgumentException($"{err} Visualizer type {column[4]}  required settings are missing or invalid");

                    string sIntCount = s0[0].Equals("VertexIntegerCount", StringComparison.InvariantCultureIgnoreCase)
                        ? s0[1]
                        : s1[0].Equals("VertexIntegerCount", StringComparison.InvariantCultureIgnoreCase)
                        ? s1[1]
                        : null;

                    string sDrawMode = s0[0].Equals("ArrayDrawingMode", StringComparison.InvariantCultureIgnoreCase)
                        ? s0[1]
                        : s1[0].Equals("ArrayDrawingMode", StringComparison.InvariantCultureIgnoreCase)
                        ? s1[1]
                        : null;

                    if (sIntCount is null || sDrawMode is null) throw new ArgumentException($"{err} Visualizer type {column[4]}  required settings are missing or invalid");

                    var intCount = sIntCount.ToInt32(1000);
                    var drawMode = sDrawMode.ToEnum(ArrayDrawingMode.Points);

                    (viz as VisualizerVertexIntegerArray).Initialize(intCount, drawMode, shaderPass.Shader);
                }
                else
                {
                    // VisualizerFragmentQuad
                    viz.Initialize(Config, shaderPass.Shader);
                }
            }
            else
            {
                shaderPass.Visualizer = RenderingHelper.GetVisualizer(this, Config);
                if (!IsValid) return;
                shaderPass.Visualizer.Initialize(Config, shaderPass.Shader);
            }

            // store it
            ShaderPasses.Add(shaderPass);
        }

        // highest backbuffer can only be validated after we know all draw buffers
        if (maxBackbuffer > maxDrawbuffer) throw new ArgumentException($"{err} Backbuffer {maxBackbuffer.ToAlpha()} referenced but draw buffer {maxBackbuffer} wasn't used");

        // allocate drawbuffer resources (front buffers when double-buffering)
        DrawbufferResources = RenderManager.ResourceManager.CreateResourceGroups(DrawbufferOwnerName, maxDrawbuffer + 1, ViewportResolution);
        foreach(var resource in DrawbufferResources)
        {
            resource.UniformName = $"input{resource.DrawbufferIndex}";
        }

        // allocate backbuffer resources
        if (maxBackbuffer > -1)
        {
            BackbufferResources = RenderManager.ResourceManager.CreateResourceGroups(BackbufferOwnerName, maxBackbuffer + 1, ViewportResolution);

            // The DrawbufferIndex in the resource list is generated sequentially. This reassigns
            // them based on the order of first backbuffer usage in the multipass config section.
            int i = 0;
            foreach(var backbufferKey in backbufferKeys)
            {
                var resource = BackbufferResources[i++];
                resource.DrawbufferIndex = backbufferKey.ToOrdinal();
                resource.UniformName = $"input{backbufferKey}";
            }

            // At this stage the list of InputBackbufferResources points to the drawbuffer
            // indexes (A = 0, B = 1, etc). This remaps them to the true backbuffer resource
            // index based on order of first usage.
            foreach(var pass in ShaderPasses)
            {
                if(pass.InputBackbufferResources.Count > 0)
                {
                    List<int> newList = new(pass.InputBackbufferResources.Count);
                    foreach (var index in pass.InputBackbufferResources)
                    {
                        var backbufferKey = index.ToAlpha();
                        var resourceIndex = backbufferKeys.IndexOf(backbufferKey);
                        newList.Add(resourceIndex);
                    }
                    pass.InputBackbufferResources = newList;
                }
            }
        }

        // extract the resource values used during rendering
        foreach(var pass in ShaderPasses)
        {
            pass.Drawbuffers = DrawbufferResources[pass.DrawbufferIndex];

            var backbufferKey = pass.DrawbufferIndex.ToAlpha();
            var resourceIndex = backbufferKeys.IndexOf(backbufferKey);
            if (resourceIndex > -1) pass.Backbuffers = BackbufferResources[resourceIndex];
        }
    }

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
