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

        public void Start(HostWindow hostWindow)
        {
            if (!Enum.TryParse<ArrayDrawingMode>(hostWindow.ActiveVisualizer.Config.ReadValue("VisualizerVertexIntegerArray","ArrayDrawingMode"), out var mhhMode))
                mhhMode = ArrayDrawingMode.Points;

            DrawingMode = Array.FindIndex(Modes, m => m.Equals(mhhMode.GetGLDrawingMode()));

            VertexIntegerCount = hostWindow.ActiveVisualizer.Config.ReadValue("VisualizerVertexIntegerArray", "VertexIntegerCount").ToInt32(1000);
            VertexIds = new float[VertexIntegerCount];
            for (var i = 0; i < VertexIntegerCount; i++)
            {
                VertexIds[i] = i;
            }
        }

        public void Stop(HostWindow hostWindow)
        {
            // do nothing
        }

        public void OnLoad(HostWindow hostWindow)
        {
            VertexBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferObject);
            GL.BufferData(BufferTarget.ArrayBuffer, VertexIds.Length * sizeof(float), VertexIds, BufferUsageHint.StaticDraw);

            VertexArrayObject = GL.GenVertexArray();
            GL.BindVertexArray(VertexArrayObject);
            GL.VertexAttribPointer(0, 1, VertexAttribPointerType.Float, false, sizeof(float), 0);
            GL.EnableVertexAttribArray(0); // 0 = location of vertexId attribute
        }

        public void OnRenderFrame(HostWindow hostWindow, FrameEventArgs e)
        {
            hostWindow.Shader.SetUniform("vertexCount", (float)VertexIntegerCount);

            GL.BindVertexArray(VertexArrayObject);
            GL.DrawArrays(Modes[DrawingMode], 0, VertexIntegerCount);
        }

        public void OnUpdateFrame(HostWindow hostWindow, FrameEventArgs e)
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

        public string CommandLineArgument(HostWindow hostWindow, string command, string value)
        {
            var cmdMode = ArrayDrawingMode.Points;
            if (!command.ToLowerInvariant().Equals("mode") || !Enum.TryParse(value, out cmdMode))
                return "invalid command or value, try --help viz";
            
            DrawingMode = Array.FindIndex(Modes, m => m.Equals(cmdMode.GetGLDrawingMode()));

            return $"setting drawing mode {Modes[DrawingMode]}";
        }

        public List<(string command, string value)> CommandLineArgumentHelp()
        { 
            return new() 
            { 
                ("mode", "Points|Lines|LineStrip|LineLoop|Triangles|TriangleStrip|TriangleFan")
            };
        }
    }
}
