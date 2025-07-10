using FileSyncCommon.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon.Messages
{
    public class FileListDetail
    {
        private string _path;
        private long _createTime;
        private long _lastAccessTime;
        private long _lastWriteTime;
        private long _fileLength;
        private uint _checksum;
        public string Path { get => _path; set => _path = value; }
        /// <summary>
        /// 文件创建时间
        /// </summary>
        public long CreateTime { get => _createTime; set => _createTime = value; }
        /// <summary>
        /// 文件最后一次访问时间
        /// </summary>
        public long LastAccessTime { get => _lastAccessTime; set => _lastAccessTime = value; }
        /// <summary>
        /// 文件最后一次修改时间
        /// </summary>
        public long LastWriteTime { get => _lastWriteTime; set => _lastWriteTime = value; }
        /// <summary>
        /// 文件内容字节长度
        /// </summary>
        public long FileLength { get => _fileLength; set => _fileLength = value; }
        /// <summary>
        /// 文件内容校验和
        /// </summary>
        public uint Checksum { get => _checksum; set => _checksum = value; }
        public FileListDetail(string fileName, long createTime, long lastAccessTime, long lastWriteTime, long fileLength, uint checksum)
        {
            Path = fileName;
            CreateTime = createTime;
            LastAccessTime = lastAccessTime;
            LastWriteTime = lastWriteTime;
            FileLength = fileLength;
            Checksum = checksum;
        }

        public FileListDetail(ByteArrayStream stream)
        {
            _path = stream.ReadUTF8String();
            _createTime = stream.ReadLong();
            _lastAccessTime = stream.ReadLong();
            _lastWriteTime = stream.ReadLong();
            _fileLength = stream.ReadLong();
            _checksum = stream.ReadUInt();
        }
        public byte[] GetBytes()
        {
            using (var ms = new ByteArrayStream())
            {
                ms.WriteUTF8string(_path);
                ms.Write(_createTime);
                ms.Write(LastAccessTime);
                ms.Write(LastWriteTime);
                ms.Write(_fileLength);
                ms.Write(Checksum);
                return ms.GetBuffer();
            }
        }
    }
}
