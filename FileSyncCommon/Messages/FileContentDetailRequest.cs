using FileSyncCommon.Tools;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon.Messages;

public class FileContentDetailRequest : Request
{
    private long _startPos;
    private long _endPos;
    private int _pathLength;
    private string _path;
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
    public FileContentDetailRequest(int clientId, long requestId, long startPos, string path) : base(MessageType.FileContentDetailRequest, clientId, requestId)
    {
        _startPos = startPos;
        _path = path;
    }

    public FileContentDetailRequest(ByteArrayStream stream):base(stream)
    {
        _startPos = stream.ReadInt64();
        _endPos = stream.ReadInt64();
        _pathLength = stream.ReadInt32();

        var buffer = new byte[_pathLength];
        stream.Read(buffer, 0, _pathLength);
        _path = Encoding.UTF8.GetString(buffer).Trim('\0');
    }
    protected override ByteArrayStream GetStream()
    {
        var stream = base.GetStream();

        stream.Write(_startPos);
        stream.Write(_endPos);

        var buffer = Encoding.UTF8.GetBytes(_path);
        _pathLength = buffer.Length;
        stream.Write(_pathLength);

        stream.Write(buffer, 0, _pathLength);
        return stream;

    }
}
