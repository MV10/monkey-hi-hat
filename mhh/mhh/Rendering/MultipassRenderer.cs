
using mhh.Utils;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;

namespace mhh;

public class MultipassRenderer : IRenderer, IFramebufferOwner
{
    public bool IsValid { get; set; } = true;
    public string InvalidReason { get; set; } = string.Empty;
    public string Filename { get; set; }

    public VisualizerConfig Config;

    private Guid OwnerName = Guid.NewGuid();
    private IReadOnlyList<GLResources> Resources;
    private Stopwatch Clock = new();

    private List<MultipassDrawCall> DrawCalls;
    private int OutputFramebuffer = -1;
    private bool CopyToBackbuffer = true;

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

        foreach(var pass in DrawCalls)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, pass.DrawbufferHandle);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            Program.AppWindow.Eyecandy.SetTextureUniforms(pass.Shader);
            pass.Shader.SetUniform("resolution", Program.AppWindow.ResolutionUniform);
            pass.Shader.SetUniform("time", timeUniform);
            for(int i = 0; i < pass.InputTextureHandle.Count; i++)
            {
                pass.Shader.SetTexture(pass.InputTextureUniform[i], pass.InputTextureHandle[i], pass.InputTextureUnit[i]);
            }
            pass.Visualizer.RenderFrame(pass.Shader);
        }

        // rendering completed; update the backbuffers for the next frame
        foreach (var pass in DrawCalls)
        {
            if (pass.BackbufferHandle > -1)
                CopyFramebuffer(pass.DrawbufferHandle, pass.BackbufferHandle);
        }

        // blit to OpenGL's backbuffer unless Crossfade is intercepting the final draw buffer
        if (CopyToBackbuffer)
            CopyFramebuffer(Resources[OutputFramebuffer].FramebufferHandle, 0);
    }

    public GLResources GetFinalDrawTargetResource(bool interceptActive)
    {
        CopyToBackbuffer = !interceptActive;
        return (OutputFramebuffer == -1) ? null : Resources?[OutputFramebuffer] ?? null;
    }

    // the [multipass] section is documented by comments in multipass.conf and doublebuffer.conf in the TestContent directory
    private void ParseMultipassConfig()
    {
        // [multipass] exists because RenderManager looks for it to create this class
        var drawCalls = Config.ConfigSource.SequentialSection("multipass");
        DrawCalls = new(drawCalls.Count);

        // indicates how many resources to request and simple validation
        int maxDrawbuffer = -1;
        int maxBackbuffer = -1;
        string backbufferKeys = string.Empty;

        var err = $"Error in {Filename} [multipass] section: ";
        foreach (var kvp in Config.ConfigSource.Content["multipass"])
        {
            MultipassDrawCall drawCall = new();

            var column = kvp.Value.Split(' ', Const.SplitOptions);
            if (column.Length < 4 || column.Length > 6) throw new ArgumentException($"{err} Invalid entry, 4 to 6 parameters required; content {kvp.Value}");

            //---------------------------------------------------------------------------------------
            // column 0: draw buffer number
            //---------------------------------------------------------------------------------------

            if (!int.TryParse(column[0], out var drawBuffer) || drawBuffer < 0) throw new ArgumentException($"{err} The draw buffer number is not valid; content {column[0]}");
            if (drawBuffer > maxDrawbuffer + 1) throw new ArgumentException($"{err} Each new draw buffer number can only increment by 1 at most; content {column[0]}");
            maxDrawbuffer = Math.Max(maxDrawbuffer, drawBuffer);
            drawCall.ParseDrawbufferIndex = drawBuffer;

            //---------------------------------------------------------------------------------------
            // column 1: comma-delimited input buffer numbers and letters, or * for no buffer inputs
            //---------------------------------------------------------------------------------------

            if (column[1].Equals("*"))
            {
                drawCall.ParseInputDrawbufferIndex = new(1);
                drawCall.ParseInputBackbufferKey = new(1);
                drawCall.InputTextureHandle = new(1);
                drawCall.InputTextureUnit = new(1);
                drawCall.InputTextureUniform = new(1);
            }
            else
            {
                var inputs = column[1].Split(',', Const.SplitOptions);
                drawCall.ParseInputDrawbufferIndex = new(inputs.Length);
                drawCall.ParseInputBackbufferKey = new(inputs.Length);
                foreach (var i in inputs)
                {
                    if (!int.TryParse(i, out var inputBuffer))
                    {
                        // backbuffers are A-Z
                        inputBuffer = i.IsAlpha() ? i.ToOrdinal() : -1;
                        if (i.Length > 1 || inputBuffer < 0 || inputBuffer > 25) throw new ArgumentException($"{err} The input buffer number is not valid; content {column[1]}");
                        var key = i.ToUpper();
                        drawCall.ParseInputBackbufferKey.Add(key);
                        if(!backbufferKeys.Contains(key)) backbufferKeys += key;
                        maxBackbuffer = Math.Max(maxBackbuffer, inputBuffer);
                    }
                    else
                    {
                        // current-pass buffers are integers
                        if(inputBuffer < 0 || inputBuffer > maxDrawbuffer) throw new ArgumentException($"{err} The input buffer number is not valid; content {column[1]}");
                        drawCall.ParseInputDrawbufferIndex.Add(inputBuffer);
                    }
                }
                var count = drawCall.ParseInputDrawbufferIndex.Count + drawCall.ParseInputBackbufferKey.Count;
                drawCall.InputTextureHandle = new(count);
                drawCall.InputTextureUnit = new(count);
                drawCall.InputTextureUniform = new(count);
            }

            // assume this won't be backbuffered (can only be determined after all rows parsed)
            drawCall.BackbufferHandle = -1;

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
            drawCall.Shader = RenderingHelper.GetShader(this, vertPathname, fragPathname);
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

                    (viz as VisualizerVertexIntegerArray).DirectInit(intCount, drawMode, drawCall.Shader);
                }
                else
                {
                    // VisualizerFragmentQuad
                    viz.Initialize(Config, drawCall.Shader);
                }
            }
            else
            {
                drawCall.Visualizer = RenderingHelper.GetVisualizer(this, Config);
                if (!IsValid) return;
                drawCall.Visualizer.Initialize(Config, drawCall.Shader);
            }

            // store it
            DrawCalls.Add(drawCall);
        }

        // highest backbuffer can only be validated after we know all draw buffers
        if (maxBackbuffer > maxDrawbuffer) throw new ArgumentException($"{err} Backbuffer {maxBackbuffer.ToAlpha()} referenced but draw buffer {maxBackbuffer} wasn't used");

        // allocate resources
        Resources = RenderManager.ResourceManager.CreateResources(OwnerName, (maxDrawbuffer + 1) + backbufferKeys.Length);

        // extract the resource values used during rendering
        foreach(var call in DrawCalls)
        {
            call.DrawbufferHandle = Resources[call.ParseDrawbufferIndex].FramebufferHandle;

            foreach(var drawIndex in call.ParseInputDrawbufferIndex)
            {
                call.InputTextureHandle.Add(Resources[drawIndex].TextureHandle);
                call.InputTextureUnit.Add(Resources[drawIndex].TextureUnit);
                call.InputTextureUniform.Add($"input{drawIndex}");
            }

            int backbufferOffset = maxDrawbuffer + 1;
            foreach(var key in call.ParseInputBackbufferKey)
            {
                var resourceIndex = backbufferKeys.IndexOf(key) + backbufferOffset;
                call.InputTextureHandle.Add(Resources[resourceIndex].TextureHandle);
                call.InputTextureUnit.Add(Resources[resourceIndex].TextureUnit);
                call.InputTextureUniform.Add($"input{key}");
                DrawCalls[key.ToOrdinal()].BackbufferHandle = Resources[resourceIndex].FramebufferHandle;
            }
        }

        // store the last draw buffer index number
        OutputFramebuffer = DrawCalls[DrawCalls.Count - 1].ParseDrawbufferIndex;
    }

    private void CopyFramebuffer(int sourceHandle, int destHandle)
    {
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, sourceHandle);
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, destHandle);
        GL.BlitFramebuffer(
            0, 0, Program.AppWindow.ClientSize.X, Program.AppWindow.ClientSize.Y,
            0, 0, Program.AppWindow.ClientSize.X, Program.AppWindow.ClientSize.Y,
            ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Dispose()
    {
        if (IsDisposed) return;

        if(DrawCalls is not null)
        {
            foreach (var dc in DrawCalls)
            {
                dc.Visualizer?.Dispose();
                RenderingHelper.DisposeUncachedShader(dc.Shader);
            }
            DrawCalls = null;
        }

        if (Resources?.Count > 0) RenderManager.ResourceManager.DestroyResources(OwnerName);
        Resources = null;

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
    private bool IsDisposed = false;
}
