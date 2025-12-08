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
            var logsDir = System.IO.Path.Combine(baseDir, "logs");
            if (!System.IO.Directory.Exists(logsDir))
            {
                System.IO.Directory.CreateDirectory(logsDir);
            }
            _logFilePath = System.IO.Path.Combine(logsDir, $"log_{DateTime.UtcNow:yyyyMMdd}.txt");
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
            catch (Exception ex)
            {
                // Fallback to console if file logging fails
                Console.WriteLine($"[FileLogger Error] {ex.Message}");
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
