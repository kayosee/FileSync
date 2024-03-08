using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon
{
    public class PacketAuthenticateRequest : PacketRequest
    {
        private string _password;
        private int _passwordLength;
        public PacketAuthenticateRequest(byte[] bytes) : base(bytes)
        {
        }

        public PacketAuthenticateRequest(int clientId, long requestId, string password) : base(PacketType.AuthenticateRequest, clientId, requestId)
        {
            _password = password;
        }

        public string Password { get => _password; set => _password = value; }

        protected override void Deserialize(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException("bytes");
            }
            using (var stream = new ByteArrayStream(bytes))
            {
                _requestId = stream.ReadInt64();
                _passwordLength = stream.ReadInt32();

                byte[] buffer = new byte[_passwordLength];
                stream.Read(buffer, 0, _passwordLength);
                _password = Encoding.UTF8.GetString(buffer).TrimEnd('\0');
            }
        }

        protected override byte[] Serialize()
        {
            using (var stream = new ByteArrayStream())
            {
                stream.Write(_requestId);
                byte[] buffer = Encoding.UTF8.GetBytes(_password);
                _passwordLength = buffer.Length;
                stream.Write(_passwordLength);

                stream.Write(buffer, 0, buffer.Length);
                return stream.GetBuffer();
            }
        }
    }
}
