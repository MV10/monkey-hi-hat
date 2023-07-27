using eyecandy;
using Microsoft.Extensions.Logging;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Diagnostics;
using System.Reflection;
using System.Text;

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

        private bool CommandFlag_Paused = false;
        private bool CommandFlag_QuitRequested = false;

        private bool TrackingSilentPeriod = false;
        private bool RespondedToSilence = false;

        private object NewVisualizerLock = new();
        private VisualizerConfig NewVisualizer = null;

        public HostWindow(EyeCandyWindowConfig windowConfig, EyeCandyCaptureConfig audioConfig)
            : base(windowConfig, createShaderFromConfig: false)
        {
            Engine = new(audioConfig);
            EngineCreateTexture = Engine.GetType().GetMethod("Create");
            EngineDestroyTexture = Engine.GetType().GetMethod("Destroy");

            ForceIdleVisualization(true);

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
            if (CommandFlag_Paused || CommandFlag_QuitRequested || Visualizer is null || Shader is null) return;

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

            // either requested from the command line or the ESC
            // key on a previous pass in the next block of code
            if (CommandFlag_QuitRequested)
            {
                EndVisualization();
                Close();
                return;
            }

            // ESC to quit is the only keyboard input supported
            var input = KeyboardState;
            if (input.IsKeyDown(Keys.Escape))
            {
                // set the flag to ensure the render callback starts
                // short-circuiting before we start releasing stuff
                CommandFlag_QuitRequested = true;
                return;
            }

            // silence detection handling
            if(Program.AppConfig.DetectSilenceSeconds > 0)
            {
                double duration = 0;
                if(Engine.IsSilent)
                {
                    if(!TrackingSilentPeriod)
                    {
                        TrackingSilentPeriod = true;
                        RespondedToSilence = false;
                    }
                    else
                    {
                        if (!RespondedToSilence) duration = DateTime.Now.Subtract(Engine.SilenceStarted).TotalSeconds;
                    }
                }
                else
                {
                    if(TrackingSilentPeriod)
                    {
                        TrackingSilentPeriod = false;
                        if (!RespondedToSilence) duration = Engine.SilenceEnded.Subtract(Engine.SilenceStarted).TotalSeconds;
                    }
                }

                if (duration >= Program.AppConfig.DetectSilenceSeconds)
                {
                    RespondToSilence(duration);
                    return;
                }
            }

            // if the CommandLineSwitchPipe thread processed a request
            // to use a different shader, process that here where we know
            // the GLFW thread won't be trying to use the Shader object
            lock (NewVisualizerLock)
            {
                if(NewVisualizer is not null)
                {
                    EndVisualization();

                    ActiveVisualizer = NewVisualizer;
                    NewVisualizer = null;

                    StartNewVisualizer();
                    Clock.Restart();
                    Engine.BeginAudioProcessing();
                }
            }

            Visualizer.OnUpdateFrame(this, e);
        }

        public new void Dispose()
        {
            Engine?.Dispose();
            base.Dispose(); // disposes the Shader
        }

        /// <summary>
        /// Preps and executes a new visualization
        /// </summary>
        public void ChangeVisualizer(VisualizerConfig newVisualizerConfig)
        {
            lock(NewVisualizerLock)
            {
                // CommandLineSwitchPipe invokes this from another thread;
                // actual update occurs in OnUpdateFrame which is "safe"
                // because it won't be busy doing things like using the
                // current Shader object in an OnRenderFrame call.
                NewVisualizer = newVisualizerConfig;
            }
        }

        /// <summary>
        /// Handler for the --load command-line switch.
        /// </summary>
        public string Command_Load(string visualizerConfPathname)
        {
            var newViz = new VisualizerConfig(visualizerConfPathname);
            if (newViz.Config.Content.Count == 0)
            {
                var err = $"Unable to load visualizer configuration {newViz.Config.Pathname}";
                LogHelper.Logger?.LogError(err);
                return err;
            }
            ChangeVisualizer(newViz);
            var msg = $"Loading {newViz.Config.Pathname}";
            LogHelper.Logger?.LogInformation(msg);
            return msg;
        }

        /// <summary>
        /// Handler for the --quit command-line switch.
        /// </summary>
        public string Command_Quit()
        {
            CommandFlag_QuitRequested = true;
            return "ACK";
        }

        /// <summary>
        /// Handler for the --info command-line switch.
        /// </summary>
        public string Command_Info()
        {
            var msg = $@"
elapsed sec: {Clock.Elapsed.TotalSeconds:0.####}
frame rate : {FramesPerSecond}
average fps: {AverageFramesPerSecond}
avg fps sec: {AverageFPSTimeframeSeconds}
description: {ActiveVisualizer.Description}
config file: {ActiveVisualizer.Config.Pathname}
vert shader: {ActiveVisualizer.VertexShaderPathname}
frag shader: {ActiveVisualizer.FragmentShaderPathname}
vizualizer : {ActiveVisualizer.VisualizerTypeName}
";
            LogHelper.Logger?.LogInformation(msg);
            return msg;
        }

        /// <summary>
        /// Handler for the --idle command-line switch.
        /// </summary>
        public string Command_Idle()
        {
            ChangeVisualizer(Program.AppConfig.IdleVisualizer);
            return "ACK";
        }

        /// <summary>
        /// Handler for the --pause command-line switch.
        /// </summary>
        public string Command_Pause()
        {
            if (CommandFlag_Paused) return "already paused; use --run to resume";
            Visualizer?.Stop(this);
            Clock.Stop();
            CommandFlag_Paused = true;
            return "ACK";
        }

        /// <summary>
        /// Handler for the --run command-line switch.
        /// </summary>
        public string Command_Run()
        {
            if (!CommandFlag_Paused) return "already running; use --pause to suspend";
            Visualizer?.Start(this);
            Clock.Start();
            CommandFlag_Paused = false;
            return "ACK";
        }

        /// <summary>
        /// Handler for the --reload command-line switch.
        /// </summary>
        public string Command_Reload()
        {
            ChangeVisualizer(new(ActiveVisualizer.Config.Pathname));
            return "ACK";
        }

        /// <summary>
        /// Pass-through for IVisualizer.CommandLineArgument
        /// </summary>
        public string Command_VizCommand(string command, string value)
            => Visualizer.CommandLineArgument(this, command, value);

        /// <summary>
        /// Pass-through for IVisualizer.CommandLineArgumentHelp
        /// </summary>
        public List<(string command, string value)> Command_VizHelp()
            => Visualizer.CommandLineArgumentHelp();

        /// <summary>
        /// Implements the configured action when silence is detected by OnUpdateFrame
        /// </summary>
        private void RespondToSilence(double duration)
        {
            RespondedToSilence = true;

            LogHelper.Logger?.LogDebug($"Silence detected (duration: {duration:0.####} sec");

            lock (NewVisualizerLock)
            {
                NewVisualizer = Program.AppConfig.DetectSilenceAction switch
                {
                    SilenceAction.Blank => Program.AppConfig.BlankVisualizer,
                    SilenceAction.Idle => Program.AppConfig.IdleVisualizer,
                };
            }
        }

        /// <summary>
        /// Mostly an emergency bail-out for StartNewVisualizer.
        /// </summary>
        private void ForceIdleVisualization(bool initializingWindow)
        {
            if(!initializingWindow && ActiveVisualizer == Program.AppConfig.IdleVisualizer)
            {
                throw new Exception("Built-in idle visualizer has failed.");
            }
            if(!initializingWindow) EndVisualization();
            ActiveVisualizer = Program.AppConfig.IdleVisualizer;
            StartNewVisualizer();
        }

        /// <summary>
        /// Prepartion for starting a new viz, or for exiting the program.
        /// </summary>
        private void EndVisualization()
        {
            Visualizer?.Stop(this);
            Visualizer?.Dispose();
            Visualizer = null;
            Shader?.Dispose();
            Shader = null; // eyecandy 1.0.1 should be able to handle this now
            Engine?.EndAudioProcessing_SynchronousHack();
            DestroyAudioTextures();
        }

        /// <summary>
        /// Starts up the visualizer defined by the ActiveVisualizer field. Any cleanup of the
        /// previous visualizer (such as DestroyAudioTextures) is assumed to have been done prior
        /// to calling this (see OnUpdateFrame where this is addressed).
        /// </summary>
        private void StartNewVisualizer()
        {
            // if the type name isn't recognized, default to the idle viz
            var VizType = KnownTypes.Visualizers.FindType(ActiveVisualizer.VisualizerTypeName);
            if (VizType is null)
            {
                LogHelper.Logger.LogError($"Visualizer type not recognized: {ActiveVisualizer.VisualizerTypeName}; using default idle-viz.");
                ForceIdleVisualization(false);
                return;
            }
            Visualizer = Activator.CreateInstance(VizType) as IVisualizer;

            CreateAudioTextures();

            Shader = new(ActiveVisualizer.VertexShaderPathname, ActiveVisualizer.FragmentShaderPathname);
            if(!Shader.IsValid)
            {
                LogHelper.Logger.LogError($"Shader not valid for {ActiveVisualizer.Config.Pathname}; using default idle-viz.");
                ForceIdleVisualization(false);
                return;
            }

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
            if (Engine is null || ActiveVisualizer is null) return;

            // Enum Texture0 is some big weird number, but they increment serially from there
            int unit0 = (int)TextureUnit.Texture0;

            var defaultEnabled = (object)true;

            foreach (var tex in ActiveVisualizer.AudioTextureTypeNames)
            {
                // match the string value to one of the known Eyecandy types
                var TextureType = KnownTypes.AudioTextureTypes.FindType(tex.Value);

                // call the engine to create the object
                if (TextureType is not null)
                {
                    var textureUnit = (object)(TextureUnit)(tex.Key + unit0);

                    var uniformName = (object)(ActiveVisualizer.AudioTextureUniformNames.ContainsKey(tex.Key)
                        ? ActiveVisualizer.AudioTextureUniformNames[tex.Key]
                        : tex.Value.ToString());

                    var multiplier = (object)(ActiveVisualizer.AudioTextureMultipliers.ContainsKey(tex.Key)
                        ? ActiveVisualizer.AudioTextureMultipliers[tex.Key]
                        : 1.0f);

                    // AudioTextureEngine.Create<TextureType>(uniform, assignedTextureUnit, multiplier, enabled)
                    var method = EngineCreateTexture.MakeGenericMethod(TextureType);
                    method.Invoke(Engine, new object[]
                    {
                    uniformName,
                    textureUnit,
                    multiplier,
                    defaultEnabled,
                    });
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
            if (Engine is null || ActiveVisualizer is null) return;

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
