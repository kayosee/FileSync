using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon
{
    public class PacketFileContentInfoResponse : Packet
    {
        private long _inquireId;
        private long _requestId;
        private long _startPos;
        private long _totalCount;
        private long _totalSize;
        private int _pathLength;
        private string _path;
        public PacketFileContentInfoResponse(byte[] bytes) : base(bytes)
        {
        }

        public PacketFileContentInfoResponse(int clientId, long inquireId,long requestId,long startPos, long totalCount, long totalSize, string path) : base(PacketType.FileContentInfoResponse, clientId)
        {
            _inquireId = inquireId;
            _requestId = requestId;
            _startPos = startPos;
            _totalCount = totalCount;
            _totalSize = totalSize;
            _path = path;
        }
        /// <summary>
        /// 查询ID
        /// </summary>
        public long InquireId { get => _inquireId; set => _inquireId = value; }
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
        /// 请求ID
        /// </summary>
        public long RequestId { get => _requestId; set => _requestId = value; }
        public long StartPos { get => _startPos; set => _startPos = value; }

        protected override void Deserialize(byte[] bytes)
        {
            using (var stream = new ByteArrayStream(bytes))
            {
                _inquireId = stream.ReadInt64();
                _requestId = stream.ReadInt64();
                _startPos = stream.ReadInt64();
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
                stream.Write(_inquireId);
                stream.Write(_requestId);
                stream.Write(_startPos);
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
