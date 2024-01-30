using System;
using System.IO;

namespace mhhinstall
{
    public static class Output
    {
        public static void Write(string message)
        {
            Console.WriteLine(message);
            LogOnly(message);
        }

        public static void Separator()
        {
            Write("".PadLeft(Console.WindowWidth - 1, '-'));
        }

        public static void LogOnly(string message)
        {
            File.AppendAllText(Installer.log, $"{message}\n");
        }

        public static string Prompt(string keys)
        {
            Output.Write($"[{keys.ToUpper()}]? ");
            while(true)
            {
                if(Console.KeyAvailable)
                {
                    var k = Console.ReadKey(true).KeyChar.ToString().ToUpper();
                    if (keys.Contains(k))
                    {
                        return k;
                    }
                }
            }
        }
    }
}
