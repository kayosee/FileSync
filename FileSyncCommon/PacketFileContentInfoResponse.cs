using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon
{
    public class PacketFileContentInfoResponse : PacketResponse
    {
        private long _lastPos;
        private uint _checksum;
        private long _totalCount;
        private long _totalSize;
        private int _pathLength;
        private string _path;
        public PacketFileContentInfoResponse(byte[] bytes) : base(bytes)
        {
        }

        public PacketFileContentInfoResponse(int clientId, long requestId,long lastPos,uint checksum, long totalCount, long totalSize, string path) : base(PacketType.FileContentInfoResponse, clientId, requestId)
        {
            _lastPos = lastPos;
            _checksum = checksum;
            _totalCount = totalCount;
            _totalSize = totalSize;
            _path = path;
        }
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

        protected override void Deserialize(byte[] bytes)
        {
            using (var stream = new ByteArrayStream(bytes))
            {
                _requestId = stream.ReadInt64();
                _lastPos = stream.ReadInt64();
                _checksum = stream.ReadUInt32();
                _totalCount = stream.ReadInt64();
                _totalSize = stream.ReadInt64();
                _pathLength = stream.ReadInt32();
                var buffer = new byte[_pathLength];

                stream.Read(buffer, 0, _pathLength);
                _path = Encoding.UTF8.GetString(buffer, 0, _pathLength).Trim('\0');
            }
        }

        protected override byte[] Serialize()
        {
            if (string.IsNullOrEmpty(_path))
                throw new ArgumentException("path");

            using (var stream = new ByteArrayStream())
            {
                stream.Write(_requestId);
                stream.Write(_lastPos);
                stream.Write(_checksum);
                stream.Write(_totalCount);
                stream.Write(_totalSize);

                byte[] buffer = Encoding.UTF8.GetBytes(_path);
                _pathLength = buffer.Length;
                stream.Write(_pathLength);
                stream.Write(buffer, 0, _pathLength);

                return stream.GetBuffer();
            }
        }
    }
}
