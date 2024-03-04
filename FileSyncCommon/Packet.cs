using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon;

public abstract class Packet
{
    public const int Int32Size = sizeof(int);
    public const int HeaderSize = sizeof(byte) + sizeof(int) * 2;
    private byte _dataType;
    private int _dataLength;
    private int _clientId;

    public PacketType DataType { get => (PacketType)_dataType; set => _dataType = (byte)value; }
    public int DataLength { get => _dataLength; set => _dataLength = value; }
    public int ClientId { get => _clientId; set => _clientId = value; }
    public int PacketSize
    {
        get
        {
            return HeaderSize + DataLength;
        }
    }
    public Packet(PacketType dataType, int clientId) { _dataType = (byte)dataType; _clientId = clientId; }
    public Packet(byte[] bytes)
    {
        using (var stream = new ByteArrayStream(bytes))
        {
            _dataType = stream.ReadByte();
            _dataLength = stream.ReadInt32();
            _clientId = stream.ReadInt32();
        }
        if (_dataLength > 0)
        {
            Memory<byte> span = bytes;
            var buffer = span.Slice(HeaderSize, _dataLength).ToArray();
            Deserialize(buffer);
        }
    }
    protected abstract void Deserialize(byte[] bytes);
    protected abstract byte[] Serialize();
    public byte[] GetBytes()
    {
        var body = Serialize();
        _dataLength = body.Length;
        using (var stream = new ByteArrayStream(HeaderSize + _dataLength))
        {
            stream.Write(_dataType);
            stream.Write(_dataLength);
            stream.Write(_clientId);
            if (_dataLength > 0)
                stream.Write(body, 0, _dataLength);
            var buffer = stream.GetBuffer();
            return buffer;
        }
    }
    public override string ToString()
    {
        return GetType().Name;
    }
}
