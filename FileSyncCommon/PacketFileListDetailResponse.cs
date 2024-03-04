using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon;

public class PacketFileListDetailResponse : Packet
{
    private long _total;
    private long _inquireId;
    private long _createTime;
    private long _lastAccessTime;
    private long _lastWriteTime;
    private long _fileLength;
    private uint _checksum;
    private string _path;
    private int _pathLength;
    public PacketFileListDetailResponse(int clientId, long inquireId, long createTime, long lastAccessTime, long lastWriteTime, long fileLength, uint checksum, string path) : base(PacketType.FileDetailInfo, clientId)
    {
        _inquireId = inquireId;
        _createTime = createTime;
        _lastAccessTime = lastAccessTime;
        _lastWriteTime = lastWriteTime;
        _fileLength = fileLength;
        _checksum = checksum;
        _path = path;
    }
    public PacketFileListDetailResponse(byte[] bytes) : base(bytes)
    {
    }
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
    /// <summary>
    /// 文件路径
    /// </summary>
    public string Path { get => _path; set => _path = value; }
    /// <summary>
    /// 查询的ID
    /// </summary>
    public long InquireId { get => _inquireId; set => _inquireId = value; }
    /// <summary>
    /// 总共多少个INFORMATION
    /// </summary>
    public long Total { get => _total; set => _total = value; }

    protected override void Deserialize(byte[] bytes)
    {
        if (bytes == null)
            throw new ArgumentException("bytes");

        using (var stream = new ByteArrayStream(bytes))
        {
            _total = stream.ReadInt64();
            _inquireId = stream.ReadInt64();
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
            stream.Write(_total);
            stream.Write(_inquireId);
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
