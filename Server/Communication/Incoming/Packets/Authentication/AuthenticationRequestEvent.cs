using Server.Client.Users;
using Server.Communication.Outgoing.Packets;
using Server.Infrastructure.Connection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Communication.Incoming.Packets
{
    public class AuthenticationRequestEvent : IPacket
    {
        public void ExecutePacket(ClientSocket clientSocket, ClientPacket clientPacket)
        {
            string sid = clientPacket.ReadString();
            string authToken = clientPacket.ReadString();

            if (UsersFactory.TryGetUser(sid, authToken, out User user))
            {
                AuthenticationCompletedComposer packet = new AuthenticationCompletedComposer();
                clientSocket.Send(packet.Finalize());
            }
            else
            {
                AuthenticationDeniedComposer packet = new AuthenticationDeniedComposer("Authentication Failed");
                clientSocket.Send(packet.Finalize());
            }
        }
    }
}
