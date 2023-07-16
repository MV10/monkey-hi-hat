using eyecandy;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Diagnostics;
using System.Reflection;

namespace mhh
{
    /// <summary>
    /// The window owns the visualizer definition and instance, the eyecandy
    /// audio texture and capture processing, and supplies the "resolution"
    /// and "time" uniforms. The visualizers do most of the other work.
    /// </summary>
    public class HostWindow : BaseWindow
    {
        /// <summary>
        /// The current visualizer.
        /// </summary>
        public VisualizerConfig ActiveVisualizer;

        /// <summary>
        /// Audio and texture processing by the eyecandy library.
        /// </summary>
        public AudioTextureEngine Engine;

        private IVisualizer Visualizer;
        private bool OnLoadCompleted = false;
        private Stopwatch Clock = new();

        private MethodInfo EngineCreateTexture;
        private MethodInfo EngineDestroyTexture;
        private MethodInfo EngineSetMultiplier;

        public HostWindow(EyeCandyWindowConfig windowConfig, EyeCandyCaptureConfig audioConfig, CancellationToken cancellationToken)
            : base(windowConfig)
        {
            Engine = new(audioConfig);
            EngineCreateTexture = Engine.GetType().GetMethod("Create");
            EngineDestroyTexture = Engine.GetType().GetMethod("Destroy");
            EngineSetMultiplier = Engine.GetType().GetMethod("SetMultiplier");

            ActiveVisualizer = Program.AppConfig.IdleVisualizer;
            StartNewVisualizer();

            Clock.Start();
        }

        /// <summary>
        /// The host window's base class sets the background color, then the active visualizer is invoked.
        /// </summary>
        protected override void OnLoad()
        {
            base.OnLoad();
            Visualizer.OnLoad(this);
            Engine.BeginAudioProcessing();
            OnLoadCompleted = true;
        }

        /// <summary>
        /// The host window's base class clears the background, then the host window updates any audio
        /// textures and sets the audio texture uniforms, as well as the "resolution" and "time" uniforms,
        /// then invokes the active visualizer. Finally, buffers are swapped and FPS is calculated.
        /// </summary>
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            Engine.UpdateTextures();
            Engine.SetTextureUniforms(Shader);

            Shader.SetUniform("resolution", new Vector2(Size.X, Size.Y));
            Shader.SetUniform("time", (float)Clock.Elapsed.TotalSeconds);

            Visualizer.OnRenderFrame(this, e);

            SwapBuffers();
            CalculateFPS();
        }

        /// <summary>
        /// Processes the ESC key to exit the program, or invokes the active visualizer if
        /// ESC has not been pressed.
        /// </summary>
        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);

            var input = KeyboardState;

            if (input.IsKeyDown(Keys.Escape))
            {
                Engine.EndAudioProcessing_SynchronousHack();
                Visualizer.Stop(this);
                Close();
                return;
            }

            Visualizer.OnUpdateFrame(this, e);
        }

        public new void Dispose()
        {
            base.Dispose();
            Visualizer?.Dispose();
            Engine?.Dispose();
        }

        /// <summary>
        /// Preps and executes a new visualization
        /// </summary>
        public void ChangeVisualizer(VisualizerConfig newVisualizerConfig)
        {
            if (Visualizer is not null)
            {
                Visualizer.Stop(this);
                Visualizer.Dispose();
                Visualizer = null;
            }

            // TODO - any reason to stop / restart the audio texture engine?
            DestroyAudioTextures();

            ActiveVisualizer = newVisualizerConfig;
            StartNewVisualizer();
        }

        /// <summary>
        /// Starts up the visualizer defined by the ActiveVisualizer field. Any cleanup of the
        /// previous visualizer (such as DestroyAudioTextures) is assumed to have been done prior
        /// to calling this.
        /// </summary>
        private void StartNewVisualizer()
        {
            // if the type name isn't recognized, default to the idle viz
            if (!KnownTypes.Visualizers.Any(t => t.ToString().Equals(ActiveVisualizer.VisualizerTypeName)))
            {
                ActiveVisualizer = Program.AppConfig.IdleVisualizer;
            }

            CreateAudioTextures();

            var VizType = KnownTypes.Visualizers.FindType(ActiveVisualizer.VisualizerTypeName);
            Visualizer = Activator.CreateInstance(VizType) as IVisualizer;
            Shader = new(ActiveVisualizer.VertexShaderPathname, ActiveVisualizer.FragmentShaderPathname);
            Visualizer.Start(this);

            if (OnLoadCompleted)
            {
                Configuration.BackgroundColor = ActiveVisualizer.BackgroundColor;
                GL.ClearColor(Configuration.BackgroundColor);
                Visualizer.OnLoad(this);
            }
        }

        /// <summary>
        /// Calls Create on all types listed in the visualization definition.
        /// </summary>
        private void CreateAudioTextures()
        {
            // Enum Texture0 is some big weird number, but they increment serially from there
            int unit0 = (int)TextureUnit.Texture0;

            // Each entry in the AudioTextures list is "uniform AudioTextureType"
            foreach (var tex in ActiveVisualizer.AudioTextureTypeNames)
            {
                // match the string value to one of the known Eyecandy types
                var TextureType = KnownTypes.AudioTextureTypes.FindType(tex.Value);

                // call the engine to create the object
                if (TextureType is not null)
                {
                    // AudioTextureEngine.Create<TextureType>(uniform, assignedTextureUnit)
                    var method = EngineCreateTexture.MakeGenericMethod(TextureType);
                    method.Invoke(Engine, new object[]
                    {
                        (object)tex.Value,           // uniform name
                        (object)(tex.Key + unit0),   // unit# + TextureUnit.Texture0
                    });

                    // AudioTextureEngine.SetMultiplier<TextureType>(multiplier)
                    if(ActiveVisualizer.AudioTextureMultipliers.ContainsKey(tex.Key))
                    {
                        method = EngineSetMultiplier.MakeGenericMethod(TextureType);
                        method.Invoke(Engine, new object[]
                        {
                            (object)ActiveVisualizer.AudioTextureMultipliers[tex.Key]    // multiplier
                        });
                    }
                }
            }

            Engine.EvaluateRequirements();
        }

        /// <summary>
        /// The engine tracks audio textures by type, so loop through all known
        /// types and call Destroy on each; unused types will be ignored.
        /// </summary>
        private void DestroyAudioTextures()
        {
            foreach(var tex in ActiveVisualizer.AudioTextureTypeNames)
            {
                // AudioTextureEngine.Destroy<TextureType>()
                var TextureType = KnownTypes.AudioTextureTypes.FindType(tex.Value);
                var method = EngineDestroyTexture.MakeGenericMethod(TextureType);
                method.Invoke(Engine, null);
            }
        }

    }
}
