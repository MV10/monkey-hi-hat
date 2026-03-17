using System;
using System.IO;

namespace mhhinstall
{
    public static class Output
    {
        /// <summary>
        /// Must be initialized before use (duh).
        /// </summary>
        public static string LogPathname = null;
        
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
            if (LogPathname is null) return;
            File.AppendAllText(LogPathname, $"{message}\n");
        }

        public static string Prompt(string keys)
        {
            Console.Write($"[{keys.ToUpper()}]? ");
            while(true)
            {
                if(Console.KeyAvailable)
                {
                    var k = Console.ReadKey(true).KeyChar.ToString().ToUpper();
                    if (keys.Contains(k))
                    {
                        Console.WriteLine(k);
                        LogOnly($"[{keys.ToUpper()}]? {k}");
                        return k;
                    }
                }
            }
        }
    }
}
