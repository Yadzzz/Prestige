using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Server.Communication.Outgoing;
using Server.Communication.Incoming;
using Server.Communication.Incoming.Packets;

namespace Server.Communication
{
    public class CommunicationManager
    {
        public Dictionary<int, IPacket> Packets;

        public CommunicationManager()
        {
            this.Packets = new Dictionary<int, IPacket>();
            this.LoadPackets();

            Console.WriteLine("CommunicationManager Initialized ->");
        }

        public bool GetPacket(int header, out IPacket packet)
        {
            return this.Packets.TryGetValue(header, out packet);
        }

        public void LoadPackets()
        {
            this.Packets.Add(IncomingPacketHeaders.AuthenticationRequestEvent, new AuthenticationRequestEvent());
        }
    }
}
