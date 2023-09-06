
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
    public int DrawBufferHandle;
    public List<int> InputTextureHandle;
    public List<TextureUnit> InputTextureUnit;
    public CachedShader Shader;
    public IVisualizer Visualizer;

    // Data collected during parsing
    public int DrawBufferIndex;
    public List<int> InputBufferIndex;
}
