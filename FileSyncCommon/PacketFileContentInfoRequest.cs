using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon
{
    public class PacketFileContentInfoRequest : PacketRequest
    {
        private long _lastPos;
        private uint _checksum;
        private string _path;
        private int _pathLength;
        public PacketFileContentInfoRequest(byte[] bytes) : base(bytes)
        {
        }

        public PacketFileContentInfoRequest(int clientId, long requestId, long startPos,uint checksum, string path) : base(PacketType.FileContentInfoRequest, clientId, requestId)
        {
            _lastPos = startPos;
            _checksum = checksum;
            _path = path;
        }
        public string Path { get => _path; set => _path = value; }
        /// <summary>
        /// 最后一次传输的位置
        /// </summary>
        public long LastPos { get => _lastPos; set => _lastPos = value; }
        /// <summary>
        /// 按LASTPOS校验文件，如果LASTPOS大于零
        /// </summary>
        public uint Checksum { get => _checksum; set => _checksum = value; }

        protected override void Deserialize(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentException("bytes");

            using (var stream = new ByteArrayStream(bytes))
            {
                _requestId = stream.ReadInt64();
                _lastPos = stream.ReadInt64();
                _checksum = stream.ReadUInt32();
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
                stream.Write(_requestId);
                stream.Write(_lastPos);
                stream.Write(_checksum);
                byte[] buffer = Encoding.UTF8.GetBytes(Path);
                _pathLength = buffer.Length;
                stream.Write(_pathLength);

                stream.Write(buffer, 0, buffer.Length);
                return stream.GetBuffer();
            }
        }
    }
}
