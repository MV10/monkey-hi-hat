﻿
using Microsoft.Extensions.Logging;

namespace mhh;

/// <summary>
/// Parses [multipass] sections for MultipassRenderer and FXRenderer.
/// The [multipass] section is documented by comments in multipass.conf,
/// mpvizconf.conf, and doublebuffer.conf in the TestContent directory, and
/// by waterflow.conf in the TestContent/FX directory.
/// </summary>
public class MultipassSectionParser
{
    // The renderer will read these, then destroy the parser object
    public IReadOnlyList<GLResourceGroup> DrawbufferResources;
    public IReadOnlyList<GLResourceGroup> BackbufferResources;
    public List<MultipassDrawCall> ShaderPasses;

    // References to the object requesting parsing
    private IRenderer OwningRenderer;
    private string DrawbufferOwnerName = RenderingHelper.MakeOwnerName("Drawbuffers");
    private string BackbufferOwnerName = RenderingHelper.MakeOwnerName("Backbuffers");
    private ConfigFile configSource;

    // Type-specific configs from supported renderer types
    private VisualizerConfig RendererVizConfig;
    private FXConfig RendererFXConfig;

    // Indicates how many resources to request and simple validation
    private int MaxDrawbuffer = -1;
    private int MaxBackbuffer = -1;
    private string BackbufferKeys = string.Empty;

    // The [multipass] line being parsed
    private MultipassDrawCall ShaderPass;
    private string[] column;
    private string err;

    public MultipassSectionParser(IRenderer forRenderer, string drawbufferOwnerName, string backbufferOwnerName)
    {
        OwningRenderer = forRenderer;
        DrawbufferOwnerName = drawbufferOwnerName;
        BackbufferOwnerName = backbufferOwnerName;

        configSource = (OwningRenderer as IConfigSource).ConfigSource;

        if(OwningRenderer is MultipassRenderer)
        {
            RendererVizConfig = (OwningRenderer as MultipassRenderer).Config;
            MultipassRendererParse();
            if (!OwningRenderer.IsValid) return;
            AllocateResources();
        }

        if (OwningRenderer is FXRenderer)
        {
            RendererFXConfig = (OwningRenderer as FXRenderer).Config;
            FXRendererParse();
            if (!OwningRenderer.IsValid) return;
            AllocateResources();
        }
    }

    private void MultipassRendererParse()
    {
        LogHelper.Logger?.LogTrace($"BEGIN {nameof(MultipassRendererParse)}");

        ShaderPasses = new(configSource.SequentialSection("multipass").Count);

        int passline = 0;
        foreach (var kvp in configSource.Content["multipass"])
        {
            err = $"Error in {OwningRenderer.Filename} [multipass] pass {passline}: ";
            LogHelper.Logger?.LogTrace($"PARSING pass #{passline}");
            passline++;
            ShaderPass = new();

            GetMultipassColumns(kvp);
            LogHelper.Logger?.LogTrace($"  found {column.Length} columns");

            // all decisions based on column.Length should be made here
            ParseDrawBuffer();
            ParseInputBuffers();
            if(column.Length == 3)
            {
                ParseVisualizerConf();
            }
            else
            {
                ParseShaders();
                if (column.Length == 4)
                {
                    UseDefaultVertexSource();
                    if (!OwningRenderer.IsValid) return;
                }
                else
                {
                    // column 4: vertex source typename
                    LogHelper.Logger?.LogDebug($"  using vertex source {column[4]}");
                    ShaderPass.VertexSource = RenderingHelper.GetVertexSource(OwningRenderer, column[4]);
                    if (!OwningRenderer.IsValid) return;

                    // validate columns 5 & 6
                    bool isVertIntArray = (column[4].Equals(nameof(VertexIntegerArray), Const.CompareFlags));
                    if (column.Length == 6 && !isVertIntArray) throw new ArgumentException($"{err} VertexSource type {column[4]} does not require settings");
                    if (column.Length == 5 && isVertIntArray) throw new ArgumentException($"{err} VertexSource type {column[4]} is missing required settings");

                    if(column.Length == 6)
                    {
                        // settings required for VertexIntegerArray
                        ParseVertexSourceSettings();
                    }
                    else
                    {
                        // has to be VertexQuad
                        ShaderPass.VertexSource.Initialize(RendererVizConfig, ShaderPass.Shader);
                    }
                }
            }

            ShaderPasses.Add(ShaderPass);
        }

        LogHelper.Logger?.LogTrace($"END {nameof(MultipassRendererParse)}");
    }

    private void FXRendererParse()
    {
        ShaderPasses = new(configSource.SequentialSection("multipass").Count + 1);

        // configure the first pass as the primary renderer
        MaxDrawbuffer = 0;
        ShaderPass = new()
        {
            DrawbufferIndex = 0,
            InputsDrawbuffers = new(),
            InputsBackbuffers = new(),
        };
        ShaderPasses.Add(ShaderPass);

        int passline = 1;
        foreach (var kvp in configSource.Content["multipass"])
        {
            err = $"Error in {OwningRenderer.Filename} [multipass] pass {passline++}: ";
            ShaderPass = new();

            GetFXColumns(kvp);
            ParseDrawBuffer();

            if (ShaderPass.DrawbufferIndex == 0) throw new ArgumentException($"{err} Draw buffer 0 is reserved for the primary visualization; content {column[0]}");

            ParseInputBuffers();
            ParseFXFragShader();
            if (!OwningRenderer.IsValid) return;

            // every FX pass uses a VertexQuad
            ShaderPass.VertexSource = new VertexQuad();
            ShaderPass.VertexSource.Initialize(null, ShaderPass.Shader);

            ShaderPasses.Add(ShaderPass);
        }
    }

    private void GetMultipassColumns(KeyValuePair<string, string> kvp)
    {
        column = kvp.Value.Split(' ', Const.SplitOptions);
        if (column.Length == 3)
        {
            if (!column[2].Equals("*") && !column[2].EndsWith(".conf", Const.CompareFlags))
                throw new ArgumentException($"{err} Invalid entry, 3 parameters must reference * or a visualizer .conf; content {kvp.Value}");
        }
        else
        {
            if (column.Length < 4 || column.Length > 6)
                throw new ArgumentException($"{err} Invalid entry, 4 to 6 parameters required; content {kvp.Value}");
        }
    }

    private void GetFXColumns(KeyValuePair<string, string> kvp)
    {
        column = kvp.Value.Split(' ', Const.SplitOptions);
        if (column.Length != 3)
            throw new ArgumentException($"{err} Invalid entry, 3 parameters required; content {kvp.Value}");
    }

    // MP and FX column 0: draw buffer number
    private void ParseDrawBuffer()
    {
        if (!int.TryParse(column[0], out var drawBuffer) || drawBuffer < 0) throw new ArgumentException($"{err} The draw buffer number is not valid; content {column[0]}");
        if (drawBuffer > MaxDrawbuffer + 1) throw new ArgumentException($"{err} Each new draw buffer number can only increment by 1 at most; content {column[0]}");
        MaxDrawbuffer = Math.Max(MaxDrawbuffer, drawBuffer);
        ShaderPass.DrawbufferIndex = drawBuffer;
        LogHelper.Logger?.LogTrace($"  draw buffer {drawBuffer}");
    }

    // MP and FX column 1: input buffer numbers and backbuffer letters, or * for no inputs
    private void ParseInputBuffers()
    {
        ShaderPass.InputsDrawbuffers = new();
        ShaderPass.InputsBackbuffers = new();
        if (column[1].Equals("*")) return;

        var inputs = column[1].Split(',', Const.SplitOptions);
        foreach (var i in inputs)
        {
            LogHelper.Logger?.LogTrace($"  input buffer {i}");
            if (!int.TryParse(i, out var sourceBuffer))
            {
                // TryParse failed, previous-frame backbuffers are A-Z
                sourceBuffer = i.IsAlpha() ? i.ToOrdinal() : -1;
                if (i.Length > 1 || sourceBuffer < 0 || sourceBuffer > 25) throw new ArgumentException($"{err} The input buffer number is not valid; content {column[1]}");
                var key = i.ToUpper();
                if (!BackbufferKeys.Contains(key)) BackbufferKeys += key;
                MaxBackbuffer = Math.Max(MaxBackbuffer, sourceBuffer);
                // store the frontbuffer index, later this will be remapped to the actual resource list index
                ShaderPass.InputsBackbuffers.Add(sourceBuffer);
            }
            else
            {
                // current-frame frontbuffers are integers
                if (sourceBuffer < 0 || sourceBuffer > MaxDrawbuffer) throw new ArgumentException($"{err} The input buffer number is not valid; content {column[1]}");
                // for front buffers, there is a 1:1 match of frontbuffer and resource indexes
                ShaderPass.InputsDrawbuffers.Add(sourceBuffer);
            }
        }
    }

    // FX column 2: frag shader from FXPath
    private void ParseFXFragShader()
    {
        var frag = column[2];
        if (!frag.EndsWith(".frag", Const.CompareFlags)) frag += ".frag";
        var fragPathname = PathHelper.FindFile(Program.AppConfig.FXPath, frag);
        if (fragPathname is null) throw new ArgumentException($"{err} Failed to find FX shader source file {frag}");

        // when a --reload command is in effect, reload all shaders used by this renderer (save and restore the value)
        var replaceCachedShader = RenderingHelper.ReplaceCachedShader;
        ShaderPass.Shader = RenderingHelper.GetFXShader(OwningRenderer, ApplicationConfiguration.PassthroughVertexPathname, fragPathname, RendererFXConfig.LibraryConfigs);
        if (!OwningRenderer.IsValid) return;
        RenderingHelper.ReplaceCachedShader = replaceCachedShader;

        // not necessary to set ShaderPass.Uniforms for FX, those are stored at the FXConfig level.
    }

    // MP column 2: visualizer.conf-based multipass (see multipass.conf for docs)
    private void ParseVisualizerConf()
    {
        var vizPathname = (column[2].Equals("*"))
            ? configSource.Pathname
            : PathHelper.FindFile(Program.AppConfig.VisualizerPath, column[2]);
        if (vizPathname is null) throw new ArgumentException($"{err} Failed to find visualizer config {vizPathname}");

        LogHelper.Logger?.LogDebug($"  load viz.conf {vizPathname}");
        var vizConfig = new VisualizerConfig(vizPathname);

        // when a --reload command is in effect, reload all shaders used by this renderer (save and restore the value)
        var replaceCachedShader = RenderingHelper.ReplaceCachedShader;
        ShaderPass.Shader = RenderingHelper.GetVisualizerShader(OwningRenderer, vizConfig);
        if (!OwningRenderer.IsValid) return;
        RenderingHelper.ReplaceCachedShader = replaceCachedShader;

        ShaderPass.Uniforms = vizConfig.Uniforms;
        ShaderPass.VertexSource = RenderingHelper.GetVertexSource(OwningRenderer, vizConfig);
        ShaderPass.VertexSource.Initialize(vizConfig, ShaderPass.Shader);
    }

    // MP column 2 & 3: vertex and frag shader filenames
    private void ParseShaders()
    {
        var file = column[2];
        var vertPathname = RendererVizConfig.VertexShaderPathname;
        if (!file.Equals("*"))
        {
            file = (!file.EndsWith(".vert", Const.CompareFlags)) ? file += ".vert" : file;
            vertPathname = PathHelper.FindFile(Program.AppConfig.VisualizerPath, file);
            if (vertPathname is null) throw new ArgumentException($"{err} Failed to find vertex shader source file {file}");
            LogHelper.Logger?.LogDebug($"  using vertex shader {file}");
        }

        file = column[3];
        var fragPathname = RendererVizConfig.FragmentShaderPathname;
        if (!file.Equals("*"))
        {
            file = (!file.EndsWith(".frag", Const.CompareFlags)) ? file += ".frag" : file;
            fragPathname = PathHelper.FindFile(Program.AppConfig.VisualizerPath, file);
            if (fragPathname is null) throw new ArgumentException($"{err} Failed to find fragment shader source file {file}");
            LogHelper.Logger?.LogDebug($"  using fragment shader {file}");
        }

        // when a --reload command is in effect, reload all shaders used by this renderer (save and restore the value)
        var replaceCachedShader = RenderingHelper.ReplaceCachedShader;
        ShaderPass.Shader = RenderingHelper.GetVisualizerShader(OwningRenderer, vertPathname, fragPathname, RendererVizConfig.LibraryConfigs);
        if (!OwningRenderer.IsValid) return;
        RenderingHelper.ReplaceCachedShader = replaceCachedShader;
    }

    // MP column 4+: not defined, default to same as renderer's visualizer.conf
    private void UseDefaultVertexSource()
    {
        LogHelper.Logger?.LogDebug($"  using default vertex source");
        ShaderPass.VertexSource = RenderingHelper.GetVertexSource(OwningRenderer, RendererVizConfig);
        if (!OwningRenderer.IsValid) return;
        ShaderPass.VertexSource.Initialize(RendererVizConfig, ShaderPass.Shader);

        // no need to set ShaderPass.Uniforms, the renderer's viz config will supply them
    }

    // MP column 5: VisualizerVertexIntegerArray settings
    private void ParseVertexSourceSettings()
    {
        LogHelper.Logger?.LogDebug($"  parsing vertex source settings");
        var settings = column[5].Split(';', Const.SplitOptions);
        if (settings.Length != 2) throw new ArgumentException($"{err} VertexSource type {column[4]} required settings are missing or invalid");
        var s0 = settings[0].Split(':', Const.SplitOptions);
        var s1 = settings[1].Split(':', Const.SplitOptions);
        if (s0.Length != 2 || s1.Length != 2) throw new ArgumentException($"{err} VertexSource type {column[4]} required settings are missing or invalid");

        string sIntCount = s0[0].Equals("VertexIntegerCount", Const.CompareFlags)
            ? s0[1]
            : s1[0].Equals("VertexIntegerCount", Const.CompareFlags)
            ? s1[1]
            : null;

        string sDrawMode = s0[0].Equals("ArrayDrawingMode", Const.CompareFlags)
            ? s0[1]
            : s1[0].Equals("ArrayDrawingMode", Const.CompareFlags)
            ? s1[1]
            : null;

        if (sIntCount is null || sDrawMode is null) throw new ArgumentException($"{err} VertexSource type {column[4]} required settings are missing or invalid");

        var intCount = sIntCount.ToInt32(1000);
        var drawMode = sDrawMode.ToEnum(ArrayDrawingMode.Points);

        LogHelper.Logger?.LogDebug($"  parsed VertexIntegerCount {intCount}");
        LogHelper.Logger?.LogDebug($"  parsed ArrayDrawingMode {drawMode}");

        (ShaderPass.VertexSource as VertexIntegerArray).Initialize(intCount, drawMode, ShaderPass.Shader);
    }

    private void AllocateResources()
    {
        err = $"Error in {OwningRenderer.Filename} [multipass] section: ";

        // highest backbuffer can only be validated after we know all draw buffers
        if (MaxBackbuffer > MaxDrawbuffer) throw new ArgumentException($"{err} Backbuffer {MaxBackbuffer.ToAlpha()} referenced but draw buffer {MaxBackbuffer} wasn't used");

        // allocate drawbuffer resources (front buffers when double-buffering)
        DrawbufferResources = RenderManager.ResourceManager.CreateResourceGroups(DrawbufferOwnerName, MaxDrawbuffer + 1, OwningRenderer.Resolution);
        foreach (var resource in DrawbufferResources)
        {
            resource.UniformName = $"input{resource.DrawPassIndex}";
        }

        // allocate backbuffer resources
        if (BackbufferKeys.Length > 0)
        {
            BackbufferResources = RenderManager.ResourceManager.CreateResourceGroups(BackbufferOwnerName, BackbufferKeys.Length, OwningRenderer.Resolution);

            // The DrawbufferIndex in the resource list is generated sequentially. This reassigns
            // them based on the order of first backbuffer usage in the multipass config section.
            int i = 0;
            foreach (var backbufferKey in BackbufferKeys)
            {
                var resource = BackbufferResources[i++];
                resource.DrawPassIndex = backbufferKey.ToOrdinal();
                resource.UniformName = $"input{backbufferKey}";
            }
        }

        // extract the resource values used during rendering
        foreach (var pass in ShaderPasses)
        {
            pass.Drawbuffers = DrawbufferResources[pass.DrawbufferIndex];

            var backbufferKey = pass.DrawbufferIndex.ToAlpha();
            var resourceIndex = BackbufferKeys.IndexOf(backbufferKey);
            if (resourceIndex > -1) pass.Backbuffers = BackbufferResources[resourceIndex];
        }
    }
}
