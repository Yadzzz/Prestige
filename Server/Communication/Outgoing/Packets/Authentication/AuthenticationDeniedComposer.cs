using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Communication.Outgoing.Packets
{
    public class AuthenticationDeniedComposer : ServerPacket
    {
        public AuthenticationDeniedComposer(string error) : base(OutgoingPacketHeaders.AuthenticationDenied)
        {
            base.WriteString(error);
        }
    }
}
