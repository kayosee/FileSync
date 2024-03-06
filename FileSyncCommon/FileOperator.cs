using Force.Crc32;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace FileSyncCommon;

public class FileOperator
{
    private const int BufferSize = 1024 * 1024 * 1024;
    public static uint? GetCrc32(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        else
        {
            using (var stream = File.OpenRead(path))
            {
                uint initial = 0;
                var buffer = new byte[BufferSize];
                int nret = 0;
                while ((nret = stream.Read(buffer)) > 0)
                {
                    initial = Crc32Algorithm.Append(initial, buffer.Take(nret).ToArray());
                }
                return initial;
            }
        }
    }

    public static uint? GetCrc32(string path, long endPos)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        else
        {
            using (var stream = File.OpenRead(path))
            {
                uint initial = 0;
                var total = 0;
                var buffer = new byte[BufferSize];
                int nret = 0;
                while ((nret = stream.Read(buffer)) > 0 && total < endPos)
                {
                    if (total + nret > endPos)
                    {
                        nret = (int)endPos - total;
                    }
                    initial = Crc32Algorithm.Append(initial, buffer.Take(nret).ToArray());
                    total += nret;
                }
                return initial;
            }
        }
    }


    public static long GetLastPosition(string path)
    {
        try
        {
            var filePosition = new FilePosition(path);
            return filePosition.Position;
        }
        catch (Exception e)
        {
            Log.Error(e.Message);
            Log.Error(e.StackTrace);
            return 0;
        }
    }

    public static void WriteFile(string path, long position, byte[] bytes, long? filePosition)
    {
        if (!Path.Exists(path))
        {
            var folder = Path.GetDirectoryName(path);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }

        var temp = new List<byte>();
        temp.AddRange(bytes);

        if (filePosition != null)
            temp.AddRange(new FilePosition(filePosition.Value).GetBytes());

        using (var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 0, FileOptions.WriteThrough))
        {
            var x = stream.Seek(position, SeekOrigin.Begin);
            Debug.Assert(x == position);

            stream.Write(temp.ToArray(), 0, temp.Count);
            stream.Flush();
            stream.Close();
        }

        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
        {
            var x = stream.Seek(position, SeekOrigin.Begin);
            if (x != position)
                throw new FileSeekException(path, position);

            var buffer = new byte[bytes.Length];
            stream.Read(buffer, 0, buffer.Length);
            stream.Close();

            var newChecksum = Crc32Algorithm.Compute(buffer);
            var oldChecksum = Crc32Algorithm.Compute(bytes);
            if (newChecksum != oldChecksum)
                throw new FileChecksumException(path, position, oldChecksum, newChecksum);
        }
    }
    public static int AppendFile(string path, byte[] bytes)
    {
        try
        {
            using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.None, 0, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
                stream.Close();
                return bytes.Length;
            }
        }
        catch (Exception e)
        {
            Log.Error(e.Message);
            Log.Error(e.StackTrace);
            return 0;
        }
    }
    public static void SetupFile(string path, long lastWriteTime)
    {
        File.Move(path + ".sync", path, true);
        FileInfo fi = new FileInfo(path);
        fi.LastWriteTime = DateTime.FromBinary(lastWriteTime);
    }
}
