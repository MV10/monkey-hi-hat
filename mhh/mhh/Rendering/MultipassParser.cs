
namespace mhh;

/// <summary>
/// The [multipass] section is documented by comments in multipass.conf,
/// mpvizconf.conf, and doublebuffer.conf in the TestContent directory.
/// </summary>
public class MultipassParser
{
    // The renderer will read these, then destroy the parser object
    public IReadOnlyList<GLResourceGroup> DrawbufferResources;
    public IReadOnlyList<GLResourceGroup> BackbufferResources;
    public List<MultipassDrawCall> ShaderPasses;

    // References to the object requesting parsing
    private MultipassRenderer OwningRenderer;
    private Guid DrawbufferOwnerName = Guid.NewGuid();
    private Guid BackbufferOwnerName = Guid.NewGuid();

    // Indicates how many resources to request and simple validation
    private int MaxDrawbuffer = -1;
    private int MaxBackbuffer = -1;
    private string BackbufferKeys = string.Empty;

    // The [multipass] line being parsed
    private MultipassDrawCall ShaderPass;
    private string[] column;
    private string err;

    public MultipassParser(MultipassRenderer forRenderer, Guid drawbufferOwnerName, Guid backbufferOwnerName)
    {
        OwningRenderer = forRenderer;
        DrawbufferOwnerName = drawbufferOwnerName;
        BackbufferOwnerName = backbufferOwnerName;

        ShaderPasses = new(OwningRenderer.Config.ConfigSource.SequentialSection("multipass").Count);

        Parse();
        if (!OwningRenderer.IsValid) return;
        AllocateResources();
    }

    private void Parse()
    {
        int passline = 0;
        foreach (var kvp in OwningRenderer.Config.ConfigSource.Content["multipass"])
        {
            err = $"Error in {OwningRenderer.Filename} [multipass] pass {passline++}: ";
            ShaderPass = new();

            GetColumns(kvp);

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
                    UseDefaultVisualizer();
                    if (!OwningRenderer.IsValid) return;
                }
                else
                {
                    // column 4: visualizer class
                    ShaderPass.Visualizer = RenderingHelper.GetVisualizer(OwningRenderer, column[4]);
                    if (!OwningRenderer.IsValid) return;

                    // validate columns 5 & 6
                    bool isVertIntArray = (column[4].Equals(nameof(VisualizerVertexIntegerArray), StringComparison.InvariantCultureIgnoreCase));
                    if (column.Length == 6 && !isVertIntArray) throw new ArgumentException($"{err} Visualizer type {column[4]} does not require settings");
                    if (column.Length == 5 && isVertIntArray) throw new ArgumentException($"{err} Visualizer type {column[4]} is missing required settings");

                    if(column.Length == 6)
                    {
                        // settings required for VisualizerVertexIntegerArray
                        ParseVisualizerSettings();
                    }
                    else
                    {
                        // has to be VisualizerFragmentQuad
                        ShaderPass.Visualizer.Initialize(OwningRenderer.Config, ShaderPass.Shader);
                    }
                }
            }

            ShaderPasses.Add(ShaderPass);
        }
    }

    private void GetColumns(KeyValuePair<string, string> kvp)
    {
        column = kvp.Value.Split(' ', Const.SplitOptions);
        if (column.Length == 3)
        {
            if (!column[2].Equals("*") && !column[2].EndsWith(".conf", StringComparison.InvariantCultureIgnoreCase))
                throw new ArgumentException($"{err} Invalid entry, 3 parameters must reference * or a visualizer .conf; content {kvp.Value}");
        }
        else
        {
            if (column.Length < 4 || column.Length > 6)
                throw new ArgumentException($"{err} Invalid entry, 4 to 6 parameters required; content {kvp.Value}");
        }
    }

    // column 0: draw buffer number
    private void ParseDrawBuffer()
    {
        if (!int.TryParse(column[0], out var drawBuffer) || drawBuffer < 0) throw new ArgumentException($"{err} The draw buffer number is not valid; content {column[0]}");
        if (drawBuffer > MaxDrawbuffer + 1) throw new ArgumentException($"{err} Each new draw buffer number can only increment by 1 at most; content {column[0]}");
        MaxDrawbuffer = Math.Max(MaxDrawbuffer, drawBuffer);
        ShaderPass.DrawbufferIndex = drawBuffer;
    }

    // column 1: input buffer numbers and backbuffer letters, or * for no inputs
    private void ParseInputBuffers()
    {
        ShaderPass.InputFrontbufferResources = new();
        ShaderPass.InputBackbufferResources = new();
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
                    if (!BackbufferKeys.Contains(key)) BackbufferKeys += key;
                    MaxBackbuffer = Math.Max(MaxBackbuffer, inputBuffer);
                    // store the frontbuffer index, later this will be remapped to the actual resource list index
                    ShaderPass.InputBackbufferResources.Add(inputBuffer);
                }
                else
                {
                    // current-frame frontbuffers are integers
                    if (inputBuffer < 0 || inputBuffer > MaxDrawbuffer) throw new ArgumentException($"{err} The input buffer number is not valid; content {column[1]}");
                    // for front buffers, there is a 1:1 match of frontbuffer and resource indexes
                    ShaderPass.InputFrontbufferResources.Add(inputBuffer);
                }
            }
        }
    }

    // column 2: visualizer.conf-based multipass (see mpvizconf.conf for docs)
    private void ParseVisualizerConf()
    {
        var vizPathname = (column[2].Equals("*"))
            ? OwningRenderer.Config.ConfigSource.Pathname
            : PathHelper.FindFile(Program.AppConfig.VisualizerPath, column[2]);
        if (vizPathname is null) throw new ArgumentException($"{err} Failed to find visualizer config {vizPathname}");

        var vizConfig = new VisualizerConfig(vizPathname);

        // when a --reload command is in effect, reload all shaders used by this renderer (save and restore the value)
        var replaceCachedShader = RenderingHelper.ReplaceCachedShader;
        ShaderPass.Shader = RenderingHelper.GetShader(OwningRenderer, vizConfig);
        if (!OwningRenderer.IsValid) return;
        RenderingHelper.ReplaceCachedShader = replaceCachedShader;

        ShaderPass.Visualizer = RenderingHelper.GetVisualizer(OwningRenderer, vizConfig);
        ShaderPass.Visualizer.Initialize(vizConfig, ShaderPass.Shader);
    }

    // column 2 & 3: vertex and frag shader filenames
    private void ParseShaders()
    {
        var vert = (column[2].Equals("*")) ? Path.GetFileNameWithoutExtension(OwningRenderer.Config.VertexShaderPathname) : column[2];
        if (!vert.EndsWith(".vert", StringComparison.InvariantCultureIgnoreCase)) vert += ".vert";
        var vertPathname = PathHelper.FindFile(Program.AppConfig.VisualizerPath, vert);
        if (vertPathname is null) throw new ArgumentException($"{err} Failed to find vertex shader source file {vert}");

        var frag = (column[3].Equals("*")) ? Path.GetFileNameWithoutExtension(OwningRenderer.Config.FragmentShaderPathname) : column[3];
        if (!frag.EndsWith(".frag", StringComparison.InvariantCultureIgnoreCase)) frag += ".frag";
        var fragPathname = PathHelper.FindFile(Program.AppConfig.VisualizerPath, frag);
        if (fragPathname is null) throw new ArgumentException($"{err} Failed to find fragment shader source file {frag}");

        // when a --reload command is in effect, reload all shaders used by this renderer (save and restore the value)
        var replaceCachedShader = RenderingHelper.ReplaceCachedShader;
        ShaderPass.Shader = RenderingHelper.GetShader(OwningRenderer, vertPathname, fragPathname);
        if (!OwningRenderer.IsValid) return;
        RenderingHelper.ReplaceCachedShader = replaceCachedShader;
    }

    // column 4+: not defined, default to same as renderer's visualizer.conf
    private void UseDefaultVisualizer()
    {
        ShaderPass.Visualizer = RenderingHelper.GetVisualizer(OwningRenderer, OwningRenderer.Config);
        if (!OwningRenderer.IsValid) return;
        ShaderPass.Visualizer.Initialize(OwningRenderer.Config, ShaderPass.Shader);
    }

    // column 5: VisualizerVertexIntegerArray settings
    private void ParseVisualizerSettings()
    {
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

        (ShaderPass.Visualizer as VisualizerVertexIntegerArray).Initialize(intCount, drawMode, ShaderPass.Shader);
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
            resource.UniformName = $"input{resource.DrawbufferIndex}";
        }

        // allocate backbuffer resources
        if (MaxBackbuffer > -1)
        {
            BackbufferResources = RenderManager.ResourceManager.CreateResourceGroups(BackbufferOwnerName, MaxBackbuffer + 1, OwningRenderer.Resolution);

            // The DrawbufferIndex in the resource list is generated sequentially. This reassigns
            // them based on the order of first backbuffer usage in the multipass config section.
            int i = 0;
            foreach (var backbufferKey in BackbufferKeys)
            {
                var resource = BackbufferResources[i++];
                resource.DrawbufferIndex = backbufferKey.ToOrdinal();
                resource.UniformName = $"input{backbufferKey}";
            }

            // At this stage the list of InputBackbufferResources points to the drawbuffer
            // indexes (A = 0, B = 1, etc). This remaps them to the true backbuffer resource
            // index based on order of first usage.
            foreach (var pass in ShaderPasses)
            {
                if (pass.InputBackbufferResources.Count > 0)
                {
                    List<int> newList = new(pass.InputBackbufferResources.Count);
                    foreach (var index in pass.InputBackbufferResources)
                    {
                        var backbufferKey = index.ToAlpha();
                        var resourceIndex = BackbufferKeys.IndexOf(backbufferKey);
                        newList.Add(resourceIndex);
                    }
                    pass.InputBackbufferResources = newList;
                }
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
