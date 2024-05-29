using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Tools;

namespace FileSyncCommon.Messages
{
    public class FileContentInfoRequest : Request
    {
        private long _lastPos;
        private uint _checksum;
        private string _path;
        private int _pathLength;
        public string Path { get => _path; set => _path = value; }
        /// <summary>
        /// 最后一次传输的位置
        /// </summary>
        public long LastPos { get => _lastPos; set => _lastPos = value; }
        /// <summary>
        /// 按LASTPOS校验文件，如果LASTPOS大于零
        /// </summary>
        public uint Checksum { get => _checksum; set => _checksum = value; }

        public FileContentInfoRequest(ByteArrayStream stream) : base(stream)
        {
            _lastPos = stream.ReadInt64();
            _checksum = stream.ReadUInt32();
            _pathLength = stream.ReadInt32();

            byte[] buffer = new byte[_pathLength];
            stream.Read(buffer, 0, _pathLength);
            _path = Encoding.UTF8.GetString(buffer).TrimEnd('\0');
        }

        public FileContentInfoRequest(int clientId, long requestId, long startPos, uint checksum, string path) : base(MessageType.FileContentInfoRequest, clientId, requestId)
        {
            _lastPos = startPos;
            _checksum = checksum;
            _path = path;
        }
        protected override ByteArrayStream GetStream()
        {
            var stream = base.GetStream();
            stream.Write(_lastPos);
            stream.Write(_checksum);
            byte[] buffer = Encoding.UTF8.GetBytes(_path);
            _pathLength = buffer.Length;
            stream.Write(_pathLength);

            stream.Write(buffer, 0, buffer.Length);
            return stream;
        }
    }
}
