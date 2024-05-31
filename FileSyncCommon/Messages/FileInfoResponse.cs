using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Tools;

namespace FileSyncCommon.Messages
{
    public class FileInfoResponse : FileResponse
    {
        private long _lastPos;
        private uint _checksum;
        private long _totalCount;
        private long _totalSize;
        /// <summary>
        /// 总共分片数量
        /// </summary>
        public long TotalCount { get => _totalCount; set => _totalCount = value; }
        /// <summary>
        /// 总共文件大小
        /// </summary>
        public long TotalSize { get => _totalSize; set => _totalSize = value; }
        /// <summary>
        /// 最后传输的位置
        /// </summary>
        public long LastPos { get => _lastPos; set => _lastPos = value; }
        /// <summary>
        /// 按照传输的位置计算检验（LASTPOS>0）
        /// </summary>
        public uint Checksum { get => _checksum; set => _checksum = value; }
        public FileInfoResponse(ByteArrayStream stream) : base(stream)
        {
            _lastPos = stream.ReadLong();
            _checksum = stream.ReadUInt();
            _totalCount = stream.ReadLong();
            _totalSize = stream.ReadLong();
        }

        public FileInfoResponse(int clientId, long requestId, long lastPos, uint checksum, long totalCount, long totalSize, string path) : base(MessageType.FileInfoResponse, clientId, requestId, true, path)
        {
            _lastPos = lastPos;
            _checksum = checksum;
            _totalCount = totalCount;
            _totalSize = totalSize;
        }

        protected override ByteArrayStream GetStream()
        {
            var stream = base.GetStream();
            stream.Write(_lastPos);
            stream.Write(_checksum);
            stream.Write(_totalCount);
            stream.Write(_totalSize);
            return stream;
        }
    }
}
