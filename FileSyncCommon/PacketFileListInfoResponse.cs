using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon
{
    public class PacketFileListInfoResponse : PacketResponse
    {
        private long _fileCount;
        private long _totalSize;
        public PacketFileListInfoResponse(byte[] bytes) : base(bytes)
        {
        }

        public PacketFileListInfoResponse(int clientId,long requestId,long fileCount,long totalSize) : base(PacketType.FileListInfoResponse, clientId, requestId)
        {
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
        protected override void Deserialize(byte[] bytes)
        {
            using (var stream = new ByteArrayStream(bytes))
            {
                _requestId = stream.ReadInt64();
                _fileCount = stream.ReadInt64();
                _totalSize = stream.ReadInt64();
            }
        }

        protected override byte[] Serialize()
        {
            using (var stream = new ByteArrayStream())
            {
                stream.Write(_requestId);
                stream.Write(_fileCount);
                stream.Write(_totalSize);
                return stream.GetBuffer();
            }
        }
    }
}
