﻿using Force.Crc32;
using Serilog;
using System.Diagnostics;

namespace FileSyncCommon.Tools;

public class FileOperator
{
    private const int BufferSize = 1024 * 1024 * 128;//128KB
    public static uint GetCrc32(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(path);
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
    public static uint GetCrc32(string path, long endPos)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(path);
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

        if (filePosition != null)
        {
            var suffix = new FilePosition(filePosition.Value).GetBytes();
            bytes = bytes.Concat(suffix).ToArray();
        }

        using (var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 0, FileOptions.WriteThrough))
        {
            var x = stream.Seek(position, SeekOrigin.Begin);
            Debug.Assert(x == position);

            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
            stream.Close();
        }
        /*
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
        {
            var x = stream.Seek(position, SeekOrigin.Begin);
            if (x != position)
                throw new Exceptions.FileSeekException(path, position);

            var buffer = new byte[bytes.Length];
            stream.Read(buffer, 0, buffer.Length);
            stream.Close();

            var newChecksum = Crc32Algorithm.Compute(buffer);
            var oldChecksum = Crc32Algorithm.Compute(bytes);
            if (newChecksum != oldChecksum)
                throw new Exceptions.FileChecksumException(path, position, oldChecksum, newChecksum);
        }
        */
    }
    public static void SetupFile(string path, long lastWriteTime)
    {
        File.Move(path + ".sync", path, true);
        FileInfo fi = new FileInfo(path);
        fi.LastWriteTime = DateTime.FromBinary(lastWriteTime);
    }
    public static void DeleteOldFile(string path, DateTime dateBefore)
    {
        DirectoryInfo directoryInfo = new DirectoryInfo(path);
        if (!directoryInfo.Exists)
            throw new DirectoryNotFoundException(path);

        var files = directoryInfo.GetFiles("*.*").Where(f => f.CreationTime <= dateBefore);
        foreach (var file in files)
            file.Delete();

        var dirs = directoryInfo.GetDirectories();
        foreach (var dir in dirs)
            DeleteOldFile(dir.FullName, dateBefore);
    }
}
