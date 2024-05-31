using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Tools;

namespace FileSyncCommon.Messages
{
    public abstract class FileResponse : Response
    {
        protected string _path;
        /// <summary>
        /// 请求文件路径
        /// </summary>
        public string Path { get { return _path; } set { _path = value; } }
        protected FileResponse(MessageType messageType, int clientId, long requestId, bool latest, string path) : base(messageType, clientId, requestId, latest)
        {
            _path = path;
        }

        protected FileResponse(ByteArrayStream stream) : base(stream)
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
