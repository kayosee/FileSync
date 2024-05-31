using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Tools;

namespace FileSyncCommon.Messages;

public class FileListDetailResponse : FileResponse
{
    private long _createTime;
    private long _lastAccessTime;
    private long _lastWriteTime;
    private long _fileLength;
    private uint _checksum;
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
    public FileListDetailResponse(int clientId, long requestId, long createTime, long lastAccessTime, long lastWriteTime, long fileLength, uint checksum, string path, bool latest) : base(MessageType.FileListDetailResponse, clientId, requestId, latest, path)
    {
        _createTime = createTime;
        _lastAccessTime = lastAccessTime;
        _lastWriteTime = lastWriteTime;
        _fileLength = fileLength;
        _checksum = checksum;
    }
    public FileListDetailResponse(ByteArrayStream stream) : base(stream)
    {
        _createTime = stream.ReadLong();
        _lastAccessTime = stream.ReadLong();
        _lastWriteTime = stream.ReadLong();
        _fileLength = stream.ReadLong();
        _checksum = stream.ReadUInt();
    }

    protected override ByteArrayStream GetStream()
    {
        var stream = base.GetStream();

        stream.Write(_createTime);
        stream.Write(_lastAccessTime);
        stream.Write(_lastWriteTime);
        stream.Write(_fileLength);
        stream.Write(_checksum);

        return stream;
    }
}
