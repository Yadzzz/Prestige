using Server.Infrastructure.Logger.Loggers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Infrastructure.Logger
{
    public class LoggerManager
    {
        public LoggerConfiguration LoggerConfiguration { get; private set; }
        private FileLogger _fileLogger { get; set; }
        private ConsoleLogger _consoleLogger { get; set; }
        private DatabaseLogger _databaseLogger { get; set; }

        public LoggerManager(LoggerConfiguration loggerConfiguration)
        {
            this.LoggerConfiguration = loggerConfiguration;
            this._fileLogger = new FileLogger();
            this._consoleLogger = new ConsoleLogger();
            this._databaseLogger = new DatabaseLogger();
        }

        private void Log(string message, LoggingLevel level)
        {
            if (this.LoggerConfiguration.FileLoggerEnabled)
            {
                switch (level)
                {
                    case LoggingLevel.Info:
                        this._fileLogger.Log(message);
                        break;
                    case LoggingLevel.Warning:
                        this._fileLogger.LogWarning(message);
                        break;
                    case LoggingLevel.Error:
                        this._fileLogger.LogError(message);
                        break;
                    default:
                        this._fileLogger.Log(message);
                        break;
                }
            }

            if (this.LoggerConfiguration.ConsoleLoggerEnabled)
            {
                switch (level)
                {
                    case LoggingLevel.Info:
                        this._consoleLogger.Log(message);
                        break;
                    case LoggingLevel.Warning:
                        this._consoleLogger.LogWarning(message);
                        break;
                    case LoggingLevel.Error:
                        this._consoleLogger.LogError(message);
                        break;
                    default:
                        this._consoleLogger.Log(message);
                        break;
                }
            }

            if (this.LoggerConfiguration.DatabaseLoggerEnabled)
            {
                switch (level)
                {
                    case LoggingLevel.Info:
                        this._databaseLogger.Log(message);
                        break;
                    case LoggingLevel.Warning:
                        this._databaseLogger.LogWarning(message);
                        break;
                    case LoggingLevel.Error:
                        this._databaseLogger.LogError(message);
                        break;
                    default:
                        this._databaseLogger.Log(message);
                        break;
                }
            }
        }

        public void Log(string message)
        {
            this.Log(message, LoggingLevel.Info);
        }

        public void LogWarning(string message)
        {
            this.Log(message, LoggingLevel.Warning);
        }

        public void LogError(string message)
        {
            this.Log(message, LoggingLevel.Error);
        }

        public void LogError(Exception exception)
        {
            this.Log(exception.ToString(), LoggingLevel.Error);
        }
    }
}
