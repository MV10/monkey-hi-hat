
namespace mhh;

internal static class PathHelper
{
    public static string FindFile(string pathspec, string filename)
    {
        if (string.IsNullOrWhiteSpace(pathspec)) return null;

        var paths = pathspec.Split(Path.PathSeparator, Const.SplitOptions);
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
}
