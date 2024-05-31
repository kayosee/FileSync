using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Tools;

namespace FileSyncCommon.Messages
{
    public abstract class FileRequest : Request
    {
        private string _path;
        /// <summary>
        /// 请求文件路径
        /// </summary>
        public string Path { get => _path; set => _path = value; }

        /// <summary>
        /// 请求ID
        /// </summary>
        protected FileRequest(MessageType messageType, int clientId, long requestId, string path) : base(messageType, clientId, requestId)
        {
            _path = path;
        }
        protected FileRequest(ByteArrayStream stream) : base(stream)
        {
            _path = stream.ReadUTF8String();
        }
        protected override ByteArrayStream GetStream()
        {
            var stream = base.GetStream();
            stream.WriteUTF8string(_path);
            return stream;
        }
    }
}
