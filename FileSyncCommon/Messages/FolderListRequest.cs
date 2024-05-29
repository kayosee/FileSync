using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Tools;

namespace FileSyncCommon.Messages   
{
    public class FolderListRequest : Request
    {
        private string _path;
        private int _pathLength;
        public FolderListRequest(ByteArrayStream stream) : base(stream)
        {
            _pathLength = stream.ReadInt32();

            var buffer = new byte[_pathLength];
            stream.Read(buffer, 0, _pathLength);
            _path = Encoding.UTF8.GetString(buffer).Trim('\0');
        }
        public FolderListRequest(int clientId, long requestId, string path) : base(MessageType.FolderListRequest, clientId, requestId)
        {
            _path = path;
        }
        public string Path { get => _path; set => _path = value; }
        protected override ByteArrayStream GetStream()
        {
            var stream = base.GetStream();

            var buffer = Encoding.UTF8.GetBytes(_path);
            _pathLength = buffer.Length;
            stream.Write(_pathLength);

            stream.Write(buffer, 0, _pathLength);
            return stream;
        }
    }
}
