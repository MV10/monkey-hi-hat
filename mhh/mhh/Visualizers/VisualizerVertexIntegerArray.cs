using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace mhh
{
    /// <summary>
    /// This implementation is similar to VertexShaderArt. It's assumed the
    /// vertex shader is the primary workload, and other than the default time
    /// and resolution inputs, and any optional audio textures, the only input
    /// is a single array of sequential integers of arbitrary length.
    /// </summary>
    public class VisualizerVertexIntegerArray : IVisualizer
    {
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

        public void Start(VisualizerHostWindow hostWindow)
        {
            var glDrawingMode = hostWindow.Definition.GLDrawingMode();
            DrawingMode = Array.FindIndex(Modes, m => m.Equals(glDrawingMode));

            VertexIds = new float[hostWindow.Definition.VertexIntegerCount];
            for (var i = 0; i < hostWindow.Definition.VertexIntegerCount; i++)
            {
                VertexIds[i] = i;
            }
        }

        public void Stop(VisualizerHostWindow hostWindow)
        {
            // do nothing
        }

        public void OnLoad(VisualizerHostWindow hostWindow)
        {
            VertexBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferObject);
            GL.BufferData(BufferTarget.ArrayBuffer, VertexIds.Length * sizeof(float), VertexIds, BufferUsageHint.StaticDraw);

            VertexArrayObject = GL.GenVertexArray();
            GL.BindVertexArray(VertexArrayObject);
            GL.VertexAttribPointer(0, 1, VertexAttribPointerType.Float, false, sizeof(float), 0);
            GL.EnableVertexAttribArray(0); // 0 = location of vertexId attribute
        }

        public void OnRenderFrame(VisualizerHostWindow hostWindow, FrameEventArgs e)
        {
            hostWindow.Shader.SetUniform("vertexCount", (float)hostWindow.Definition.VertexIntegerCount);

            GL.BindVertexArray(VertexArrayObject);
            GL.DrawArrays(Modes[DrawingMode], 0, hostWindow.Definition.VertexIntegerCount);
        }

        public void OnUpdateFrame(VisualizerHostWindow hostWindow, FrameEventArgs e)
        {
            var input = hostWindow.KeyboardState;

            // Not really useful running non-interactively, but fun and perhaps useful for testing.
            if (input.IsKeyReleased(Keys.Space))
            {
                DrawingMode++;
                if (DrawingMode == Modes.Length) DrawingMode = 0;
            }
        }

        public void Dispose()
        {
            // do nothing
        }
    }
}
