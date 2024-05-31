using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Tools;

namespace FileSyncCommon.Messages   
{
    public class FolderListRequest : FileRequest
    {
        public FolderListRequest(ByteArrayStream stream) : base(stream)
        {
        }
        public FolderListRequest(int clientId, long requestId, string path) : base(MessageType.FolderListRequest, clientId, requestId,path)
        {
        }
        protected override ByteArrayStream GetStream()
        {
            var stream = base.GetStream();
            return stream;
        }
    }
}
