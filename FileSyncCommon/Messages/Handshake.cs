using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Tools;

namespace FileSyncCommon.Messages
{
    public class Handshake : Message
    {
        public Handshake(ByteArrayStream stream) : base(stream)
        {
        }

        public Handshake(int clientId) : base(MessageType.Handshake, clientId)
        {
        }
    }
}
