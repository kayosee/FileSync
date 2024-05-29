using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Tools;

namespace FileSyncCommon.Messages
{
    public class AuthenticateResponse : Response
    {
        private byte _ok;

        public bool OK { get => _ok == 1; set => _ok = (byte)(value ? 1 : 0); }
        public AuthenticateResponse(int clientId, long requestId,bool ok) : base(MessageType.AuthenticateResponse, clientId, requestId, true)
        {
            _ok = (byte)(ok ? 1 : 0);
        }
        public AuthenticateResponse(ByteArrayStream stream) : base(stream)
        {
            _ok = stream.ReadByte();
        }
        protected override ByteArrayStream GetStream()
        {
            var stream = base.GetStream();
            stream.Write(_ok);
            return stream;
        }
    }
}
