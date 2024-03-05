using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon
{
    public class PacketFileContentInfoRequest : Packet
    {
        private long _inquireId;
        private long _requestId;
        private long _startPos;
        private string _path;
        private int _pathLength;
        public PacketFileContentInfoRequest(byte[] bytes) : base(bytes)
        {
        }

        public PacketFileContentInfoRequest(int clientId, long inquireId, long requestId, long startPos, string path) : base(PacketType.FileContentInfoRequest, clientId)
        {
            _inquireId = inquireId;
            _requestId = requestId;
            _startPos = startPos;
            _path = path;
        }

        public long InquireId { get => _inquireId; set => _inquireId = value; }
        public long RequestId { get => _requestId; set => _requestId = value; }
        public string Path { get => _path; set => _path = value; }
        public long StartPos { get => _startPos; set => _startPos = value; }

        protected override void Deserialize(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentException("bytes");

            using (var stream = new ByteArrayStream(bytes))
            {
                _inquireId = stream.ReadInt64();
                _requestId = stream.ReadInt64();
                _startPos = stream.ReadInt64();
                _pathLength = stream.ReadInt32();

                byte[] buffer = new byte[_pathLength];
                stream.Read(buffer, 0, _pathLength);
                _path = Encoding.UTF8.GetString(buffer).TrimEnd('\0');

            }
        }

        protected override byte[] Serialize()
        {
            using (var stream = new ByteArrayStream())
            {
                stream.Write(_inquireId);
                stream.Write(_requestId);
                stream.Write(_startPos);

                byte[] buffer = Encoding.UTF8.GetBytes(Path);
                _pathLength = buffer.Length;
                stream.Write(_pathLength);

                stream.Write(buffer, 0, buffer.Length);
                return stream.GetBuffer();
            }
        }
    }
}
