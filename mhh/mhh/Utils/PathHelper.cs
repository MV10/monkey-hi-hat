
namespace mhh.Utils;

internal static class PathHelper
{
    public static string FindFile(string pathspec, string filename)
    {
        var paths = pathspec.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
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
