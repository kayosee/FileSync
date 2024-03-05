using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon
{
    public class PacketFileListInfoResponse : Packet
    {
        private long _inquireId;
        private long _fileCount;
        private long _totalSize;
        public PacketFileListInfoResponse(byte[] bytes) : base(bytes)
        {
        }

        public PacketFileListInfoResponse(int clientId,long inquireId,long fileCount,long totalSize) : base(PacketType.FileListInfoResponse, clientId)
        {
            _inquireId = inquireId;
            _fileCount = fileCount;
            _totalSize = totalSize;
        }
        /// <summary>
        /// 合计文件数量
        /// </summary>
        public long FileCount { get => _fileCount; set => _fileCount = value; }
        /// <summary>
        /// 合计文件容量
        /// </summary>
        public long TotalSize { get => _totalSize; set => _totalSize = value; }
        /// <summary>
        /// 请求ID
        /// </summary>
        public long InquireId { get => _inquireId; set => _inquireId = value; }

        protected override void Deserialize(byte[] bytes)
        {
            using (var stream = new ByteArrayStream(bytes))
            {
                _inquireId = stream.ReadInt64();
                _fileCount = stream.ReadInt64();
                _totalSize = stream.ReadInt64();
            }
        }

        protected override byte[] Serialize()
        {
            using (var stream = new ByteArrayStream())
            {
                stream.Write(_inquireId);
                stream.Write(_fileCount);
                stream.Write(_totalSize);
                return stream.GetBuffer();
            }
        }
    }
}
