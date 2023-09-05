
using eyecandy;
using OpenTK.Graphics.OpenGL;

namespace mhh;

/// <summary>
/// This impelementation is similar to Shadertoy. The vertex shader must
/// declare "in vec3 vertices" and "in vec2 vertexTexCoords". Refer to
/// the eyecandy "frag" demo for an example. The vertex input data is a
/// pair of triangles forming a quad that covers the entire display area,
/// and it is assumed the primary work happens in the frag shader. There
/// are the two standard built-in uniforms "resolution" and "time". This
/// visualizer always uses the Triangles drawing mode.
/// </summary>
public class VisualizerFragmentQuad : IVisualizer
{
    // defines two triangles that cover the whole display area
    float[] vertices =
    {
        // position             texture coords
            1.0f,  1.0f, 0.0f,   1.0f, 1.0f,     // top right
            1.0f, -1.0f, 0.0f,   1.0f, 0.0f,     // bottom right
        -1.0f, -1.0f, 0.0f,   0.0f, 0.0f,     // bottom left
        -1.0f,  1.0f, 0.0f,   0.0f, 1.0f      // top left
    };

    // connect the dots (verts)
    private readonly uint[] indices =
    {
        0, 1, 3,
        1, 2, 3
    };

    private int ElementBufferObject;
    private int VertexBufferObject;
    private int VertexArrayObject;

    public void Initialize(VisualizerConfig config, Shader shader)
    {
        shader.Use();

        VertexArrayObject = GL.GenVertexArray();
        GL.BindVertexArray(VertexArrayObject);

        VertexBufferObject = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferObject);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

        ElementBufferObject = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ElementBufferObject);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

        var locationVertices = shader.GetAttribLocation("vertices");
        GL.EnableVertexAttribArray(locationVertices);
        GL.VertexAttribPointer(locationVertices, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);
        //                                       ^ 3 vertex is 3 floats                   ^ 5 per row        ^ 0 offset per row

        var locationTexCoords = shader.GetAttribLocation("vertexTexCoords");
        GL.EnableVertexAttribArray(locationTexCoords);
        GL.VertexAttribPointer(locationTexCoords, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));
        //                                        ^ tex coords is 2 floats                 ^ 5 per row        ^ 4th and 5th float in each row
    }

    public void RenderFrame(Shader shader)
    {
        GL.BindVertexArray(VertexArrayObject);
        GL.DrawElements(PrimitiveType.Triangles, indices.Length, DrawElementsType.UnsignedInt, 0);
    }

    public void Dispose()
    {
        if (IsDisposed) return;

        GL.DeleteVertexArray(VertexArrayObject);
        GL.DeleteBuffer(VertexBufferObject);
        GL.DeleteBuffer(ElementBufferObject);

        IsDisposed = true;
        GC.SuppressFinalize(true);
    }
    private bool IsDisposed = false;
}
