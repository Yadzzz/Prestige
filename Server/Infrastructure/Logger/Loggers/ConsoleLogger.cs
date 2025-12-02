using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Infrastructure.Logger.Loggers
{
    public  class ConsoleLogger : ILogger
    {
        public void Log(string message)
        {
            Console.WriteLine("[INF] [" + DateTime.Now.ToString() + "] " + message);
        }

        public void LogWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[WRN] [" + DateTime.Now.ToString() + "] " + message);
            Console.ForegroundColor = ConsoleColor.White;
        }

        public void LogError(string message)
        {
            Console.ForegroundColor= ConsoleColor.Red;
            Console.WriteLine("[ERR] [" + DateTime.Now.ToString() + "] " + message);
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}
