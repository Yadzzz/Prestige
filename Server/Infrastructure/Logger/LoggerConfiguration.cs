using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Infrastructure.Logger
{
    public class LoggerConfiguration
    {
        public bool ConsoleLoggerEnabled { get; set; }
        public bool FileLoggerEnabled { get; set; }
        public bool DatabaseLoggerEnabled { get; set; }
        public string FileLocation { get; set; }
    }
}
