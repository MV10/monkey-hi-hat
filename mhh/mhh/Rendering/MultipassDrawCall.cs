
using mhh.Utils;
using OpenTK.Graphics.OpenGL;

namespace mhh;

/// <summary>
/// Each of these are the elements of a single draw call (aka pass). If Shader
/// or Visualizer is null, the "primary" defined in the [shader] section of
/// the visualizer config file is used. 
/// </summary>
public class MultipassDrawCall
{
    // Data used during rendering
    public int DrawbufferHandle;
    public int BackbufferHandle;
    public List<int> InputTextureHandle;
    public List<TextureUnit> InputTextureUnit;
    public List<string> InputTextureUniform;
    public CachedShader Shader;
    public IVisualizer Visualizer;

    // Data collected during parsing
    public int ParseDrawbufferIndex;
    public List<int> ParseInputDrawbufferIndex;
    public List<string> ParseInputBackbufferKey;
}
