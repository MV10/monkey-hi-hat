
using eyecandy;
using OpenTK.Graphics.OpenGL;

namespace mhh;

/// <summary>
/// This implementation is similar to VertexShaderArt. It's assumed the
/// vertex shader is the primary workload, and other than the default time
/// and resolution inputs, and any optional audio textures, the only input
/// is a single array of sequential integers of arbitrary length.
/// </summary>
public class VisualizerVertexIntegerArray : IVisualizer
{
    private int VertexIntegerCount;
    private float[] VertexIds;
    private int VertexBufferObject;
    private int VertexArrayObject;

    private int DrawingMode = 0;
    private readonly PrimitiveType[] Modes = new[]
    {
        PrimitiveType.Points,
        PrimitiveType.Lines,
        PrimitiveType.LineStrip,
        PrimitiveType.LineLoop,
        PrimitiveType.Triangles,
        PrimitiveType.TriangleStrip,
        PrimitiveType.TriangleFan,
    };

    // via interface
    public void Initialize(VisualizerConfig config, Shader shader)
    {
        var count = config.ConfigSource
            .ReadValue("VisualizerVertexIntegerArray", "VertexIntegerCount")
            .ToInt32(1000);

        var mode = config.ConfigSource
            .ReadValue("VisualizerVertexIntegerArray", "ArrayDrawingMode")
            .ToEnum(ArrayDrawingMode.Points);

        DirectInit(count, mode, shader);
    }

    // used by multipass renderer
    public void DirectInit(int vertexIntegerCount, ArrayDrawingMode arrayDrawingMode, Shader shader)
    {
        shader.Use();

        VertexIntegerCount = vertexIntegerCount;
        DrawingMode = Array.FindIndex(Modes, m => m.Equals(arrayDrawingMode.GetGLDrawingMode()));

        VertexIds = new float[VertexIntegerCount];
        for (var i = 0; i < VertexIntegerCount; i++)
        {
            VertexIds[i] = i;
        }

        VertexBufferObject = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferObject);
        GL.BufferData(BufferTarget.ArrayBuffer, VertexIds.Length * sizeof(float), VertexIds, BufferUsageHint.StaticDraw);

        VertexArrayObject = GL.GenVertexArray();
        GL.BindVertexArray(VertexArrayObject);
        GL.VertexAttribPointer(0, 1, VertexAttribPointerType.Float, false, sizeof(float), 0);
        GL.EnableVertexAttribArray(0); // 0 = location of vertexId attribute
    }

    public void RenderFrame(Shader shader)
    {
        shader.SetUniform("vertexCount", (float)VertexIntegerCount);

        GL.BindVertexArray(VertexArrayObject);
        GL.DrawArrays(Modes[DrawingMode], 0, VertexIntegerCount);
    }

    public void Dispose()
    {
        if (IsDisposed) return;

        GL.DeleteVertexArray(VertexArrayObject);
        GL.DeleteBuffer(VertexBufferObject);

        IsDisposed = true;
        GC.SuppressFinalize(true);
    }
    private bool IsDisposed = false;
}
