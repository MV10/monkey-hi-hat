
namespace mhh;

internal static class PathHelper
{
    public static string[] GetIndividualPaths(string pathspec)
        => pathspec.Split(Path.PathSeparator, Const.SplitOptions);

    public static string FindFile(string pathspec, string filename)
    {
        if (string.IsNullOrWhiteSpace(pathspec)) return null;

        var paths = GetIndividualPaths(pathspec);
        foreach (var path in paths)
        {
            string pathname = Path.Combine(path, filename);
            if (File.Exists(pathname)) return pathname;
        }
        return null;
    }

    public static string FindConfigFile(string pathspec, string filename)
    {
        if (!filename.EndsWith(".conf", StringComparison.InvariantCultureIgnoreCase)) filename += ".conf";
        return FindFile(pathspec, filename);
    }

    public static IReadOnlyList<string> GetConfigFiles(string pathspec)
    {
        List<string> list = new();
        var paths = pathspec.Split(';', Const.SplitOptions);
        foreach (var path in paths)
        {
            foreach (var filename in Directory.EnumerateFiles(path, "*.conf"))
            {
                list.Add(Path.GetFileNameWithoutExtension(filename));
            }
        }
        return list;
    }

    public static bool HasPathSeparators(string argument)
        => argument.Contains(Path.DirectorySeparatorChar) || argument.Contains(Path.AltDirectorySeparatorChar);
}
