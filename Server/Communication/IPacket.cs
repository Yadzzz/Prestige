using Server.Communication.Incoming;
using Server.Infrastructure.Connection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Communication
{
    public interface IPacket
    {
        void ExecutePacket(ClientSocket clientSocket, ClientPacket packet);
    }
}
