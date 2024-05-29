using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Tools;

namespace FileSyncCommon.Messages
{
    public class FolderListResponse : Response
    {
        private string _path;
        private int _pathLength;
        private int _folderListLength;
        private string _folderList;
        public string Path { get => _path; set => _path = value; }
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
            _pathLength = stream.ReadInt32();

            var buffer = new byte[_pathLength];
            stream.Read(buffer, 0, _pathLength);
            _path = Encoding.UTF8.GetString(buffer).Trim('\0');

            _folderListLength = stream.ReadInt32();
            buffer = new byte[_folderListLength];
            stream.Read(buffer, 0, _folderListLength);
            _folderList = Encoding.UTF8.GetString(buffer).Trim('\0');
        }
        public FolderListResponse(int clientId, long requestId, string path, string[] folderList) : base(MessageType.FolderListResponse, clientId, requestId, true)
        {
            _path = path;
            if (folderList != null && folderList.Length > 0)
                _folderList = string.Join(";", folderList);
            else
                _folderList = string.Empty;
        }
        protected override ByteArrayStream GetStream()
        {
            var stream = base.GetStream();

            var buffer = Encoding.UTF8.GetBytes(_path);
            _pathLength = buffer.Length;
            stream.Write(_pathLength);
            stream.Write(buffer, 0, _pathLength);

            buffer = Encoding.UTF8.GetBytes(string.Join(";", _folderList));
            _folderListLength = buffer.Length;
            stream.Write(_folderListLength);
            stream.Write(buffer, 0, _folderListLength);

            return stream;
        }
    }
}
