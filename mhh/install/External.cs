using System;
using System.Diagnostics;
using System.Text;

// This launches a command-line process, captures all output,
// and scans the output for a specific string value. One of the
// things which is dramatically easier in PowerShell, unfortunately.

namespace mhhinstall
{
    public static class External
    {
        public static bool FindString(string target, string command)
        {
            var success = false;

            (string stdout, string stderr) = ExecuteCmd(command);

            success = stdout.Contains(target);
            Output.LogOnly($"-- TARGET STRING: {target}");
            Output.LogOnly($"-- STRING FOUND: {success}\n");
            return success;
        }

        public static (string stdout, string stderr) ExecuteCmd(string command)
        {
            Output.LogOnly($"\n-- EXTERNAL COMMAND: {command}");

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            using (var p = new Process())
            {
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.FileName = "cmd.exe";
                p.StartInfo.Arguments = $"/c {command}";
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.WorkingDirectory = Installer.temp;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.OutputDataReceived += (src, dat) => stdout.AppendLine(dat.Data);
                p.ErrorDataReceived += (src, dat) => stderr.AppendLine(dat.Data);
                p.Start();
                p.BeginErrorReadLine();
                p.BeginOutputReadLine();
                p.WaitForExit();

                Output.LogOnly($"-- EXIT CODE: {p.ExitCode}");
            }

            var se = stderr.ToString();
            var so = stdout.ToString();

            Output.LogOnly($"-- STDERR");
            Output.LogOnly(se);

            Output.LogOnly($"-- STDOUT");
            Output.LogOnly(so);

            return (so, se);
        }
    }
}
