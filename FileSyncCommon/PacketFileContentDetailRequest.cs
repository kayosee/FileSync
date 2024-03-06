using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon;

public class PacketFileContentDetailRequest : PacketRequest
{
    private long _startPos;
    private long _endPos;
    private int _pathLength;
    private string _path;
    public PacketFileContentDetailRequest(int clientId, long requestId, long startPos, string path) : base(PacketType.FileContentDetailRequest, clientId,requestId)
    {
        _startPos = startPos;
        _path = path;
    }
    public PacketFileContentDetailRequest(byte[] bytes) : base(bytes) { }
    /// <summary>
    /// 请求文件的起始位置
    /// </summary>
    public long StartPos { get => _startPos; set => _startPos = value; }
    /// <summary>
    /// 请求文件路径
    /// </summary>
    public string Path
    {
        get => _path; set
        {
            _path = value;
        }
    }
    public long EndPos { get => _endPos; set => _endPos = value; }

    protected override void Deserialize(byte[] bytes)
    {
        if (bytes == null)
            throw new ArgumentException("bytes");

        using (var stream = new ByteArrayStream(bytes))
        {
            _requestId = stream.ReadInt64();
            _startPos = stream.ReadInt64();
            _endPos = stream.ReadInt64();
            _pathLength = stream.ReadInt32();

            var buffer = new byte[_pathLength];
            stream.Read(buffer, 0, _pathLength);
            _path = Encoding.UTF8.GetString(buffer).Trim('\0');
        }
    }
    protected override byte[] Serialize()
    {
        using (var stream = new ByteArrayStream())
        {
            stream.Write(_requestId);
            stream.Write(_startPos);
            stream.Write(_endPos);

            var buffer = Encoding.UTF8.GetBytes(_path);
            _pathLength = buffer.Length;
            stream.Write(_pathLength);

            stream.Write(buffer, 0, _pathLength);
            return stream.GetBuffer();
        }
    }
}
