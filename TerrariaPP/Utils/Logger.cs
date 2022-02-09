using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TerrariaPP.Utils
{
    public enum LogLevel
    {
        NONE,
        ERROR,
        WARNING,
        INFO,
        DEBUG,
    }

    public static class Logger
    {
        private static readonly Dictionary<LogLevel, ConsoleColor> _level2Color = new Dictionary<LogLevel, ConsoleColor>()
        {
            { LogLevel.NONE,    ConsoleColor.White },
            { LogLevel.ERROR,   ConsoleColor.Red },
            { LogLevel.WARNING, ConsoleColor.Yellow },
            { LogLevel.INFO,    ConsoleColor.White },
            { LogLevel.DEBUG,   ConsoleColor.Green }
        };

        private static readonly Dictionary<LogLevel, bool> _level2ShowCaller = new Dictionary<LogLevel, bool>()
        {
            { LogLevel.NONE,    false },
            { LogLevel.ERROR,   true },
            { LogLevel.WARNING, true },
            { LogLevel.INFO,    false },
            { LogLevel.DEBUG,   true }
        };

        private static readonly object _consoleWriteLock = new object();

        public static void Log(string content, LogLevel level = LogLevel.DEBUG, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNum = 0)
        {
            string fileName = Path.GetFileName(filePath);

            if (level > TerrariaPPPlugin.Config.Settings.LogLevel)
                return;
            lock (_consoleWriteLock)
            {
                Console.ForegroundColor = _level2Color[level];
                Console.WriteLine($"[TerrariaPP] [{level:G}]{(_level2ShowCaller[level] ? $" [{fileName}:{lineNum}]" : "")} {content}");
                Console.ResetColor();
            }
        }
    }
}
