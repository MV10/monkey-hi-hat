
using mhh.Utils;

namespace mhh;

/// <summary>
/// Each of these are the elements of a single draw call (aka pass). If Shader
/// or Visualizer is null, the "primary" defined in the [shader] section of
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
    public GLResources Drawbuffers;

    /// <summary>
    /// The OpenGL resources drawn into on the previous frame. If this is populated, it
    /// will be swapped with Drawbuffers at the end of each frame.
    /// </summary>
    public GLResources Backbuffers;

    /// <summary>
    /// Input-texture indexes rendered during earlier passes in the current frame. These
    /// have uniform names like "input0" and "input1" where the number corresponds to a
    /// Buffer ID.
    /// </summary>
    public List<int> InputFrontbufferResources;

    /// <summary>
    /// Input-texture indexes rendered in the previous frame. These have uniform names
    /// like "inputA" and "inputB" where the letter corresponds to the 0-25 / A-Z mapping
    /// of a Buffer ID.
    /// </summary>
    public List<int> InputBackbufferResources;

    /// <summary>
    /// The shader which will render this pass.
    /// </summary>
    public CachedShader Shader;

    /// <summary>
    /// The visualizer providing content for this pass.
    /// </summary>
    public IVisualizer Visualizer;
}
