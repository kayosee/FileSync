using Force.Crc32;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon;

public class PacketFileContentDetailResponse : PacketResponse
{
    private byte _responseType;
    private int _pathLength;
    private string _path;
    private long _pos;
    private long _lastWriteTime;
    private int _fileDataLength;
    private long _fileDataTotal;
    private uint _fileDataChecksum;
    private byte[] _fileData;
    public const int MaxDataSize = 38284;
    public override bool Equals(object? obj)
    {
        if (obj == null) return false;
        if (!(obj is PacketFileContentDetailResponse)) return false;
        PacketFileContentDetailResponse other = obj as PacketFileContentDetailResponse;

        return other.Path == Path && other.Pos == Pos;
    }
    public PacketFileContentDetailResponse(int clientId,long requestId, FileResponseType responseType, string path) : base(PacketType.FileContentDetailResponse, clientId, requestId)
    {
        _responseType = (byte)responseType;
        _path = path;
    }
    public PacketFileContentDetailResponse(byte[] bytes) : base(bytes)
    {
    }
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
    /// 路径
    /// </summary>
    public string Path
    {
        get => _path; set
        {
            _path = value;
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
    protected override byte[] Serialize()
    {
        using (var stream = new ByteArrayStream())
        {
            stream.Write(_requestId);
            stream.Write(_responseType);

            byte[] buffer = Encoding.UTF8.GetBytes(Path);
            _pathLength = buffer.Length;
            stream.Write(_pathLength);

            stream.Write(buffer, 0, buffer.Length);
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

            return stream.GetBuffer();
        }
    }
    protected override void Deserialize(byte[] bytes)
    {
        if (bytes == null)
            throw new ArgumentException("bytes");

        using (var stream = new ByteArrayStream(bytes))
        {
            _requestId = stream.ReadInt64();
            _responseType = stream.ReadByte();
            _pathLength = stream.ReadInt32();

            byte[] buffer = new byte[_pathLength];
            stream.Read(buffer, 0, _pathLength);
            _path = Encoding.UTF8.GetString(buffer).TrimEnd('\0');

            _pos = stream.ReadInt64();
            _lastWriteTime = stream.ReadInt64();
            _fileDataLength = stream.ReadInt32();
            _fileDataTotal = stream.ReadInt64();

            if (_fileDataLength > 0)
            {
                _fileDataChecksum = stream.ReadUInt32();
                _fileData = new byte[_fileDataLength];
                stream.Read(_fileData, 0, _fileDataLength);
            }
        }
    }
}
