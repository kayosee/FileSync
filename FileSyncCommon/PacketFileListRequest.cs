using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon;

public class PacketFileListRequest : Packet
{
    private long _inquireId;
    private string _path;
    private int _pathLength;

    public string Path { get => _path; set => _path = value; }
    public long InquireId { get => _inquireId; set => _inquireId = value; }

    public PacketFileListRequest(int clientId,long inquireId, string path) : base(PacketType.FileInquire, clientId)
    {
        _inquireId = inquireId;
        _path = path;
    }
    public PacketFileListRequest(byte[] bytes) : base(bytes)
    {
    }
    protected override void Deserialize(byte[] bytes)
    {
        if (bytes == null)
            throw new ArgumentException("bytes");

        using (var stream = new ByteArrayStream(bytes))
        {
            _inquireId = stream.ReadInt64();
            _pathLength = stream.ReadInt32();
            var buffer = new byte[_pathLength];

            stream.Read(buffer, 0, _pathLength);
            _path = Encoding.UTF8.GetString(buffer, 0, _pathLength).Trim('\0');
        }
    }
    protected override byte[] Serialize()
    {
        if (string.IsNullOrEmpty(_path))
            throw new ArgumentException("path");

        using (var stream = new ByteArrayStream())
        {
            stream.Write(_inquireId);

            byte[] buffer = Encoding.UTF8.GetBytes(_path);
            _pathLength = buffer.Length;
            stream.Write(_pathLength);
            stream.Write(buffer, 0, _pathLength);

            return stream.GetBuffer();
        }
    }

}
