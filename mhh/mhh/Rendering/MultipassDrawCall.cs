
namespace mhh;

/// <summary>
/// Each of these are the elements of a single draw call (aka pass). If Shader
/// or VertexSource is null, the "primary" defined in the [shader] section of
/// the visualizer config file is used. 
/// </summary>
public class MultipassDrawCall
{
    /// <summary>
    /// The zero-based number to draw into for this pass. This should only be 
    /// associated with the [multipass] draw buffer declarations, not resource list
    /// indexes.
    /// </summary>
    public int DrawbufferIndex;

    /// <summary>
    /// The active OpenGL resources to use for drawing. If Backbuffers is populated, these
    /// will be swapped at the end of each frame.
    /// </summary>
    public GLResourceGroup Drawbuffers;

    /// <summary>
    /// The OpenGL resources drawn into on the previous frame. If this is populated, it
    /// will be swapped with Drawbuffers at the end of each frame.
    /// </summary>
    public GLResourceGroup Backbuffers;

    /// <summary>
    /// Input-texture indexes rendered during earlier passes in the current frame. These
    /// have uniform names like "input0" and "input1" where the number corresponds to a
    /// rendering pass.
    /// </summary>
    public List<int> InputsDrawbuffers;

    /// <summary>
    /// Input-texture indexes rendered in the previous frame. These have uniform names
    /// like "inputA" and "inputB" where the letter corresponds to the 0-25 / A-Z mapping
    /// of a rendering pass number.
    /// </summary>
    public List<int> InputsBackbuffers;

    /// <summary>
    /// The shader which will render this pass.
    /// </summary>
    public CachedShader Shader;

    /// <summary>
    /// The object providing vertex data for this pass.
    /// </summary>
    public IVertexSource VertexSource;

    /// <summary>
    /// If the pass references a stand-alone visualizer config, any uniforms defined
    /// in that config will be stored here since the overall config is not stored.
    /// </summary>
    public Dictionary<string, float> Uniforms;
}
