using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Client
{
    public class ClientManager
    {
        private List<Client> _clients;

        public ClientManager()
        {
            _clients = new List<Client>();
        }

        public List<Client> Clients
        {
            get
            {
                return _clients;
            }
        }

        public void AddClient(Client client)
        {
            if (!Clients.Contains(client))
            {
                Clients.Add(client);
            }
        }

        public void RemoveClient(Client client)
        {
            if (Clients.Contains(client))
            {
                Clients.Remove(client);
            }
        }
    }
}
