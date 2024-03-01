using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon;

public class Packet
{
    public const int Int32Size = sizeof(int);
    public const int HeaderSize = sizeof(byte) + sizeof(int) * 2;
    public byte[] RawData { get; set; }
    public byte DataType { get; set; }
    public int DataLength { get; set; }
    public int ClientId { get; set; }
    public int PacketSize
    {
        get
        {
            return HeaderSize + DataLength;
        }
    }
    public Packet(byte dataType, int clientId) { DataType = dataType; ClientId = clientId; }
    public Packet(Packet packet)
    {
        ClientId = packet.ClientId;
        DataType = packet.DataType;
        DataLength = packet.DataLength;
        RawData = packet.RawData;
        Deserialize(packet.RawData);
    }
    public Packet(byte[] bytes)
    {
        using (var stream = new ByteArrayStream(bytes))
        {
            DataType = stream.ReadByte();
            DataLength = stream.ReadInt32();
            ClientId = stream.ReadInt32();
        }
        if(DataLength>0)
        {
            Memory<byte> span = bytes;
            var buffer = span.Slice(HeaderSize, DataLength).ToArray();
            Deserialize(buffer);
        }
    }
    protected virtual void Deserialize(byte[] bytes) { RawData = bytes; }
    protected virtual byte[] Serialize() { return RawData; }
    public virtual IEnumerable<Packet>? Process(string folder) { throw new NotImplementedException(); }
    public byte[] GetBytes()
    {
        var body = Serialize();
        DataLength = body.Length;
        using (var stream = new ByteArrayStream(HeaderSize + DataLength))
        {
            stream.Write(DataType);
            stream.Write(DataLength);
            stream.Write(ClientId);
            if(DataLength > 0)
                stream.Write(body, 0, DataLength);
            var buffer = stream.GetBuffer();
            return buffer;
        }
    }
    public override string ToString()
    {
        return GetType().Name;
    }
}
