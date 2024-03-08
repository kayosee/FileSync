using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon
{
    public class PacketAuthenticateResponse : PacketResponse
    {
        private byte _ok;
        public PacketAuthenticateResponse(byte[] bytes) : base(bytes)
        {
        }

        public PacketAuthenticateResponse(int clientId, long requestId, bool ok) : base(PacketType.AuthenticateResponse, clientId, requestId)
        {
            _ok = (byte)(ok ? 1 : 0);
        }

        public bool OK { get => _ok == 1; set => _ok = (byte)(value ? 1 : 0); }

        protected override void Deserialize(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException("bytes");
            }
            using (var stream = new ByteArrayStream(bytes))
            {
                _requestId = stream.ReadInt64();
                _ok = stream.ReadByte();
            }
        }

        protected override byte[] Serialize()
        {
            using (var stream = new ByteArrayStream())
            {
                stream.Write(_requestId);               
                stream.Write(_ok);
                return stream.GetBuffer();
            }
        }
    }
}
