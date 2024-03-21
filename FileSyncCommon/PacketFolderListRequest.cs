using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon
{
    public class PacketFolderListRequest : PacketRequest
    {
        private string _path;
        private int _pathLength;
        public PacketFolderListRequest(byte[] bytes) : base(bytes)
        {
        }

        public PacketFolderListRequest(int clientId, int requestId,string path) : base(PacketType.FolderListRequest, clientId, requestId)
        {
            _path = path;
        }

        public string Path { get => _path; set => _path = value; }

        protected override void Deserialize(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentException("bytes");

            using (var stream = new ByteArrayStream(bytes))
            {
                _requestId = stream.ReadInt64();
                _pathLength = stream.ReadInt32();

                var buffer = new byte[_pathLength];
                stream.Read(buffer, 0, _pathLength);
                _path = Encoding.UTF8.GetString(buffer).Trim('\0');
            }
        }
        protected override byte[] Serialize()
        {
            using (var stream = new ByteArrayStream())
            {
                stream.Write(_requestId);

                var buffer = Encoding.UTF8.GetBytes(_path);
                _pathLength = buffer.Length;
                stream.Write(_pathLength);

                stream.Write(buffer, 0, _pathLength);
                return stream.GetBuffer();
            }
        }
    }
}
