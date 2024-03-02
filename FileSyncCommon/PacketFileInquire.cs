using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon;

public class PacketFileInquire : Packet
{
    private string _path;
    private int _pathLength;

    public string Path { get => _path; set => _path = value; }
    public PacketFileInquire(int clientId, string path) : base((int)PacketType.FileInquire, clientId)
    {
        _path = path;
    }
    public PacketFileInquire(byte[] bytes) : base(bytes)
    {
    }
    protected override void Deserialize(byte[] bytes)
    {
        if (bytes == null)
            throw new ArgumentException("bytes");

        using (var stream = new ByteArrayStream(bytes))
        {
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
            byte[] buffer = Encoding.UTF8.GetBytes(Path);
            _pathLength = buffer.Length;
            stream.Write(_pathLength);

            stream.Write(buffer, 0, _pathLength);

            return stream.GetBuffer();
        }
    }

}
