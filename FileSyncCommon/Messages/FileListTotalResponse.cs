using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Tools;

namespace FileSyncCommon.Messages
{
    public class FileListTotalResponse : FileResponse
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
        public FileListTotalResponse(ByteArrayStream stream) : base(stream)
        {
            _fileCount = stream.ReadLong();
            _totalSize = stream.ReadLong();
        }
        public FileListTotalResponse(int clientId, long requestId, string path, bool latest,Error error) : base(MessageType.FileListTotalResponse, clientId, requestId, latest, path, error)
        {
        }
        public FileListTotalResponse(int clientId, long requestId, string path, long fileCount, long totalSize, bool latest) : base(MessageType.FileListTotalResponse, clientId, requestId, latest, path, Error.None)
        {
            _fileCount = fileCount;
            _totalSize = totalSize;
        }
        protected override ByteArrayStream GetStream()
        {
            var stream = base.GetStream();
            stream.Write(_fileCount);
            stream.Write(_totalSize);
            return stream;
        }
    }
}
