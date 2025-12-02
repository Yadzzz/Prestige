using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server.Infrastructure.Connection
{
    public class ConnectionManager
    {
        private ServerSocket _serverSocket;
        private List<ClientSocket> _clients;

        public ConnectionManager()
        {
            this._serverSocket = new ServerSocket();
            this._clients= new List<ClientSocket>();

            Console.WriteLine("ConnectionManager Initialized ->");
        }

        public void AddActiveClient(ClientSocket client)
        {
            if(this._clients.Contains(client))
            {
                return;
            }

            this._clients.Add(client);
        }

        public void RemoveActiveClient(ClientSocket client)
        {
            if(!this._clients.Contains(client))
            {
                return;
            }

            this._clients.Remove(client);
        }
    }
}
