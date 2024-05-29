using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Tools;

namespace FileSyncCommon.Messages;

public class FileListDetailResponse : Response
{
    private long _createTime;
    private long _lastAccessTime;
    private long _lastWriteTime;
    private long _fileLength;
    private uint _checksum;
    private string _path;
    private int _pathLength;
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
    public FileListDetailResponse(int clientId, long requestId, long createTime, long lastAccessTime, long lastWriteTime, long fileLength, uint checksum, string path, bool latest) : base(MessageType.FileListDetailResponse, clientId, requestId, latest)
    {
        _createTime = createTime;
        _lastAccessTime = lastAccessTime;
        _lastWriteTime = lastWriteTime;
        _fileLength = fileLength;
        _checksum = checksum;
        _path = path;
    }
    public FileListDetailResponse(ByteArrayStream stream) : base(stream)
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

    protected override ByteArrayStream GetStream()
    {
        var stream = base.GetStream();

        stream.Write(_createTime);
        stream.Write(_lastAccessTime);
        stream.Write(_lastWriteTime);
        stream.Write(_fileLength);
        stream.Write(_checksum);

        byte[] buffer = Encoding.UTF8.GetBytes(Path);
        _pathLength = buffer.Length;
        stream.Write(_pathLength);
        stream.Write(buffer, 0, _pathLength);

        return stream;
    }
}
