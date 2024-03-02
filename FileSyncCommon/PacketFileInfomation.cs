using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon;

public class PacketFileInfomation : Packet
{
    private long _createTime;
    private long _lastAccessTime;
    private long _lastWriteTime;
    private long _fileLength;
    private uint _checksum;
    private string _path;
    private int _pathLength;
    public PacketFileInfomation(int clientId, long createTime, long lastAccessTime, long lastWriteTime, long fileLength, uint checksum, string path) : base((int)PacketType.FileInformation, clientId)
    {
        _createTime = createTime;
        _lastAccessTime = lastAccessTime;
        _lastWriteTime = lastWriteTime;
        _fileLength = fileLength;
        _checksum = checksum;
        _path = path;
    }
    public PacketFileInfomation(byte[] bytes) : base(bytes)
    {
    }
    public long CreateTime { get => _createTime; set => _createTime = value; }
    public long LastAccessTime { get => _lastAccessTime; set => _lastAccessTime = value; }
    public long LastWriteTime { get => _lastWriteTime; set => _lastWriteTime = value; }
    public long FileLength { get => _fileLength; set => _fileLength = value; }
    public uint Checksum { get => _checksum; set => _checksum = value; }
    public string Path { get => _path; set => _path = value; }

    protected override void Deserialize(byte[] bytes)
    {
        if (bytes == null)
            throw new ArgumentException("bytes");

        using (var stream = new ByteArrayStream(bytes))
        {
            _createTime = stream.ReadInt64();
            _lastAccessTime = stream.ReadInt64();
            _lastWriteTime = stream.ReadInt64();
            _fileLength = stream.ReadInt64();
            _checksum = stream.ReadUInt32();

            _pathLength = stream.ReadInt32();
            var buffer = new byte[_pathLength];
            stream.Read(buffer, 0, _pathLength);
            _path = Encoding.UTF8.GetString(buffer, 0, _pathLength).Trim('\0');
        }
    }

    protected override byte[] Serialize()
    {
        if (string.IsNullOrEmpty(_path))
            throw new ArgumentException("path");

        using (var stream = new ByteArrayStream())
        {
            stream.Write(_createTime);
            stream.Write(_lastAccessTime);
            stream.Write(_lastWriteTime);
            stream.Write(_fileLength);
            stream.Write(_checksum);

            byte[] buffer = Encoding.UTF8.GetBytes(Path);
            _pathLength = buffer.Length;
            stream.Write(_pathLength);
            stream.Write(buffer, 0, _pathLength);

            return stream.GetBuffer();
        }
    }
}
