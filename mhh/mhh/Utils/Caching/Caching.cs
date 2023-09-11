﻿
using eyecandy;
using System.Numerics;

namespace mhh.Utils
{
    public static class Caching
    {
        /// <summary>
        /// All built-in EyeCandy AudioTexture implementations. Plugin DLLs may define others not listed here.
        /// </summary>
        public static readonly IReadOnlyList<Type> KnownAudioTextures = typeof(AudioTexture).GetAllDerivedTypes();

        /// <summary>
        /// All built-in MHH IVisualizer implementations. Plugin DLLs may define others not listed here.
        /// </summary>
        public static readonly IReadOnlyList<Type> KnownVisualizers = typeof(IVisualizer).GetAllDerivedTypes();

        /// <summary>
        /// Compiled shader programs. The key is a murmur3 hash of the combined vert and frag pathnames.
        /// </summary>
        public static CacheLRU<BigInteger, CachedShader> Shaders;

        /// <summary>
        /// Compiled internal shaders keyed on the name (blank, idle, crossfade).
        /// </summary>
        public static Dictionary<string, Shader> InternalShaders = new();

        /// <summary>
        /// The built-in default visualizer.
        /// </summary>
        public static VisualizerConfig IdleVisualizer;

        /// <summary>
        /// The built-in visualizer to optionally blank the screen in response
        /// to long-term silence detection.
        /// </summary>
        public static VisualizerConfig BlankVisualizer;

        /// <summary>
        /// Indicates the highest 0-based TextureUnit which can be assigned by FramebufferManager.
        /// This is calculated from the GL MaxCombinedTextureImageUnits value, less 1 (for 0 offset)
        /// and less the number of known audio texture classes (as the eyecandy library hard-assigns
        /// those from the high end of the range).
        /// </summary>
        public static int MaxAvailableTextureUnit;
    }
}