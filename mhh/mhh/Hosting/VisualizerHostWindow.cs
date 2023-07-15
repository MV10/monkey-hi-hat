using eyecandy;
using OpenTK.Graphics.ES11;
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
    public class VisualizerHostWindow : BaseWindow
    {
        /// <summary>
        /// Settings related to the active visualizer.
        /// </summary>
        public VisualizerDefinition Definition;

        /// <summary>
        /// Audio and texture processing by the eyecandy library.
        /// </summary>
        public AudioTextureEngine Engine;

        private IVisualizer Visualizer;
        private Stopwatch Clock = new();

        private MethodInfo EngineCreateTexture;
        private MethodInfo EngineDestroyTexture;

        public VisualizerHostWindow(EyeCandyWindowConfig windowConfig, EyeCandyCaptureConfig audioConfig, CancellationToken cancellationToken)
            : base(windowConfig)
        {
            Engine = new(audioConfig);
            EngineCreateTexture = Engine.GetType().GetMethod("Create");
            EngineDestroyTexture = Engine.GetType().GetMethod("Destroy");

            ProcessNewVisualizerDefinition(new());
            Visualizer.Start(this);

            Clock.Start();
        }

        /// <summary>
        /// The host window's base class sets the background color, then the active visualizer is invoked.
        /// </summary>
        protected override void OnLoad()
        {
            base.OnLoad();
            
            // TODO - should OnLoad be called here or in ProcessNewVisualizerDefinition?
            Visualizer.OnLoad(this);
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
            Visualizer.Dispose();
            Engine.Dispose();
        }

        private void ProcessNewVisualizerDefinition(VisualizerDefinition newDefinition)
        {
            // TODO - set GL.ClearColor since OnLoad won't run again? can we do that during window ctor?
            Definition = newDefinition;

            DestroyAudioTextures(); // out with the old...
            CreateAudioTextures();  // ...in with the new

            Visualizer = Definition.VisualizationMode switch
            {
                VizMode.VertexIntegerArray => new VisualizerVertexIntegerArray(),
                VizMode.FragmentQuad => new VisualizerFragmentQuad(),
            };

            // TODO - call OnLoad if the window has already loaded
            //Visualizer.OnLoad(this);
        }

        /// <summary>
        /// Calls Create on all types listed in the visualization definition.
        /// </summary>
        private void CreateAudioTextures()
        {
            // Enum Texture0 is some big weird number, but they increment serially from there
            int unit = (int)TextureUnit.Texture0;

            // Each entry in the AudioTextures list is "uniform AudioTextureType"
            foreach (var texDef in Definition.AudioTextures)
            {
                // 0 = uniform, 1 = AudioTexture type-name
                var texInfo = texDef.Split(' ');
                if(texInfo.Length == 2)
                {
                    // match the string value to one of the known Eyecandy types
                    var TextureType = Defaults.AudioTextureTypes
                        .FirstOrDefault(t => 
                            t.ToString().ToLowerInvariant()
                            .Equals(texInfo[1].ToLowerInvariant()));
                    
                    if(TextureType is not null)
                    {
                        object[] args = new[]
                        {
                            (object)texInfo[0], // uniform name
                            (object)unit,       // unit + TextureUnit.Texture0
                        };

                        var method = EngineCreateTexture.MakeGenericMethod(TextureType);

                        // AudioTextureEngine.Create<TextureType>(uniform, assignedTextureUnit)
                        method.Invoke(Engine, args);

                        unit++;
                    }
                }
            }
        }

        /// <summary>
        /// The engine tracks audio textures by type, so loop through all known
        /// types and call Destroy on each; unused types will be ignored.
        /// </summary>
        private void DestroyAudioTextures()
        {
            foreach(var TextureType in Defaults.AudioTextureTypes)
            {
                var method = EngineDestroyTexture.MakeGenericMethod(TextureType);

                // AudioTextureEngine.Destroy<TextureType>()
                method.Invoke(Engine, null);
            }
        }
    }
}
