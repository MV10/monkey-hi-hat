
using eyecandy;
using mhh.Hosting;
using mhh.Utils;
using Microsoft.Extensions.Logging;
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
        /// The current playlist, if any.
        /// </summary>
        public PlaylistConfig ActivePlaylist;

        /// <summary>
        /// Audio and texture processing by the eyecandy library.
        /// </summary>
        public AudioTextureEngine Eyecandy;

        /// <summary>
        /// Used by the renderer for the "time" uniform.
        /// </summary>
        public float TimeUniform;

        /// <summary>
        /// Used by the renderer for the "resolution" uniform. Updated every frame.
        /// </summary>
        public Vector2 ResolutionUniform;

        private Stopwatch Clock = new();

        private MethodInfo EyecandyEnableMethod;
        private MethodInfo EyecandyDisableMethod;
        private MethodInfo EyecandyMultiplierMethod;

        private bool CommandFlag_Paused = false;
        private bool CommandFlag_QuitRequested = false;

        private bool TrackingSilentPeriod = false;

        private object QueuedVisualizerLock = new();
        private VisualizerConfig QueuedVisualizerConfig = null;

        private int PlaylistPointer = 0;
        private DateTime PlaylistAdvanceAt = DateTime.MaxValue;
        private DateTime PlaylistIgnoreSilenceUntil = DateTime.MinValue;
        private Random rnd = new();

        private RenderManager Renderer = new();

        public HostWindow(EyeCandyWindowConfig windowConfig, EyeCandyCaptureConfig audioConfig)
            : base(windowConfig, createShaderFromConfig: false)
        {
            Eyecandy = new(audioConfig);
            EyecandyEnableMethod = Eyecandy.GetType().GetMethod("Enable");
            EyecandyDisableMethod = Eyecandy.GetType().GetMethod("Disable");
            EyecandyMultiplierMethod = Eyecandy.GetType().GetMethod("SetMultiplier");

            // TODO default these to enabled: false
            Eyecandy.Create<AudioTextureWaveHistory>("eyecandyWave", enabled: true);
            Eyecandy.Create<AudioTextureFrequencyDecibelHistory>("eyecandyFreqDB", enabled: true);
            Eyecandy.Create<AudioTextureFrequencyMagnitudeHistory>("eyecandyFreqMag", enabled: true);
            Eyecandy.Create<AudioTextureWebAudioHistory>("eyecandyWebAudio", enabled: true);
            Eyecandy.Create<AudioTextureShadertoy>("eyecandyShadertoy", enabled: true);
            Eyecandy.Create<AudioTexture4ChannelHistory>("eyecandy4Channel", enabled: true);
            Eyecandy.EvaluateRequirements();

            InitializeCache();

            // TODO use Renderer
            ForceIdleVisualization(true);

            Clock.Start();
        }

        /// <summary>
        /// The host window's base class sets the background color, then the active visualizer is invoked.
        /// </summary>
        protected override void OnLoad()
        {
            base.OnLoad();
            Eyecandy.BeginAudioProcessing();
        }

        /// <summary>
        /// The host window's base class clears the background, then the host window updates any audio
        /// textures and sets the audio texture uniforms, as well as the "resolution" and "time" uniforms,
        /// then invokes the active visualizer. Finally, buffers are swapped and FPS is calculated.
        /// </summary>
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            if (CommandFlag_Paused || CommandFlag_QuitRequested || Renderer.ActiveRenderer is null) return;

            base.OnRenderFrame(e);

            Eyecandy.UpdateTextures();
            TimeUniform = (float)Clock.Elapsed.TotalSeconds;
            ResolutionUniform = new(ClientSize.X, ClientSize.Y);

            Renderer.RenderFrame();

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
                Renderer?.Dispose();
                Close();
                return;
            }

            var input = KeyboardState;

            // ESC to quit
            if (input.IsKeyReleased(Keys.Escape))
            {
                // set the flag to ensure the render callback starts
                // short-circuiting before we start releasing stuff
                CommandFlag_QuitRequested = true;
                return;
            }

            // Right-arrow for next in playlist
            if (input.IsKeyReleased(Keys.Right) && ActivePlaylist is not null)
            {
                Command_PlaylistNext(temporarilyIgnoreSilence: true);
                return;
            }

            // silence detection handling
            double duration = 0;
            if(Eyecandy.IsSilent)
            {
                if(!TrackingSilentPeriod)
                {
                    TrackingSilentPeriod = true;
                }
                else
                {
                    duration = DateTime.Now.Subtract(Eyecandy.SilenceStarted).TotalSeconds;
                }
            }
            else
            {
                if(TrackingSilentPeriod)
                {
                    TrackingSilentPeriod = false;
                    duration = Eyecandy.SilenceEnded.Subtract(Eyecandy.SilenceStarted).TotalSeconds;
                }
            }

            // long-duration silence switches to the lower-overhead Idle or Blank viz
            if (Program.AppConfig.DetectSilenceSeconds > 0 && duration >= Program.AppConfig.DetectSilenceSeconds)
            {
                RespondToSilence(duration);
                return;
            }

            // short-duration silence for playlist track-change viz-advancement
            if(ActivePlaylist?.SwitchMode == PlaylistSwitchModes.Silence && DateTime.Now >= PlaylistIgnoreSilenceUntil && duration >= ActivePlaylist.SwitchSeconds)
            {
                Command_PlaylistNext(temporarilyIgnoreSilence: true);
                return;
            }

            // playlist viz-advancement by time
            if(DateTime.Now >= PlaylistAdvanceAt)
            {
                Command_PlaylistNext(temporarilyIgnoreSilence: true);
                return;
            }

            // if the CommandLineSwitchPipe thread processed a request
            // to use a different shader, process here where we know
            // the GLFW thread won't be trying to use the Shader object
            lock (QueuedVisualizerLock)
            {
                if(QueuedVisualizerConfig is not null)
                {
                    Renderer.PrepareNewRenderer(QueuedVisualizerConfig);
                    QueuedVisualizerConfig = null;
                    Clock.Restart();
                }
            }
        }

        /// <summary>
        /// Resets framebuffers to match the new viewport size.
        /// </summary>
        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            Renderer.ViewportResized(ClientSize.X, ClientSize.Y);
        }

        /// <summary>
        /// Sets the uniforms generally available to all shaders (resolution, time).
        /// </summary>
        public void SetStandardUniforms(Shader shader)
        {
            shader.SetUniform("resolution", ResolutionUniform);
            shader.SetUniform("time", TimeUniform);
        }

        /// <summary>
        /// Queues a new visualizer configuration to send to
        /// the RenderManager on the next OnUpdateFrame pass.
        /// </summary>
        public void QueueNextVisualizerConfig(VisualizerConfig newVisualizerConfig, bool replaceCachedShader = false)
        {
            lock(QueuedVisualizerLock)
            {
                // CommandLineSwitchPipe invokes this from another thread;
                // actual update occurs in OnUpdateFrame which is "safe"
                // because it won't be busy doing things like using the
                // current Shader object in an OnRenderFrame call.
                QueuedVisualizerConfig = newVisualizerConfig;

                // When the --reload command has been issued we want to compile a fresh copy.
                RenderingHelper.ReplaceCachedShader = replaceCachedShader;
            }
        }

        /// <summary>
        /// Handler for the --load command-line switch.
        /// </summary>
        public string Command_Load(string visualizerConfPathname, bool terminatesPlaylist = true)
        {
            var newViz = new VisualizerConfig(visualizerConfPathname);
            if (newViz.ConfigSource.Content.Count == 0)
            {
                var err = $"Unable to load visualizer configuration {newViz.ConfigSource.Pathname}";
                LogHelper.Logger?.LogError(err);
                return $"ERR: {err}";
            }
            if (terminatesPlaylist) ActivePlaylist = null;
            QueueNextVisualizerConfig(newViz);
            var msg = $"Loading {newViz.ConfigSource.Pathname}";
            LogHelper.Logger?.LogInformation(msg);
            return msg;
        }

        /// <summary>
        /// Loads and begins using a playlist.
        /// </summary>
        public string Command_Playlist(string playlistConfPathname)
        {
            ActivePlaylist = null;
            var cfg = new PlaylistConfig(playlistConfPathname);
            string err = null;
            if(cfg.Playlist.Length < 2) err = "Invalid playlist configuration file, one or zero visualizations loaded, aborted";
            if (cfg.Order == PlaylistOrder.RandomWeighted && cfg.Favorites.Count == 0) err = "RandomWeighted playlist requires Favorites visualizations, aborted";
            if(err is not null)
            {
                LogHelper.Logger?.LogError(err);
                return $"ERR: {err}";
            }
            PlaylistPointer = 0;
            ActivePlaylist = cfg;
            return Command_PlaylistNext();
        }

        /// <summary>
        /// Advances to the next visualization when a playlist is active.
        /// </summary>
        public string Command_PlaylistNext(bool temporarilyIgnoreSilence = false)
        {
            if(ActivePlaylist is null) return "ERR: No playlist is active";

            string filename;
            if(ActivePlaylist.Order == PlaylistOrder.RandomWeighted)
            {
                do
                {
                    if (rnd.Next(100) < 50 || rnd.Next(100) < ActivePlaylist.FavoritesPct)
                    {
                        filename = ActivePlaylist.Favorites[rnd.Next(ActivePlaylist.Favorites.Count)];
                    }
                    else
                    {
                        filename = ActivePlaylist.Visualizations[rnd.Next(ActivePlaylist.Visualizations.Count)];
                    }
                } while (filename.Equals(Renderer.ActiveRenderer.Filename));
            }
            else
            {
                filename = ActivePlaylist.Playlist[PlaylistPointer++];
                if (PlaylistPointer == ActivePlaylist.Playlist.Length)
                {
                    PlaylistPointer = 0;
                    ActivePlaylist.GeneratePlaylist();
                }
            }

            PlaylistAdvanceAt = (ActivePlaylist.SwitchMode == PlaylistSwitchModes.Time)
                ? DateTime.Now.AddSeconds(ActivePlaylist.SwitchSeconds)
                : (ActivePlaylist.SwitchMode == PlaylistSwitchModes.Silence)
                    ? DateTime.Now.AddSeconds(ActivePlaylist.MaxRunSeconds)
                    : DateTime.MaxValue;
            
            PlaylistIgnoreSilenceUntil = (temporarilyIgnoreSilence && ActivePlaylist.SwitchMode == PlaylistSwitchModes.Silence)
                ? DateTime.Now.AddSeconds(ActivePlaylist.SwitchCooldownSeconds)
                : DateTime.MinValue;

            var pathname = PathHelper.FindConfigFile(Program.AppConfig.ShaderPath, filename);
            if(pathname is not null)
            {
                var msg = Command_Load(pathname, terminatesPlaylist: false);
                // TODO handle ERR message
                return msg;
            }

            return $"ERR - {filename} not found in shader path(s)";
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
            // TODO call Renderer GetInfo
            var msg = $@"
elapsed sec: {Clock.Elapsed.TotalSeconds:0.####}
frame rate : {FramesPerSecond}
average fps: {AverageFramesPerSecond}
avg fps sec: {AverageFPSTimeframeSeconds}
visualizer : {Renderer.ActiveRenderer.Filename}
playlist   : {(ActivePlaylist is null ? "(none)" : ActivePlaylist.ConfigSource.Pathname )}
";
            LogHelper.Logger?.LogInformation(msg);
            return msg;
        }

        /// <summary>
        /// Handler for the --idle command-line switch.
        /// </summary>
        public string Command_Idle()
        {
            QueueNextVisualizerConfig(Program.AppConfig.IdleVisualizer);
            return "ACK";
        }

        /// <summary>
        /// Handler for the --pause command-line switch.
        /// </summary>
        public string Command_Pause()
        {
            if (CommandFlag_Paused) return "already paused; use --run to resume";
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
            Clock.Start();
            CommandFlag_Paused = false;
            return "ACK";
        }

        /// <summary>
        /// Handler for the --reload command-line switch.
        /// </summary>
        public string Command_Reload()
        {
            var filename = Renderer.ActiveRenderer.Filename;
            var pathname = PathHelper.FindConfigFile(Program.AppConfig.ShaderPath, filename);
            if (pathname is null) return $"ERR - {filename} not found in shader path(s)";

            var newViz = new VisualizerConfig(pathname);
            if (newViz.ConfigSource.Content.Count == 0)
            {
                var err = $"Unable to load visualizer configuration {newViz.ConfigSource.Pathname}";
                LogHelper.Logger?.LogError(err);
                return $"ERR: {err}";
            }

            QueueNextVisualizerConfig(newViz, replaceCachedShader: true);
            var msg = $"Reloading {newViz.ConfigSource.Pathname}";
            LogHelper.Logger?.LogInformation(msg);
            return msg;
        }

        /// <summary>
        /// Implements the configured action when silence is detected by OnUpdateFrame
        /// </summary>
        private void RespondToSilence(double duration)
        {
            ActivePlaylist = null;

            LogHelper.Logger?.LogDebug($"Silence detected (duration: {duration:0.####} sec");

            lock (QueuedVisualizerLock)
            {
                QueuedVisualizerConfig = Program.AppConfig.DetectSilenceAction switch
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
            if(!initializingWindow && Renderer.ActiveRenderer.Filename.Equals("idle", StringComparison.InvariantCultureIgnoreCase))
            {
                throw new Exception("Built-in idle visualizer has failed.");
            }
            Renderer.PrepareNewRenderer(Program.AppConfig.IdleVisualizer);
        }

        // Example of how to invoke generic method
        //private void CreateAudioTextures()
        //{
        //    if (Eyecandy is null || ActiveVisualizerConfig is null) return;
        //
        //    var defaultEnabled = (object)true;
        //
        //    foreach (var tex in ActiveVisualizerConfig.AudioTextureTypeNames)
        //    {
        //        // match the string value to one of the known Eyecandy types
        //        var TextureType = Caching.KnownAudioTextures.FindType(tex.Value);
        //
        //        // call the engine to create the object
        //        if (TextureType is not null)
        //        {
        //            var uniformName = (object)(ActiveVisualizerConfig.AudioTextureUniformNames.ContainsKey(tex.Key)
        //                ? ActiveVisualizerConfig.AudioTextureUniformNames[tex.Key]
        //                : tex.Value.ToString());
        //
        //            var multiplier = (object)(ActiveVisualizerConfig.AudioTextureMultipliers.ContainsKey(tex.Key)
        //                ? ActiveVisualizerConfig.AudioTextureMultipliers[tex.Key]
        //                : 1.0f);
        //
        //            // AudioTextureEngine.Create<TextureType>(uniform, assignedTextureUnit, multiplier, enabled)
        //            var method = EyecandyEnableMethod.MakeGenericMethod(TextureType);
        //            method.Invoke(Eyecandy, new object[]
        //            {
        //            uniformName,
        //            multiplier,
        //            defaultEnabled,
        //            });
        //        }
        //    }
        //
        //    Eyecandy.EvaluateRequirements();
        //}

        private void InitializeCache()
        {
            Caching.Shaders = new(Program.AppConfig.ShaderCacheSize);

            CacheInternalShader("idle");
            CacheInternalShader("blank");
            CacheInternalShader("crossfade");
        }

        /// <summary>
        /// The program exits if the shader pathname is invalid or compilation fails.
        /// </summary>
        private void CacheInternalShader(string name)
        {
            Shader shader = new(
                Path.Combine(ApplicationConfiguration.InternalShaderPath, $"{name}.vert"),
                Path.Combine(ApplicationConfiguration.InternalShaderPath, $"{name}.frag"));

            if (!shader.IsValid)
            {
                Console.WriteLine($"\n\nFATAL ERROR: Internal {name} shader was not found or failed to compile.\n\n");
                Thread.Sleep(250);
                Environment.Exit(-1);
            }

            Caching.InternalShaders.Add(name, shader);
        }

        private bool IsDisposed = false;
        public new void Dispose() // "new" hides the non-overridable base method
        {
            if (IsDisposed) return;
            base.Dispose();

            Eyecandy?.EndAudioProcessing_SynchronousHack();
            Renderer?.Dispose();
            Caching.InternalShaders.DisposeAndClear();
            Caching.Shaders.DisposeAndClear();
            Eyecandy?.Dispose();

            IsDisposed = true;
            GC.SuppressFinalize(true);
        }
    }
}
