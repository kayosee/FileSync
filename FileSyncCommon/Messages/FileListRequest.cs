using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Tools;

namespace FileSyncCommon.Messages;

public class FileListRequest : Request
{
    private string _path;
    private int _pathLength;
    private int _daysBefore;
    public string Path { get => _path; set => _path = value; }
    public int DaysBefore { get => _daysBefore; set => _daysBefore = value; }

    public FileListRequest(int clientId, long requestId, int daysBefore, string path) : base(MessageType.FileListRequest, clientId, requestId)
    {
        _daysBefore = daysBefore;
        _path = path;
    }
    public FileListRequest(ByteArrayStream stream) : base(stream)
    {
        _daysBefore = stream.ReadInt32();
        _pathLength = stream.ReadInt32();
        var buffer = new byte[_pathLength];

        stream.Read(buffer, 0, _pathLength);
        _path = Encoding.UTF8.GetString(buffer, 0, _pathLength).Trim('\0');

    }
    protected override ByteArrayStream GetStream()
    {
        var stream = base.GetStream();
        stream.Write(_daysBefore);
        byte[] buffer = Encoding.UTF8.GetBytes(_path);
        _pathLength = buffer.Length;
        stream.Write(_pathLength);
        stream.Write(buffer, 0, _pathLength);

        return stream;
    }

}
