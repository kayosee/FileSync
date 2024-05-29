using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Tools;

namespace FileSyncCommon.Messages   
{
    public class FileListInfoResponse : Response
    {
        private long _fileCount;
        private long _totalSize;
        /// <summary>
        /// 合计文件数量
        /// </summary>
        public long FileCount { get => _fileCount; set => _fileCount = value; }
        /// <summary>
        /// 合计文件容量
        /// </summary>
        public long TotalSize { get => _totalSize; set => _totalSize = value; }
        public FileListInfoResponse(ByteArrayStream stream) : base(stream)
        {
            _fileCount = stream.ReadInt64();
            _totalSize = stream.ReadInt64();
        }

        public FileListInfoResponse(int clientId, long requestId, long fileCount, long totalSize, bool latest) : base(MessageType.FileListInfoResponse, clientId, requestId, latest)
        {
            _fileCount = fileCount;
            _totalSize = totalSize;
        }
        protected override ByteArrayStream GetStream()
        {
            var stream = base.GetStream();

            stream.Write(_requestId);
            stream.Write(_fileCount);
            stream.Write(_totalSize);
            return stream;
        }
    }
}
