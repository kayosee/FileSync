using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Tools;

namespace FileSyncCommon.Messages
{
    public class AuthenticateRequest : Request
    {
        private string _password;
        public string Password { get => _password; set => _password = value; }
        public AuthenticateRequest(ByteArrayStream stream) : base(stream)
        {
            _password = stream.ReadUTF8String();
        }
        public AuthenticateRequest(int clientId, long requestId, string password) : base(MessageType.AuthenticateRequest, clientId, requestId)
        {
            _password = password;
        }
        protected override ByteArrayStream GetStream()
        {
            var stream = base.GetStream();
            stream.WriteUTF8string(_password);
            return stream;
        }
    }
}
