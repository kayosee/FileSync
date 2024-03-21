using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon
{
    public class PacketFolderListResponse : PacketRequest
    {
        private string _path;
        private int _pathLength;
        private int _folderListLength;
        private string[] _folderList;
        public PacketFolderListResponse(byte[] bytes) : base(bytes)
        {
        }

        public PacketFolderListResponse(int clientId, long requestId, string path, string[] folderList) : base(PacketType.FolderListRequest, clientId, requestId)
        {
            _path = path;
            _folderList = folderList;
        }

        public string Path { get => _path; set => _path = value; }

        protected override void Deserialize(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentException("bytes");

            using (var stream = new ByteArrayStream(bytes))
            {
                _requestId = stream.ReadInt64();
                _pathLength = stream.ReadInt32();

                var buffer = new byte[_pathLength];
                stream.Read(buffer, 0, _pathLength);
                _path = Encoding.UTF8.GetString(buffer).Trim('\0');

                _folderListLength = stream.ReadInt32();

                buffer = new byte[_folderListLength];
                stream.Read(buffer, 0, _folderListLength);
                _folderList = Encoding.UTF8.GetString(buffer).Trim('\0').Split(';');

            }
        }
        protected override byte[] Serialize()
        {
            using (var stream = new ByteArrayStream())
            {
                stream.Write(_requestId);

                var buffer = Encoding.UTF8.GetBytes(_path);
                _pathLength = buffer.Length;
                stream.Write(_pathLength);
                stream.Write(buffer, 0, _pathLength);

                buffer = Encoding.UTF8.GetBytes(string.Join(";", _folderList));
                _folderListLength = buffer.Length;
                stream.Write(_folderListLength);
                stream.Write(buffer, 0, _folderListLength);

                return stream.GetBuffer();
            }
        }
    }
}
