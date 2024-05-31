using FileSyncCommon.Tools;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon.Messages;

public class FileContentRequest : FileRequest
{
    private long _startPos;
    private long _endPos;
    /// <summary>
    /// 请求文件的起始位置
    /// </summary>
    public long StartPos { get => _startPos; set => _startPos = value; }
    public long EndPos { get => _endPos; set => _endPos = value; }
    public FileContentRequest(int clientId, long requestId, long startPos, string path) : base(MessageType.FileContentRequest, clientId, requestId, path)
    {
        _startPos = startPos;
    }

    public FileContentRequest(ByteArrayStream stream) : base(stream)
    {
        _startPos = stream.ReadLong();
        _endPos = stream.ReadLong();
    }
    protected override ByteArrayStream GetStream()
    {
        var stream = base.GetStream();
        stream.Write(_startPos);
        stream.Write(_endPos);
        return stream;

    }
}
