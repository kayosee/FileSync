using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon;

public class FilePosition
{
    public readonly int Length = Mark.Length + sizeof(long);
    public const string Mark = "LASTPOST";
    public long Position { get; set; }

    private void Parse(byte[] data)
    {
        if (data == null || data.Length < Length)
            throw new InvalidDataException("data 参数无效");

        var str = Encoding.ASCII.GetString(data, 0, Mark.Length);
        if (str != Mark)
            throw new InvalidDataException("data 参数无效");

        Position = BitConverter.ToInt64(data, Mark.Length);

    }
    public FilePosition(byte[] data)
    {
        Parse(data);
    }
    public FilePosition(long position)
    {
        Position = position;
    }

    public FilePosition(string path)
    {
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            stream.Seek(0 - Length, SeekOrigin.End);
            var buffer = new byte[Length];
            stream.Read(buffer, 0, buffer.Length);
            stream.Close();

            Parse(buffer);
        }
    }
    public byte[] GetBytes()
    {
        using (var buffer = new ByteArrayStream(Mark.Length + sizeof(long)))
        {
            buffer.Write(Encoding.ASCII.GetBytes(Mark), 0, Mark.Length);
            buffer.Write(Position);
            return buffer.GetBuffer();
        }

    }
}
