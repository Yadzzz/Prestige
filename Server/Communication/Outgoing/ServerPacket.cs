using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Communication.Outgoing
{
    public class ServerPacket
    {
        public List<byte> Bytes;

        public ServerPacket(int header)
        {
            this.Bytes = new List<byte>();
            this.WriteInt(header);
        }

        public void WriteString(string s)
        {
            this.WriteShort((short)s.Length);
            this.Write(Encoding.ASCII.GetBytes(s));
        }

        public void WriteInt(int i)
        {
            this.Write(BitConverter.GetBytes(i));
        }

        public void WriteBoolean(bool b)
        {
            this.Write(BitConverter.GetBytes(b));
        }

        public void WriteShort(short s)
        {
            this.Write(BitConverter.GetBytes(s));
        }

        public void Write(byte[] bytes)
        {
            this.Bytes.AddRange(bytes);
        }

        public byte[] Finalize()
        {
            return this.Bytes.ToArray();
        }
    }
}
