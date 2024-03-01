using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon;

public class FileNotification : Packet
{
    private long _lastWriteTime;
    private long _fileLength;
    private int _pathLength;
    private string _path;
    public FileNotification(int clientId, long lastWriteTime, long fileLength, string path) : base((int)PacketType.FileNotification, clientId)
    {
        _lastWriteTime = lastWriteTime;
        _fileLength = fileLength;
        _path = path;
    }
    public FileNotification(byte[] bytes) : base(bytes)
    {
    }

    public FileNotification(Packet packet) : base(packet)
    {
    }

    public string Path
    {
        get => _path; set
        {
            _path = value;
        }
    }
    public long FileLength { get => _fileLength; set => _fileLength = value; }
    public long LastWriteTime { get => _lastWriteTime; set => _lastWriteTime = value; }

    protected override void Deserialize(byte[] bytes)
    {
        if (bytes == null)
            throw new ArgumentException("bytes");

        using (var stream = new ByteArrayStream(bytes))
        {
            _lastWriteTime = stream.ReadInt64();
            _fileLength = stream.ReadInt64();
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
            stream.Write(_lastWriteTime);
            stream.Write(_fileLength);

            byte[] buffer = Encoding.UTF8.GetBytes(Path);
            _pathLength = buffer.Length;
            stream.Write(_pathLength);

            stream.Write(buffer, 0, _pathLength);

            return stream.GetBuffer();
        }
    }

    public override bool Equals(object? obj)
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));
        if (obj is FileNotification)
        {
            FileNotification o = (FileNotification)obj;
            return o.Path == Path;
        }
        return false;
    }

    public override IEnumerable<Packet>? Process(string folder)
    {
        var file = System.IO.Path.Combine(folder, Path.TrimStart(System.IO.Path.DirectorySeparatorChar));

        var localFileInfo = new FileInfo(file);
        if (!localFileInfo.Exists)
        {
            if (File.Exists(file + ".sync"))
            {
                var pos = FileOperator.GetLastPosition(file + ".sync");
                var request = new FileRequest(ClientId, pos, Path);
                return new Packet[] { request };
            }
            else
            {
                var request = new FileRequest(ClientId, 0, Path);
                return new Packet[] { request };
            }
        }
        else
        {
            if (localFileInfo.Length == FileLength && localFileInfo.LastWriteTime.Ticks == LastWriteTime)//请求CHECKSUM，看看是不是一样
            {
                var inquire = new FileInquire(ClientId, Path);
                return new Packet[] { inquire };
            }
            else
            {
                var request = new FileRequest(ClientId, 0, Path);
                return new Packet[] { request };
            }
        }

        return null;
    }
}
