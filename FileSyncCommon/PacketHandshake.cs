using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon
{
    public class PacketHandshake : Packet
    {        
        public PacketHandshake(byte[] bytes) : base(bytes)
        {
        }

        public PacketHandshake(int clientId) : base((byte)(PacketType.Handshake), clientId)
        {
        }
        protected override void Deserialize(byte[] bytes)
        {
        }

        protected override byte[] Serialize()
        {
            return new byte[0];
        }
    }
}
