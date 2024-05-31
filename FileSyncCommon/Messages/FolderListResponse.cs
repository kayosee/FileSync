using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Tools;

namespace FileSyncCommon.Messages
{
    public class FolderListResponse : FileResponse
    {
        private string _folderList;
        public string[] FolderList
        {
            get
            {
                if (string.IsNullOrEmpty(_folderList))
                {
                    return new string[0];
                }
                return _folderList.Split(";");
            }
        }
        public FolderListResponse(ByteArrayStream stream) : base(stream)
        {
            _folderList = stream.ReadUTF8String();
        }
        public FolderListResponse(int clientId, long requestId, string path, string[] folderList) : base(MessageType.FolderListResponse, clientId, requestId, true, path)
        {
            if (folderList != null && folderList.Length > 0)
                _folderList = string.Join(";", folderList);
            else
                _folderList = string.Empty;
        }
        protected override ByteArrayStream GetStream()
        {
            var stream = base.GetStream();
            stream.WriteUTF8string(_folderList);
            return stream;
        }
    }
}
