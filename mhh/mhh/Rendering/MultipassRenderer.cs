
using mhh.Utils;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Diagnostics;

namespace mhh;

// Multipass also allows for double-buffering, which means some passes
// have a frontbuffer and a backbuffer, where the backbuffer contains
// the final output from the previous frame. This is handled by
// allocating two sets of GLResources, those for the normal multipass
// buffers, and those for the backbuffers, which are then swapped in
// the DrawCall objects that own double-buffers.

public class MultipassRenderer : IRenderer, IFramebufferOwner
{
    public bool IsValid { get; set; } = true;
    public string InvalidReason { get; set; } = string.Empty;
    public string Filename { get; set; }
    
    public GLResources OutputBuffers { get => FinalDrawbuffers; }
    private GLResources FinalDrawbuffers;
    
    public bool OutputIntercepted { set => IsOutputIntercepted = value; }
    private bool IsOutputIntercepted = false;

    public VisualizerConfig Config;

    private Guid OwnerName = Guid.NewGuid();
    private Guid BackbufferOwnerName = Guid.NewGuid();
    private IReadOnlyList<GLResources> Resources;
    private IReadOnlyList<GLResources> BackbufferResources;
    private List<MultipassDrawCall> ShaderPasses;
    private Stopwatch Clock = new();
    private float FrameCount = 0;

    public MultipassRenderer(VisualizerConfig visualizerConfig)
    {
        Config = visualizerConfig;
        Filename = Path.GetFileNameWithoutExtension(Config.ConfigSource.Pathname);

        try
        {
            ParseMultipassConfig();
        }
        catch(ArgumentException ex)
        {
            IsValid = false;
            InvalidReason = ex.Message;
        }
    }

    public void StartClock()
        => Clock.Start();

    public void StopClock()
        => Clock.Stop();

    public float ElapsedTime()
        => (float)Clock.Elapsed.TotalSeconds;

    public void RenderFrame()
    {
        var timeUniform = ElapsedTime();

        foreach(var pass in ShaderPasses)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, pass.Drawbuffers.FramebufferHandle);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            Program.AppWindow.Eyecandy.SetTextureUniforms(pass.Shader);
            pass.Shader.SetUniform("resolution", Program.AppWindow.ResolutionUniform);
            pass.Shader.SetUniform("time", timeUniform);
            pass.Shader.SetUniform("frame", FrameCount);
            foreach (var index in pass.InputFrontbufferResources)
            {
                var resource = Resources[index];
                pass.Shader.SetTexture(resource.UniformName, resource.TextureHandle, resource.TextureUnit);
            }
            foreach (var index in pass.InputBackbufferResources)
            {
                var resource = BackbufferResources[index];
                pass.Shader.SetTexture(resource.UniformName, resource.TextureHandle, resource.TextureUnit);
            }
            pass.Visualizer.RenderFrame(pass.Shader);
        }

        // store this in case crossfade is active and the output buffer does a front/backbuffer swap
        FinalDrawbuffers = ShaderPasses[ShaderPasses.Count - 1].Drawbuffers;

        // blit drawbuffer to OpenGL's backbuffer unless Crossfade is intercepting the final draw buffer
        if (!IsOutputIntercepted)
        {
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, FinalDrawbuffers.FramebufferHandle);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
            GL.BlitFramebuffer(
                0, 0, Program.AppWindow.ClientSize.X, Program.AppWindow.ClientSize.Y,
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

                    (viz as VisualizerVertexIntegerArray).DirectInit(intCount, drawMode, shaderPass.Shader);
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
        Resources = RenderManager.ResourceManager.CreateResources(OwnerName, maxDrawbuffer + 1);
        foreach(var resource in Resources)
        {
            resource.UniformName = $"input{resource.DrawbufferIndex}";
        }

        // allocate backbuffer resources
        if (maxBackbuffer > -1)
        {
            BackbufferResources = RenderManager.ResourceManager.CreateResources(BackbufferOwnerName, maxBackbuffer + 1);

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
            pass.Drawbuffers = Resources[pass.DrawbufferIndex];

            var backbufferKey = pass.DrawbufferIndex.ToAlpha();
            var resourceIndex = backbufferKeys.IndexOf(backbufferKey);
            if (resourceIndex > -1) pass.Backbuffers = BackbufferResources[resourceIndex];
        }
    }

    public void Dispose()
    {
        if (IsDisposed) return;

        if(ShaderPasses is not null)
        {
            foreach (var dc in ShaderPasses)
            {
                dc.Visualizer?.Dispose();
                RenderingHelper.DisposeUncachedShader(dc.Shader);
            }
            ShaderPasses = null;
        }

        if (Resources?.Count > 0) RenderManager.ResourceManager.DestroyResources(OwnerName);
        if (BackbufferResources?.Count > 0) RenderManager.ResourceManager.DestroyResources(BackbufferOwnerName);
        Resources = null;
        BackbufferResources = null;

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
