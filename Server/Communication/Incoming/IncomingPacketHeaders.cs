using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Communication.Incoming
{
    public class IncomingPacketHeaders
    {
        //AuthenticationRequest
        public const int AuthenticationRequestEvent = 1001;

        //Application
        public const int ApplicationLogEvent = 1010;
    }
}
