﻿using Force.Crc32;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon;

public class FileResponse : Packet
{
    private byte _responseType;
    private int _pathLength;
    private string _path;
    private long _pos;
    private long _lastWriteTime;
    private int _fileDataLength;
    private long _fileDataTotal;
    private uint _fileDataChecksum;
    private byte[] _fileData;
    public const int MaxDataSize = 1500;
    public override bool Equals(object? obj)
    {
        if (obj == null) return false;
        if (!(obj is FileResponse)) return false;
        FileResponse other = obj as FileResponse;

        return other.Path == Path && other.Pos == Pos;
    }
    public FileResponse(int clientId, byte responseType, string path) : base((int)PacketType.FileResponse, clientId)
    {
        _responseType = responseType;
        _path = path;
    }
    public FileResponse(byte[] bytes) : base(bytes)
    {
    }

    public FileResponse(Packet packet) : base(packet)
    {
    }
    public bool EndOfFile
    {
        get
        {
            return _pos + _fileDataLength >= _fileDataTotal;
        }
    }
    /// <summary>
    /// 路径
    /// </summary>
    public string Path
    {
        get => _path; set
        {
            _path = value;
        }
    }
    /// <summary>
    /// 数据位置
    /// </summary>
    public long Pos { get { return _pos; } set { _pos = value; } }
    /// <summary>
    /// 文件总长度
    /// </summary>
    public long FileDataTotal { get { return _fileDataTotal; } set { _fileDataTotal = value; } }
    /// <summary>
    /// 本节数据长度
    /// </summary>
    public int FileDataLength { get { return _fileDataLength; } set { _fileDataLength = value; } }
    /// <summary>
    /// 本节数据内容
    /// </summary>
    public byte[] FileData { get { return _fileData; } set { _fileData = value; } }
    /// <summary>
    /// 本节数据校验
    /// </summary>
    public uint FileDataChecksum { get { return _fileDataChecksum; } }
    /// <summary>
    /// 响应类型
    /// </summary>
    public byte ResponseType { get => _responseType; set => _responseType = value; }
    public long LastWriteTime { get => _lastWriteTime; set => _lastWriteTime = value; }

    protected override byte[] Serialize()
    {
        using (var stream = new ByteArrayStream())
        {
            stream.Write(_responseType);

            byte[] buffer = Encoding.UTF8.GetBytes(Path);
            _pathLength = buffer.Length;
            stream.Write(_pathLength);

            stream.Write(buffer, 0, buffer.Length);
            stream.Write(_pos);
            stream.Write(_lastWriteTime);
            stream.Write(_fileDataLength);
            stream.Write(_fileDataTotal);

            if (_fileDataLength > 0)
            {
                _fileDataChecksum = Crc32Algorithm.Compute(_fileData);
                stream.Write(_fileDataChecksum);
                stream.Write(_fileData, 0, _fileData.Length);
            }

            return stream.GetBuffer();
        }
    }
    protected override void Deserialize(byte[] bytes)
    {
        if (bytes == null)
            throw new ArgumentException("bytes");

        using (var stream = new ByteArrayStream(bytes))
        {
            _responseType = stream.ReadByte();
            _pathLength = stream.ReadInt32();

            byte[] buffer = new byte[_pathLength];
            stream.Read(buffer, 0, _pathLength);
            _path = Encoding.UTF8.GetString(buffer).TrimEnd('\0');

            _pos = stream.ReadInt64();
            _lastWriteTime = stream.ReadInt64();
            _fileDataLength = stream.ReadInt32();
            _fileDataTotal = stream.ReadInt64();

            if (_fileDataLength > 0)
            {
                _fileDataChecksum = stream.ReadUInt32();
                _fileData = new byte[_fileDataLength];
                stream.Read(_fileData, 0, _fileDataLength);
            }
        }
    }
    public override IEnumerable<Packet>? Process(string folder)
    {
        var path = System.IO.Path.Combine(folder, Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
        switch ((FileResponseType)ResponseType)
        {
            case FileResponseType.Empty:
                {
                    File.Create(path).Close();
                    break;
                }
            case FileResponseType.FileDeleted:
                {
                    if (System.IO.Path.Exists(path))
                    {
                        File.Delete(path);
                    }
                    break;
                }
            case FileResponseType.Content:
                {
                    Log.Information($"收到文件'{path}',位置:{Pos},长度:{FileData.Length},总长:{FileDataTotal}");
                    try
                    {
                        FileOperator.WriteFile(path + ".sync", Pos, FileData);

                        if (Pos + FileDataLength >= FileDataTotal) //文件已经传输完成
                        {
                            FileOperator.SetupFile(path, LastWriteTime);
                        }
                        else //写入位置信息
                        {
                            FileOperator.AppendFile(path + ".sync", new FilePosition(Pos).GetBytes());
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e.Message);
                        Log.Error(e.StackTrace);
                    }
                    break;
                }
            case FileResponseType.FileReadError:
                {
                    Log.Error($"远程文件读取失败:{Path}");
                    break;
                }
            default:
                break;
        }
        return null;
    }
}
