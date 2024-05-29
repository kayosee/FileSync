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
        private int _passwordLength;
        public string Password { get => _password; set => _password = value; }
        public AuthenticateRequest(ByteArrayStream stream) : base(stream)
        {
            _requestId = stream.ReadInt64();
            _passwordLength = stream.ReadInt32();

            byte[] buffer = new byte[_passwordLength];
            stream.Read(buffer, 0, _passwordLength);
            _password = Encoding.UTF8.GetString(buffer).TrimEnd('\0');
        }

        public AuthenticateRequest(int clientId, long requestId, string password) : base(MessageType.AuthenticateRequest, clientId, requestId)
        {
            _password = password;
        }

        protected override ByteArrayStream GetStream()
        {
            var stream = base.GetStream();
            byte[] buffer = Encoding.UTF8.GetBytes(_password);
            _passwordLength = buffer.Length;
            stream.Write(_passwordLength);
            stream.Write(buffer, 0, buffer.Length);
            return stream;
        }
    }
}
