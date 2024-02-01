using System.IO;
using System.IO.Compression;

namespace mhhinstall
{
    public static class ZipExtensions
    {
        // Amazing there was no overwrite option in the old .NET. Wow.
        public static void ExtractWithOverwrite(string sourceArchiveFileName, string destinationDirectoryName)
        {
            using (var archive = ZipFile.Open(sourceArchiveFileName, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    string destPath = Path.Combine(destinationDirectoryName, entry.FullName);
                    string destDir = Path.GetDirectoryName(destPath);
                    if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                    if (!string.IsNullOrEmpty(Path.GetFileName(destPath))) entry.ExtractToFile(destPath, overwrite: true);
                }
            }
        }
    }
}
