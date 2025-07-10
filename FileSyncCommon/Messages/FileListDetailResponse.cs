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
    private List<FileListDetail> _list;
    private int _length;
    public int Length { get { return _length; } set { _length = value; } }
    public List<FileListDetail> List { get { return _list; } set { _list = value; } }
    public FileListDetailResponse(int clientId, long requestId, string path, List<FileListDetail> list) : base(MessageType.FileListDetailResponse, clientId, requestId, true, path, Error.None)
    {
        _list = list;
        _length = list.Count;
    }
    public FileListDetailResponse(ByteArrayStream stream) : base(stream)
    {
        _length = stream.ReadInt();
        _list = new List<FileListDetail>();
        for (var i = 0; i < _length; i++)
        {
            _list.Add(new FileListDetail(stream));
        }
    }

    protected override ByteArrayStream GetStream()
    {
        var stream = base.GetStream();
        stream.Write(_length);
        foreach (var item in _list)
        {
            var bytes = item.GetBytes();
            stream.Write(bytes, 0, bytes.Length);
        }
        return stream;
    }
}
