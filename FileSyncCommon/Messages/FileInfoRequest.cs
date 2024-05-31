using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Tools;

namespace FileSyncCommon.Messages
{
    public class FileInfoRequest : FileRequest
    {
        private long _lastPos;
        private uint _checksum;
        /// <summary>
        /// 最后一次传输的位置
        /// </summary>
        public long LastPos { get => _lastPos; set => _lastPos = value; }
        /// <summary>
        /// 按LASTPOS校验文件，如果LASTPOS大于零
        /// </summary>
        public uint Checksum { get => _checksum; set => _checksum = value; }

        public FileInfoRequest(ByteArrayStream stream) : base(stream)
        {
            _lastPos = stream.ReadLong();
            _checksum = stream.ReadUInt();
        }

        public FileInfoRequest(int clientId, long requestId, long startPos, uint checksum, string path) : base(MessageType.FileInfoRequest, clientId, requestId, path)
        {
            _lastPos = startPos;
            _checksum = checksum;
        }
        protected override ByteArrayStream GetStream()
        {
            var stream = base.GetStream();
            stream.Write(_lastPos);
            stream.Write(_checksum);
            return stream;
        }
    }
}
