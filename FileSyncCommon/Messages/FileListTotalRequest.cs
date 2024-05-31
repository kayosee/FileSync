using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Tools;

namespace FileSyncCommon.Messages;

public class FileListTotalRequest : FileRequest
{
    private int _daysBefore;
    public int DaysBefore { get => _daysBefore; set => _daysBefore = value; }

    public FileListTotalRequest(int clientId, long requestId, int daysBefore, string path) : base(MessageType.FileListTotalRequest, clientId, requestId, path)
    {
        _daysBefore = daysBefore;
    }
    public FileListTotalRequest(ByteArrayStream stream) : base(stream)
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
