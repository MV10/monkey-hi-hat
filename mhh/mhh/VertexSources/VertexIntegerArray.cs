
using eyecandy;
using Microsoft.Extensions.Logging;
using OpenTK.Graphics.OpenGL;

namespace mhh;

/// <summary>
/// This implementation is similar to VertexShaderArt. It's assumed the
/// vertex shader is the primary workload, and other than the default time
/// and resolution inputs, and any optional audio textures, the only input
/// is a single array of sequential integers of arbitrary length.
/// </summary>
public class VertexIntegerArray : IVertexSource
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

    private static readonly ILogger Logger = LogHelper.CreateLogger(nameof(VertexIntegerArray));

    // init from config via interface
    public void Initialize(IConfigSource config, Shader shader)
    {
        var count = config.ConfigSource
            .ReadValue(nameof(VertexIntegerArray), "VertexIntegerCount")
            .ToInt32(1000);

        var mode = config.ConfigSource
            .ReadValue(nameof(VertexIntegerArray), "ArrayDrawingMode")
            .ToEnum(ArrayDrawingMode.Points);

        Initialize(count, mode, shader);
    }

    // init with args (multipass renderer only knows these values, no .conf file available)
    public void Initialize(int vertexIntegerCount, ArrayDrawingMode arrayDrawingMode, Shader shader)
    {
        VertexIntegerCount = vertexIntegerCount;
        DrawingMode = Array.FindIndex(Modes, m => m.Equals(arrayDrawingMode.GetGLDrawingMode()));

        VertexIds = new float[VertexIntegerCount];
        for (var i = 0; i < VertexIntegerCount; i++)
        {
            VertexIds[i] = i;
        }

        VertexBufferObject = GL.GenBuffer();
        VertexArrayObject = GL.GenVertexArray();

        BindBuffers(shader);
    }

    public void BindBuffers(Shader shader)
    {
        shader.Use();

        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferObject);
        GL.BufferData(BufferTarget.ArrayBuffer, VertexIds.Length * sizeof(float), VertexIds, BufferUsageHint.StaticDraw);

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
        Logger?.LogTrace("Disposing");

        GL.DeleteBuffer(VertexBufferObject);
        GL.DeleteVertexArray(VertexArrayObject);

        IsDisposed = true;
        GC.SuppressFinalize(true);
    }
    private bool IsDisposed = false;
}
