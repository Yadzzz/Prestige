using Server.Infrastructure.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Infrastructure.Logger.Loggers
{
    public class FileLogger : ILogger
    {
        private static readonly object _lock = new object();
        private readonly string _logFilePath;

        public FileLogger()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _logFilePath = System.IO.Path.Combine(baseDir, "logs.txt");
        }

        private void WriteLine(string level, string message)
        {
            try
            {
                var line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
                lock (_lock)
                {
                    System.IO.File.AppendAllText(_logFilePath, line + Environment.NewLine);
                }
            }
            catch
            {
                // Never throw from logger
            }
        }

        public void Log(string message)
        {
            WriteLine("INFO", message);
        }

        public void LogWarning(string message)
        {
            WriteLine("WARN", message);
        }

        public void LogError(string message)
        {
            WriteLine("ERROR", message);
        }
    }
}
