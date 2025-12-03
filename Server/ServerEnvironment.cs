using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Server.Communication;
using Server.Infrastructure;

namespace Server
{
    public class ServerEnvironment
    {
        public ServerManager ServerManager { get; set; }
        public CommunicationManager CommunicationManager { get; set; }

        public void Initialize()
        {
            this.ServerManager = new ServerManager();
            this.CommunicationManager = new CommunicationManager();

            Console.WriteLine("Server Initialized ->");
            Console.WriteLine();
        }

        private static ServerEnvironment _serverEnvironment;

        public static ServerEnvironment GetServerEnvironment()
        {
            //_serverEnvironment ??= new ServerEnvironment();

            if (_serverEnvironment == null)
            {
                _serverEnvironment = new ServerEnvironment();
            }

            return _serverEnvironment;
        }
    }
}
