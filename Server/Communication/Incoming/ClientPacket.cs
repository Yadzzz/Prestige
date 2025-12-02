using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Communication.Incoming
{
    public class ClientPacket
    {
        public byte[] Bytes;
        public int Header;
        public int Pointer;

        public ClientPacket(byte[] bytes, int header)
        {
            this.Bytes = bytes;
            this.Header = header;
            this.Pointer = 4;
        }

        public string ReadString()
        {
            return Encoding.ASCII.GetString(this.ReadBytes(this.ReadShort()));
        }

        public int ReadInt()
        {
            return BitConverter.ToInt32(this.ReadBytes(4), 0);
        }

        public short ReadShort()
        {
            return BitConverter.ToInt16(this.ReadBytes(2), 0);
        }

        public bool ReadBoolean()
        {
            return BitConverter.ToBoolean(this.ReadBytes(1), 0);
        }

        public byte[] ReadBytes(short bytesToRead)
        {
            byte[] bytesRead = new byte[bytesToRead];

            for(int i = 0; i < bytesToRead; i++, this.Pointer++)
            {
                bytesRead[i] = this.Bytes[this.Pointer];
            }

            return bytesRead;
        }

        public void RefreshPointer(int i)
        {
            this.Pointer += i;
        }
    }
}
