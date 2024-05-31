using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Tools;

namespace FileSyncCommon.Messages;

public class FileListDetailRequest : FileRequest
{
    private int _daysBefore;
    public int DaysBefore { get => _daysBefore; set => _daysBefore = value; }

    public FileListDetailRequest(int clientId, long requestId, int daysBefore, string path) : base(MessageType.FileListDetailRequest, clientId, requestId, path)
    {
        _daysBefore = daysBefore;
    }
    public FileListDetailRequest(ByteArrayStream stream) : base(stream)
    {
        _daysBefore = stream.ReadInt();
    }
    protected override ByteArrayStream GetStream()
    {
        var stream = base.GetStream();
        stream.Write(_daysBefore);
        return stream;
    }

}
