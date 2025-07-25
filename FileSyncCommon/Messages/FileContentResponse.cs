﻿using FileSyncCommon.Tools;
using Force.Crc32;

namespace FileSyncCommon.Messages;

public class FileContentResponse : FileResponse
{
    private byte _responseType;
    private long _pos;
    private long _lastWriteTime;
    private int _fileDataLength;
    private long _fileDataTotal;
    private uint _fileDataChecksum;
    private byte[] _fileData;
    public const int MaxDataSize = 8000000;
    /// <summary>
    /// 最后一个文件段
    /// </summary>
    public bool EndOfFile
    {
        get
        {
            return _pos + _fileDataLength >= _fileDataTotal;
        }
    }
    public long UntransmitCount
    {
        get
        {
            return (_fileDataTotal - _pos + _fileDataLength) / MaxDataSize;
        }
    }
    /// <summary>
    /// 数据位置
    /// </summary>
    public long Pos { get { return _pos; } set { _pos = value; } }
    /// <summary>
    /// 文件总长度
    /// </summary>
    public long FileDataTotal { get { return _fileDataTotal; } set { _fileDataTotal = value; } }
    /// <summary>
    /// 本节数据长度
    /// </summary>
    public int FileDataLength { get { return _fileDataLength; } set { _fileDataLength = value; } }
    /// <summary>
    /// 本节数据内容
    /// </summary>
    public byte[] FileData { get { return _fileData; } set { _fileData = value; } }
    /// <summary>
    /// 本节数据校验
    /// </summary>
    public uint FileDataChecksum { get { return _fileDataChecksum; } }
    /// <summary>
    /// 响应类型
    /// </summary>
    public FileResponseType ResponseType { get => (FileResponseType)_responseType; set => _responseType = (byte)value; }
    /// <summary>
    /// 上次修改时间
    /// </summary>
    public long LastWriteTime { get => _lastWriteTime; set => _lastWriteTime = value; }
    public override bool Equals(object? obj)
    {
        if (obj == null) return false;
        if (!(obj is FileContentResponse)) return false;
        FileContentResponse other = (FileContentResponse)obj;

        return other.Path == Path && other.Pos == Pos;
    }
    public FileContentResponse(int clientId, long requestId, FileResponseType responseType, string path, bool latest, Error error) : base(MessageType.FileContentResponse, clientId, requestId, latest, path, error)
    {
        _responseType = (byte)responseType;
    }
    public FileContentResponse(int clientId, long requestId, FileResponseType responseType, string path, bool latest) : base(MessageType.FileContentResponse, clientId, requestId, latest, path, Error.None)
    {
        _responseType = (byte)responseType;
    }
    protected override ByteArrayStream GetStream()    
    {
        var stream = base.GetStream();
        stream.Write(_responseType);
        stream.Write(_pos);
        stream.Write(_lastWriteTime);
        stream.Write(_fileDataLength);
        stream.Write(_fileDataTotal);

        if (_fileDataLength > 0)
        {
            _fileDataChecksum = Crc32Algorithm.Compute(_fileData);
            stream.Write(_fileDataChecksum);
            stream.Write(_fileData, 0, _fileData.Length);
        }

        return stream;
    }
    public FileContentResponse(ByteArrayStream stream):base(stream)
    {
        _responseType = stream.ReadByte();
        _pos = stream.ReadLong();
        _lastWriteTime = stream.ReadLong();
        _fileDataLength = stream.ReadInt();
        _fileDataTotal = stream.ReadLong();

        if (_fileDataLength > 0)
        {
            _fileDataChecksum = stream.ReadUInt();
            _fileData = new byte[_fileDataLength];
            stream.Read(_fileData, 0, _fileDataLength);
        }
    }

    public override int GetHashCode()
    {
        return this.Path.GetHashCode() + this.Pos.GetHashCode();
    }
}
