
using eyecandy;
using mhh.Utils;
using Microsoft.Extensions.Logging;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Reflection;

namespace mhh
{
    /// <summary>
    /// The window owns the visualizer definition and instance, the eyecandy
    /// audio texture and capture processing, and supplies the "resolution"
    /// and "time" uniforms. The visualizers do most of the other work.
    /// </summary>
    public class HostWindow : BaseWindow, IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Handles all visualization rendering prep and execution.
        /// </summary>
        public RenderManager Renderer = new();

        /// <summary>
        /// Handles playlist content.
        /// </summary>
        public PlaylistManager Playlist = new();

        /// <summary>
        /// Audio and texture processing by the eyecandy library.
        /// </summary>
        public AudioTextureEngine Eyecandy;

        private MethodInfo EyecandyEnableMethod;
        private MethodInfo EyecandyDisableMethod;
        // Example of how to invoke generic method
        //    // AudioTextureEngine.Create<TextureType>(uniform, assignedTextureUnit, multiplier, enabled)
        //    var method = EyecandyCreateMethod.MakeGenericMethod(TextureType);
        //    method.Invoke(Eyecandy, new object[]
        //    {
        //        uniformName,
        //        multiplier,
        //        defaultEnabled,
        //    });


        private CommandRequest CommandRequested = CommandRequest.None;
        private bool IsPaused = false;

        private bool TrackingSilentPeriod = false;

        private object QueuedVisualizerLock = new();
        private VisualizerConfig QueuedVisualizerConfig = null;

        public HostWindow(EyeCandyWindowConfig windowConfig, EyeCandyCaptureConfig audioConfig)
            : base(windowConfig, createShaderFromConfig: false)
        {
            Eyecandy = new(audioConfig);
            EyecandyEnableMethod = Eyecandy.GetType().GetMethod("Enable");
            EyecandyDisableMethod = Eyecandy.GetType().GetMethod("Disable");

            // TODO default these to enabled: false
            Eyecandy.Create<AudioTextureWaveHistory>("eyecandyWave", enabled: true);
            Eyecandy.Create<AudioTextureFrequencyDecibelHistory>("eyecandyFreqDB", enabled: true);
            Eyecandy.Create<AudioTextureFrequencyMagnitudeHistory>("eyecandyFreqMag", enabled: true);
            Eyecandy.Create<AudioTextureWebAudioHistory>("eyecandyWebAudio", enabled: true);
            Eyecandy.Create<AudioTextureShadertoy>("eyecandyShadertoy", enabled: true);
            Eyecandy.Create<AudioTexture4ChannelHistory>("eyecandy4Channel", enabled: true);
            Eyecandy.Create<AudioTextureVolumeHistory>("eyecandyVolume", enabled: true);
            Eyecandy.EvaluateRequirements();

            InitializeCache();
        }

        /// <summary>
        /// The host window's base class sets the background color, then the active visualizer is invoked.
        /// </summary>
        protected override void OnLoad()
        {
            base.OnLoad();
            GL.Enable(EnableCap.ProgramPointSize);
            Renderer.PrepareNewRenderer(Caching.IdleVisualizer);
            Eyecandy.BeginAudioProcessing();
        }

        /// <summary>
        /// The host window's base class clears the background, then the host window updates any audio
        /// textures and sets the audio texture uniforms, as well as the "resolution" and "time" uniforms,
        /// then invokes the active visualizer. Finally, buffers are swapped and FPS is calculated.
        /// </summary>
        protected override void OnRenderFrame(FrameEventArgs e)
        {
            if (CommandRequested != CommandRequest.None || Renderer.ActiveRenderer is null) return;
            base.OnRenderFrame(e);
            Eyecandy.UpdateTextures();
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

            switch(CommandRequested)
            {
                case CommandRequest.Quit:
                {
                    CommandRequested = CommandRequest.None;
                    Renderer?.Dispose();
                    Close();
                    return;
                }

                case CommandRequest.ToggleFullscreen:
                {
                    switch(WindowState)
                    {
                        case WindowState.Fullscreen:
                            WindowState = WindowState.Normal;
                            break;

                        case WindowState.Normal:
                            WindowState = WindowState.Fullscreen;
                            break;

                        default:
                            break;
                    }
                    CommandRequested = CommandRequest.None;
                    return;
                }

                default:
                    CommandRequested = CommandRequest.None;
                    break;
            }

            var input = KeyboardState;

            // ESC to quit
            if (input.IsKeyReleased(Keys.Escape))
            {
                // set the flag to ensure the render callback starts
                // short-circuiting before we start releasing stuff
                CommandRequested = CommandRequest.Quit;
                return;
            }

            // Right-arrow for next in playlist
            if (input.IsKeyReleased(Keys.Right))
            {
                Command_PlaylistNext(temporarilyIgnoreSilence: true);
                return;
            }

            // Spacebar to toggle full-screen mode
            if (input.IsKeyReleased(Keys.Space))
            {
                CommandRequested = CommandRequest.ToggleFullscreen;
                return;
            }

            double duration = DetectSilence();
            if (Program.AppConfig.DetectSilenceSeconds > 0 && duration >= Program.AppConfig.DetectSilenceSeconds)
            {
                // long-duration silence switches to the lower-overhead Idle or Blank viz
                RespondToSilence(duration);
                return;
            }

            // playlists can be configured to advance after a short duration of silence
            Playlist.UpdateFrame(duration);

            // if the CommandLineSwitchPipe thread processed a request
            // to use a different shader, process here where we know
            // the GLFW thread won't be trying to use the Shader object
            lock (QueuedVisualizerLock)
            {
                if(QueuedVisualizerConfig is not null)
                {
                    Renderer.PrepareNewRenderer(QueuedVisualizerConfig);
                    QueuedVisualizerConfig = null;
                }
            }
        }

        /// <summary>
        /// Resets framebuffers to match the new viewport size.
        /// </summary>
        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            Renderer.OnResize();
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
            if (terminatesPlaylist) Playlist.TerminatePlaylist();
            QueueNextVisualizerConfig(newViz);
            var msg = $"Loading {newViz.ConfigSource.Pathname}";
            LogHelper.Logger?.LogInformation(msg);
            return msg;
        }

        /// <summary>
        /// Loads and begins using a playlist.
        /// </summary>
        public string Command_Playlist(string playlistConfPathname)
            => Playlist.StartNewPlaylist(playlistConfPathname);

        /// <summary>
        /// Advances to the next visualization when a playlist is active.
        /// </summary>
        public string Command_PlaylistNext(bool temporarilyIgnoreSilence = false)
            => Playlist.NextVisualization(temporarilyIgnoreSilence);

        /// <summary>
        /// Handler for the --quit command-line switch.
        /// </summary>
        public string Command_Quit()
        {
            CommandRequested = CommandRequest.Quit;
            return "ACK";
        }

        /// <summary>
        /// Handler for the --info command-line switch.
        /// </summary>
        public string Command_Info()
        {
            var msg = $@"
frame rate : {FramesPerSecond}
average fps: {AverageFramesPerSecond}
avg fps sec: {AverageFPSTimeframeSeconds}
playlist   : {Playlist.GetInfo()}
{Renderer.GetInfo()}
";
            LogHelper.Logger?.LogInformation(msg);
            return msg;
        }

        /// <summary>
        /// Handler for the --fullscreen command-line switch.
        /// </summary>
        public string Command_FullScreen()
        {
            CommandRequested = CommandRequest.ToggleFullscreen;
            return "ACK";
        }

        /// <summary>
        /// Handler for the --idle command-line switch.
        /// </summary>
        public string Command_Idle()
        {
            QueueNextVisualizerConfig(Caching.IdleVisualizer);
            return "ACK";
        }

        /// <summary>
        /// Handler for the --pause command-line switch.
        /// </summary>
        public string Command_Pause()
        {
            if (IsPaused) return "already paused; use --run to resume";
            Renderer.TimePaused = true;
            IsPaused = true;
            return "ACK";
        }

        /// <summary>
        /// Handler for the --run command-line switch.
        /// </summary>
        public string Command_Run()
        {
            if (!IsPaused) return "already running; use --pause to suspend";
            Renderer.TimePaused = false;
            IsPaused = false;
            return "ACK";
        }

        /// <summary>
        /// Handler for the --reload command-line switch.
        /// </summary>
        public string Command_Reload()
        {
            var filename = Renderer.ActiveRenderer.Filename;
            var pathname = PathHelper.FindConfigFile(Program.AppConfig.VisualizerPath, filename);
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

        private double DetectSilence()
        {
            if (Eyecandy.IsSilent)
            {
                if (!TrackingSilentPeriod)
                {
                    TrackingSilentPeriod = true;
                }
                else
                {
                    return DateTime.Now.Subtract(Eyecandy.SilenceStarted).TotalSeconds;
                }
            }
            else
            {
                if (TrackingSilentPeriod)
                {
                    TrackingSilentPeriod = false;
                    return Eyecandy.SilenceEnded.Subtract(Eyecandy.SilenceStarted).TotalSeconds;
                }
            }

            return 0;
        }

        /// <summary>
        /// Implements the configured action when silence is detected by OnUpdateFrame
        /// </summary>
        private void RespondToSilence(double duration)
        {
            LogHelper.Logger?.LogDebug($"Long-term silence detected (duration: {duration:0.####} sec");

            Playlist.TerminatePlaylist();

            lock (QueuedVisualizerLock)
            {
                QueuedVisualizerConfig = Program.AppConfig.DetectSilenceAction switch
                {
                    SilenceAction.Blank => Caching.BlankVisualizer,
                    SilenceAction.Idle => Caching.IdleVisualizer,
                };
            }
        }

        private void InitializeCache()
        {
            Caching.Shaders = new(Program.AppConfig.ShaderCacheSize);

            Caching.IdleVisualizer = new(Path.Combine(ApplicationConfiguration.InternalShaderPath, "idle.conf"));
            Caching.BlankVisualizer = new(Path.Combine(ApplicationConfiguration.InternalShaderPath, "blank.conf"));

            CacheInternalShader("idle");
            CacheInternalShader("blank");
            CacheInternalShader("crossfade");

            // see property comments for an explanation
            GL.GetInteger(GetPName.MaxCombinedTextureImageUnits, out var maxTU);
            Caching.MaxAvailableTextureUnit = maxTU - 1 - Caching.KnownAudioTextures.Count;
            LogHelper.Logger?.LogInformation($"This GPU supports a combined maximum of {maxTU} TextureUnits.");
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

        public new void Dispose()
            => throw new InvalidOperationException("Invoke \"await DisposeAsync\" instead of Dispose");

        public async ValueTask DisposeAsync()
        {
            if (IsDisposed) return;
            base.Dispose();

            await Eyecandy?.EndAudioProcessing();
            Renderer?.Dispose();
            Caching.InternalShaders.DisposeAndClear();
            Caching.Shaders.DisposeAndClear();
            Eyecandy?.Dispose();

            IsDisposed = true;
            GC.SuppressFinalize(true);
        }
        private bool IsDisposed = false;
    }
}
