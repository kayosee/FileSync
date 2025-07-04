﻿using System.Text;

namespace FileSyncCommon.Tools;

public class ByteArrayStream : IDisposable
{
    private byte[] _data;
    private long _readPos;
    private long _writePos;

    public ByteArrayStream()
    {
        _data = new byte[0];
        _readPos = 0;
        _writePos = 0;
    }
    public ByteArrayStream(long length)
    {
        _data = new byte[length];
        _readPos = 0;
        _writePos = 0;
    }
    public ByteArrayStream(byte[] data)
    {
        _data = new byte[data.Length];
        Array.Copy(data, 0, _data, 0, data.Length);
        _readPos = 0;
        _writePos = 0;
    }
    public long Length => _data.LongLength;
    public long WritePosition { get => _writePos; set => _writePos = value; }
    public long ReadPosition { get => _readPos; set => _readPos = value; }
    public int Peek(byte[] buffer, int offset, int count)
    {
        if (_data.Length > 0)
        {
            var min = Math.Min(count, _data.Length);
            Span<byte> span = buffer;
            Span<byte> data = _data;
            var slice = span.Slice(offset, min);
            data.Slice((int)_readPos, count).CopyTo(slice);
            return min;
        }

        throw new EndOfStreamException();
    }
    public int Read(byte[] buffer, int offset, int count)
    {
        if (_data.Length > 0)
        {
            var min = Math.Min(count, _data.Length);
            Span<byte> span = buffer;
            Span<byte> data = _data;
            var slice = span.Slice(offset, min);
            data.Slice((int)_readPos, count).CopyTo(slice);
            _readPos += min;
            return min;
        }

        throw new EndOfStreamException();
    }
    public ushort ReadUShort()
    {
        var size = sizeof(ushort);
        if (_data.LongLength == 0 || _readPos + size > _data.LongLength)
            throw new EndOfStreamException();

        Span<byte> span = _data;
        var result = BitConverter.ToUInt16(span.Slice((int)_readPos, size));
        _readPos += size;
        return result;
    }

    public uint ReadUInt()
    {
        var size = sizeof(uint);
        if (_data.LongLength == 0 || _readPos + size > _data.LongLength)
            throw new EndOfStreamException();

        Span<byte> span = _data;
        var result = BitConverter.ToUInt32(span.Slice((int)_readPos, size));
        _readPos += size;
        return result;
    }
    public ulong ReadULong()
    {
        var size = sizeof(ulong);
        if (_data.LongLength == 0 || _readPos + size > _data.LongLength)
            throw new EndOfStreamException();

        Span<byte> span = _data;
        var result = BitConverter.ToUInt64(span.Slice((int)_readPos, size));
        _readPos += size;
        return result;
    }
    public short ReadShort()
    {
        var size = sizeof(short);

        if (_data.LongLength == 0 || _readPos + size > _data.LongLength)
            throw new EndOfStreamException();

        Span<byte> span = _data;
        var result = BitConverter.ToInt16(span.Slice((int)_readPos, size));
        _readPos += size;
        return result;
    }
    public int ReadInt()
    {
        var size = sizeof(int);
        if (_data.LongLength == 0 || _readPos + size > _data.LongLength)
            throw new EndOfStreamException();

        Span<byte> span = _data;
        var result = BitConverter.ToInt32(span.Slice((int)_readPos, size));
        _readPos += size;
        return result;
    }
    public long ReadLong()
    {
        var size = sizeof(long);
        if (_data.LongLength == 0 || _readPos + size > _data.LongLength)
            throw new EndOfStreamException();

        Span<byte> span = _data;
        var result = BitConverter.ToInt64(span.Slice((int)_readPos, size));
        _readPos += size;
        return result;
    }

    public long Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin:
                _readPos = offset;
                _writePos = offset;
                break;
            case SeekOrigin.Current:
                _readPos = _readPos + offset;
                _writePos = _writePos + offset;
                break;
            case SeekOrigin.End:
                _readPos = _readPos + _data.Length;
                _writePos = _writePos + _data.Length;
                break;
        }

        return _readPos;
    }
    public void SetLength(long value)
    {
        Array.Resize(ref _data, (int)value);
    }
    public void Write(byte[] buffer, int offset, int count)
    {
        if (_writePos + count > _data.Length)
            SetLength(_writePos + count);

        Span<byte> span = buffer;
        Span<byte> data = _data;
        span.Slice(offset, count).CopyTo(data.Slice((int)_writePos));
        _writePos += count;
    }
    public void Write(byte value)
    {
        if (_writePos + 1 >= _data.Length)
            SetLength(_writePos + 1);

        Write(new byte[] { value }, 0, 1);
    }
    public void Write(short value)
    {
        if (_writePos + sizeof(short) >= _data.Length)
            SetLength(_writePos + sizeof(short));

        var buffer = BitConverter.GetBytes(value);
        Write(buffer, 0, buffer.Length);
    }
    public void Write(ushort value)
    {
        if (_writePos + sizeof(ushort) >= _data.Length)
            SetLength(_writePos + sizeof(ushort));

        var buffer = BitConverter.GetBytes(value);
        Write(buffer, 0, buffer.Length);
    }
    public void Write(int value)
    {
        if (_writePos + sizeof(int) >= _data.Length)
            SetLength(_writePos + sizeof(int));

        var buffer = BitConverter.GetBytes(value);
        Write(buffer, 0, buffer.Length);
    }
    public void Write(long value)
    {
        if (_writePos + sizeof(long) >= _data.Length)
            SetLength(_writePos + sizeof(long));

        var buffer = BitConverter.GetBytes(value);
        Write(buffer, 0, buffer.Length);
    }
    public void Write(uint value)
    {
        if (_writePos + sizeof(uint) >= _data.Length)
            SetLength(_writePos + sizeof(uint));

        var buffer = BitConverter.GetBytes(value);
        Write(buffer, 0, buffer.Length);
    }
    public void Write(ulong value)
    {
        if (_writePos + sizeof(ulong) >= _data.Length)
            SetLength(_writePos + sizeof(ulong));

        var buffer = BitConverter.GetBytes(value);
        Write(buffer, 0, buffer.Length);
    }

    public byte[] GetBuffer()
    {
        return _data;
    }
    public void Dispose()
    {
    }
    public byte ReadByte()
    {
        if (_readPos >= _data.LongLength)
            throw new EndOfStreamException();

        var result = _data[_readPos];
        _readPos += 1;
        return result;
    }
    public string ReadUTF8String()
    {
        int length = ReadInt();
        var buffer = new byte[length];
        Read(buffer, 0, length);
        return Encoding.UTF8.GetString(buffer, 0, length);
    }
    public void WriteUTF8string(string text)
    {
        var buffer = Encoding.UTF8.GetBytes(text);
        Write(buffer.Length);
        Write(buffer, 0, buffer.Length);
    }
}
