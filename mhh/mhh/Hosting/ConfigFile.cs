
using OpenTK.Graphics.OpenGL;
using Microsoft.Extensions.Logging;

namespace mhh
{
    /// <summary>
    /// Container for MHH "conf" configuration files. All MHH conf files consist of
    /// [section] headers followed by key=value pairs. Blank lines and hashtag (#) prefixed
    /// content is discarded. Invalid content is discarded (key=value pairs before a [section]
    /// heading, lines not in the key=value format, or duplicate keys within a section). All
    /// [section] and key values are forced to lowercase for case-insensitive matching. Values
    /// retain their casing (important for things like Type-name-matching).
    /// </summary>
    public class ConfigFile
    {
        /// <summary>
        /// The source of the configuration data. If a relative pathname is provided
        /// to the constructor, this stores the fully-qualified equivalent.
        /// </summary>
        public readonly string Pathname = string.Empty;

        /// <summary>
        /// Contents of the configuration data excluding blank lines and comments.
        /// The nested dictionaries reflect the conf file as <section, <key, value>>.
        /// </summary>
        public readonly Dictionary<string, Dictionary<string, string>> Content = new();

        private Random RNG = new();

        // Can't be readonly or static because Program.FindAppConfig creates a ConfigFile
        // before logging is initialized, but later ConfigFile instances need a valid Logger.
        private static ILogger Logger;

        public ConfigFile(string confPathname)
        {
            Logger = LogHelper.CreateLogger(nameof(ConfigFile));

            Pathname = Path.GetFullPath(confPathname);
            if (!Pathname.EndsWith(".conf", Const.CompareFlags)) Pathname += ".conf";
            if (!File.Exists(Pathname))
            {
                var err = $"Configuration file not found: {Pathname}";
                if (Logger is null)
                {
                    // Program.FindAppConfig creates a ConfigFile before logging is initialized
                    Console.WriteLine(err);
                }
                else
                {
                    Logger.LogError(err);
                }
                return;
            }

            Logger?.LogTrace(Pathname);

            var section = string.Empty;
            int sectionID = 0;
            foreach (var line in File.ReadAllLines(Pathname))
            {
                if (!string.IsNullOrWhiteSpace(line) && !line.Trim().StartsWith("#"))
                {
                    var text = (line.Contains("#")) ? line.Split("#", Const.SplitOptions)[0].Trim() : line.Trim();
                    if (text.StartsWith("[") && text.EndsWith("]"))
                    {
                        section = text.Substring(1, text.Length - 2).ToLowerInvariant();
                        sectionID = 0;
                    }
                    else
                    {
                        if(!string.IsNullOrWhiteSpace(section))
                        {
                            var kvp = (text + " ").Split("=", 2, Const.SplitOptions);
                            if(kvp.Length > 0)
                            {
                                if (!Content.ContainsKey(section)) Content.Add(section, new());
                                var key = (kvp.Length == 2) ? kvp[0] : (sectionID++).ToString();
                                var keylower = key.ToLowerInvariant();
                                if (Content[section].ContainsKey(keylower))
                                {
                                    Logger?.LogWarning($"Config {Pathname} section [{section}] has multiple entries for setting {key}");
                                }
                                else
                                {
                                    var value = (kvp.Length == 2) ? kvp[1] : kvp[0];
                                    Content[section].Add(keylower, value);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Helper function for .conf files that support a [uniforms] section. Also used to
        /// parse FX options uniform lists the same way.
        /// </summary>
        public Dictionary<string, float> ParseUniforms(string section_key = "uniforms")
        {
            var uniforms = new Dictionary<string, float>();
            if (!Content.ContainsKey(section_key)) return uniforms;

            foreach (var u in Content[section_key])
            {
                if (uniforms.ContainsKey(u.Key)) continue;

                var range = u.Value.Split(':', Const.SplitOptions);
                if (range.Length == 1)
                {
                    if (float.TryParse(range[0], out var f))
                    {
                        uniforms.Add(u.Key, f);
                    }
                }
                else
                {
                    if (float.TryParse(range[0], out var f0) && float.TryParse(range[1], out var f1))
                    {
                        var hi = Math.Max(f0, f1);
                        var lo = Math.Min(f0, f1);
                        var span = hi - lo;
                        float val = (float)RNG.NextDouble() * span + lo;
                        uniforms.Add(u.Key, val);
                    }
                }

                if (!uniforms.ContainsKey(u.Key)) uniforms.Add(u.Key, 0f);
            }

            return uniforms;
        }

        /// <summary>
        /// Helper function for .conf files that support [fx-uniforms:filename} sections.
        /// </summary>
        public Dictionary<string, Dictionary<string, float>> ParseFXUniforms()
        {
            var uniforms = new Dictionary<string, Dictionary<string, float>>();
            foreach(var kvp in Content)
            {
                if(kvp.Key.StartsWith("fx-uniforms:", Const.CompareFlags))
                {
                    var fxname = kvp.Key.Substring(12);
                    if(!uniforms.ContainsKey(fxname))
                    {
                        var fxuniforms = ParseUniforms(kvp.Key);
                        uniforms.Add(fxname, fxuniforms);
                    }
                }
            }
            return uniforms;
        }

        /// <summary>
        /// Helper function for .conf files that support a [libraries] section. This validates
        /// the files can be accessed and will throw an exception if the file isn't found. If the
        /// ShaderType element is null, this indicates the file should be linked to both the
        /// vertex and fragment program stages.
        /// </summary>
        public List<LibraryShaderConfig> ParseLibraryPathnames(string pathspec)
        {
            /*
            Supported patterns:

            path\filename.ext           explicit location of any library file (.glsl links to both vert and frag stages)
            vert:path\filename.glsl     explicit location of a library file to link to the vertex stage
            frag:path\filename.glsl     explicit location of a library file to link to the fragment stage
            filename                    extension determines handling, finds .glsl, .vert, or .frag extensions
            filename.vert               library to link to the vertex stage only
            filename.frag               library to link to the fragment stage only
            filename.glsl               general library to link to both the vertex and fragment stages
            vert:filename               general library to link to the vertex stage only, .glsl extension implied
            frag:filename               general library to link to the fragment stage only, .glsl extension implied
            vert:filename.glsl          general library to link to the vertex stage only
            frag:filename.glsl          general library to link to the fragment stage only

            Any path-filename combination is required to specify the extension.

            For the filename-only pattern, it is valid to have both a .frag and .vert file with the same filename,
            but if a .glsl filename exists, an exception is thrown if a .frag or .vert extension is also found.
            */

            var libs = new List<LibraryShaderConfig>();
            if (!Content.ContainsKey("libraries")) return libs;

            string pathname;
            ShaderType? type;
            foreach(var lib_kvp in Content["libraries"])
            {
                var lib = lib_kvp.Value;

                type = null;
                if (lib.StartsWith("vert:")) type = ShaderType.VertexShader;
                if (lib.StartsWith("frag:")) type = ShaderType.FragmentShader;
                if (type is not null) lib = lib.Substring(5);

                if (PathHelper.HasPathSeparators(lib))
                {
                    // entries with path info must specify a full filename
                    pathname = Path.GetFullPath(lib);
                    if (string.IsNullOrEmpty(pathname)) pathname = lib;
                    var ext = Path.GetExtension(lib);
                    if (string.IsNullOrEmpty(ext)
                        || !ext.Equals(".vert", Const.CompareFlags) 
                        && !ext.Equals(".frag", Const.CompareFlags)
                        && !ext.Equals(".glsl", Const.CompareFlags)) throw new ArgumentException($"Path-based shader library entry {lib} must specify a .vert, .frag, or .glsl filename extension");
                    if(type is not null && !ext.Equals(".glsl", Const.CompareFlags)) throw new ArgumentException($"Shader library entry {lib} must specify a .glsl filename extension");
                    if (!File.Exists(pathname)) throw new ArgumentException($"File not found for shader library entry {lib}");
                    if (ext.Equals(".vert", Const.CompareFlags)) type = ShaderType.VertexShader;
                    if (ext.Equals(".frag", Const.CompareFlags)) type = ShaderType.FragmentShader;
                    AddToList(libs, pathname, type);
                }
                else
                {
                    var ext = Path.GetExtension(lib);
                    if(type is not null)
                    {
                        // entries prefixed by vert: or frag: must be .glsl files
                        var filename = string.IsNullOrEmpty(ext) ? Path.ChangeExtension(lib, "glsl") : lib;
                        pathname = PathHelper.FindFile(pathspec, filename);
                        if(pathname is null) throw new ArgumentException($"Shader library entry {lib} must reference a valid .glsl file");
                        AddToList(libs, pathname, type);
                    }
                    else
                    {
                        // check for different possible extensions
                        if (string.IsNullOrEmpty(ext))
                        {
                            var count1 = libs.Count; // store to check if any of these succeed

                            pathname = PathHelper.FindFile(pathspec, Path.ChangeExtension(lib, "glsl"));
                            if(pathname is not null) AddToList(libs, pathname, type);

                            var count2 = libs.Count; // if .glsl exists, there shouldn't be .vert or .frag files of the same name

                            pathname = PathHelper.FindFile(pathspec, Path.ChangeExtension(lib, "vert"));
                            if (pathname is not null) AddToList(libs, pathname, ShaderType.VertexShader);

                            pathname = PathHelper.FindFile(pathspec, Path.ChangeExtension(lib, "frag"));
                            if (pathname is not null) AddToList(libs, pathname, ShaderType.FragmentShader);

                            if (libs.Count == count1) throw new ArgumentException($"Shader library entry {lib} could not be found with .vert, .frag or .glsl extension");
                            if (count2 > count1 + 1) throw new ArgumentException($"Shader library entry {lib} matching a .glsl file must not also match .vert or .frag files");
                        }
                        else
                        {
                            // the entry is a specific filename
                            if (!ext.Equals(".glsl", Const.CompareFlags)
                                && !ext.Equals(".vert", Const.CompareFlags) 
                                && !ext.Equals(".frag", Const.CompareFlags))
                            {
                                throw new ArgumentException($"Shader library entry {lib} is invalid; only .vert, .frag or .glsl files are accepted; omit extension for auto-discovery");
                            }

                            pathname = PathHelper.FindFile(pathspec, lib);
                            if(pathname is null) throw new ArgumentException($"Shader library entry {lib} file not found");
                            if (ext.Equals(".vert", Const.CompareFlags)) type = ShaderType.VertexShader;
                            if (ext.Equals(".frag", Const.CompareFlags)) type = ShaderType.FragmentShader;
                            AddToList(libs, pathname, type);
                        }
                    }
                }

            }

            return libs;
        }

        private void AddToList(List<LibraryShaderConfig> list, string pathname, ShaderType? type)
        {
            LibraryShaderConfig value;
            if (type is not null)
            {
                value = new(pathname, type.Value);
                if (!list.Contains(value)) list.Add(value);
            }
            else
            {
                value = new(pathname, ShaderType.VertexShader);
                if (!list.Contains(value)) list.Add(value);

                value = new(pathname, ShaderType.FragmentShader);
                if (!list.Contains(value)) list.Add(value);
            }
        }

    }
}
