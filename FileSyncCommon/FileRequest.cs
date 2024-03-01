using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon;

public class FileRequest : Packet
{
    private long _startPos;
    private long _endPos;
    private int _pathLength;
    private string _path;
    public FileRequest(int clientId, long startPos, string path) : base((int)PacketType.FileRequest, clientId)
    {
        _startPos = startPos;
        _path = path;
    }
    public FileRequest(byte[] bytes) : base(bytes) { }

    public FileRequest(Packet packet) : base(packet)
    {
    }

    public long StartPos { get => _startPos; set => _startPos = value; }

    public string Path
    {
        get => _path; set
        {
            _path = value;
        }
    }

    public long EndPos { get => _endPos; set => _endPos = value; }

    protected override void Deserialize(byte[] bytes)
    {
        if (bytes == null)
            throw new ArgumentException("bytes");

        using (var stream = new ByteArrayStream(bytes))
        {
            _startPos = stream.ReadInt64();
            _endPos = stream.ReadInt64();
            _pathLength = stream.ReadInt32();

            var buffer = new byte[_pathLength];
            stream.Read(buffer, 0, _pathLength);
            _path = Encoding.UTF8.GetString(buffer).Trim('\0');
        }
    }
    protected override byte[] Serialize()
    {
        using (var stream = new ByteArrayStream())
        {
            stream.Write(_startPos);
            stream.Write(_endPos);

            var buffer = Encoding.UTF8.GetBytes(_path);
            _pathLength = buffer.Length;
            stream.Write(_pathLength);

            stream.Write(buffer, 0, _pathLength);
            return stream.GetBuffer();
        }
    }

    public override IEnumerable<Packet>? Process(string folder)
    {
        var localPath = System.IO.Path.Combine(folder, Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
        if (!File.Exists(localPath))
            yield return new FileResponse(ClientId, (byte)FileResponseType.FileDeleted, Path);
        else if (!Readable(localPath))
        {
            yield return new FileResponse(ClientId, (byte)FileResponseType.FileReadError, Path);
        }
        else
        {
            using (var stream = File.OpenRead(localPath))
            {
                if (StartPos > stream.Length)
                {
                    Log.Error($"请求的位置{StartPos}超出该文件'{localPath}'的大小{stream.Length}");
                    yield return new FileResponse(ClientId, (byte)FileResponseType.FileReadError, Path);
                }
                if (stream.Length == 0)
                {
                    yield return new FileResponse(ClientId, (byte)FileResponseType.Empty, Path);
                }
                stream.Seek(StartPos, SeekOrigin.Begin);

                var lastWriteTime = new FileInfo(localPath).LastWriteTime.Ticks;
                var buffer = new byte[FileResponse.MaxDataSize];
                while (stream.Position < stream.Length)
                {
                    var response = new FileResponse(ClientId, (byte)FileResponseType.Content, Path);
                    response.Pos = stream.Position;
                    response.FileDataLength = stream.Read(buffer);
                    response.FileData = buffer.Take(response.FileDataLength).ToArray();
                    response.FileDataTotal = stream.Length;
                    response.LastWriteTime = lastWriteTime;
                    Log.Debug($"正在发送：{response.Path},位置:{response.Pos},长度:{response.FileData.Length},总共:{response.FileDataTotal}");
                    yield return response;
                }
            }
        }
    }

    public bool Readable(string path)
    {
        try
        {
            File.Open(path, FileMode.Open, FileAccess.Read).Dispose();
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
