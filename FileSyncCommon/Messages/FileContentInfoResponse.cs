using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Tools;

namespace FileSyncCommon.Messages
{
    public class FileContentInfoResponse : Response
    {
        private long _lastPos;
        private uint _checksum;
        private long _totalCount;
        private long _totalSize;
        private int _pathLength;
        private string _path;
        /// <summary>
        /// 总共分片数量
        /// </summary>
        public long TotalCount { get => _totalCount; set => _totalCount = value; }
        /// <summary>
        /// 总共文件大小
        /// </summary>
        public long TotalSize { get => _totalSize; set => _totalSize = value; }
        /// <summary>
        /// 文件路径
        /// </summary>
        public string Path { get => _path; set => _path = value; }
        /// <summary>
        /// 最后传输的位置
        /// </summary>
        public long LastPos { get => _lastPos; set => _lastPos = value; }
        /// <summary>
        /// 按照传输的位置计算检验（LASTPOS>0）
        /// </summary>
        public uint Checksum { get => _checksum; set => _checksum = value; }
        public FileContentInfoResponse(ByteArrayStream stream) : base(stream)
        {
            _lastPos = stream.ReadInt64();
            _checksum = stream.ReadUInt32();
            _totalCount = stream.ReadInt64();
            _totalSize = stream.ReadInt64();
            _pathLength = stream.ReadInt32();
            var buffer = new byte[_pathLength];

            stream.Read(buffer, 0, _pathLength);
            _path = Encoding.UTF8.GetString(buffer, 0, _pathLength).Trim('\0');
        }

        public FileContentInfoResponse(int clientId, long requestId, long lastPos, uint checksum, long totalCount, long totalSize, string path,bool latest) : base(MessageType.FileContentInfoResponse, clientId, requestId,latest)
        {
            _lastPos = lastPos;
            _checksum = checksum;
            _totalCount = totalCount;
            _totalSize = totalSize;
            _path = path;
        }

        protected override ByteArrayStream GetStream()
        {
            var stream = base.GetStream();
            stream.Write(_lastPos);
            stream.Write(_checksum);
            stream.Write(_totalCount);
            stream.Write(_totalSize);

            byte[] buffer = Encoding.UTF8.GetBytes(_path);
            _pathLength = buffer.Length;
            stream.Write(_pathLength);
            stream.Write(buffer, 0, _pathLength);

            return stream;
        }
    }
}
