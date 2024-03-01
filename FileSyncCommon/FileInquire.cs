using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon;

public class FileInquire : Packet
{
    private string _path;
    private int _pathLength;

    public string Path { get => _path; set => _path = value; }
    public FileInquire(int clientId, string path) : base((int)PacketType.FileInquire, clientId)
    {
        _path = path;
    }
    public FileInquire(byte[] bytes) : base(bytes)
    {
    }

    public FileInquire(Packet packet) : base(packet)
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
    public override IEnumerable<Packet>? Process(string folder)
    {
        var file = System.IO.Path.Combine(folder, Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
        if (File.Exists(file))
        {
            var fileInfo = new FileInfo(file);
            var fileInformation = new FileInfomation(
                ClientId,
                fileInfo.CreationTime.Ticks,
                fileInfo.LastAccessTime.Ticks,
                fileInfo.LastWriteTime.Ticks,
                fileInfo.Length,
                ChecksumHelper.GetCrc32(file).GetValueOrDefault(),
                Path
                );
            return new Packet[] { fileInformation };
        }
        return null;
    }

}
