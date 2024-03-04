using Force.Crc32;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon;

public abstract class SocketSession
{
    private Thread _thread { get; set; }
    private byte _encryptKey;
    private Socket _socket;
    private bool _encrypt;
    private string _ip;
    private int _port;
    private int _id;
    protected ulong _readed;
    protected ulong _writed;
    public bool IsConnected { get { return _socket.Connected; } }
    protected abstract void OnReceivePackage(Packet packet);
    protected abstract void OnSocketError(int id, Socket socket, Exception e);
    public SocketSession(int id, Socket socket, bool encrypt, byte encryptKey)
    {
        _id = id;
        _socket = socket;
        _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        _encrypt = encrypt;
        _encryptKey = encryptKey;

        _thread = new Thread((s) =>
        {
            while (true)
            {
                if (!_socket.Connected)
                    continue;
                var packet = ReadPacket();
                if (packet != null)
                    OnReceivePackage(packet);
            }
        });
        _thread.Name = "read";
        _thread.Start();
    }
    private byte[] Read(int length)
    {
        var buffer = new byte[length];
        try
        {
            var total = 0;
            while (total < length)
            {
                total += _socket.Receive(buffer, total, length - total, SocketFlags.None);
                _readed += (ulong)total;
                Log.Information($"readed:{_readed},writed:{_writed}");
            }

            if (_encrypt)
                return buffer.Apply(f => f ^= _encryptKey);
        }
        catch (SocketException e)
        {
            OnSocketError(_id, _socket, e);
        }
        return buffer;
    }
    private int Write(byte[] buffer)
    {
        try
        {
            Array.Resize(ref buffer, buffer.Length + sizeof(uint));
            Crc32Algorithm.ComputeAndWriteToEnd(buffer);

            if (_encrypt)
                buffer = buffer.Apply(f => f ^= _encryptKey);
            int n=_socket.Send(buffer);
            Debug.Assert(n == buffer.Length);
            _writed += (ulong)n;
            Log.Information($"readed:{_readed},writed:{_writed}");
            return n;
        }
        catch (SocketException e)
        {
            OnSocketError(_id, _socket, e);
            return 0;
        }
    }
    private Packet? ReadPacket()
    {
        var whole = new List<byte>();
        var buffer = Read(1);
        whole.AddRange(buffer);

        byte dataType = buffer[0];
        if (!Enum.IsDefined(typeof(PacketType), (int)dataType))
        {
            Log.Error("数据错误，无效数据包类型: " + BitConverter.ToString(buffer));
            return null;
        }

        buffer = Read(Packet.Int32Size);
        whole.AddRange(buffer);
        var dataLength = BitConverter.ToInt32(buffer);

        buffer = Read(Packet.Int32Size);
        whole.AddRange(buffer);
        var clientId = BitConverter.ToInt32(buffer);

        var data = Read(dataLength);
        whole.AddRange(data);

        buffer = Read(sizeof(uint));//checksum
        whole.AddRange(buffer);
        if (!Crc32Algorithm.IsValidWithCrcAtEnd(whole.ToArray()))
        {
            Log.Error("Crc检验出错");
            return null;
        }

        Packet result = null;
        switch ((PacketType)dataType)
        {
            case PacketType.Handshake:
                result=new PacketHandshake(whole.ToArray()); 
                break;
            case PacketType.FileInquire:
                result = new PacketFileListRequest(whole.ToArray());
                break;
            case PacketType.FileTotalInfo:
                result = new PacketFileListInfoResponse(whole.ToArray());
                break;
            case PacketType.FileDetailInfo:
                result = new PacketFileListDetailResponse(whole.ToArray());
                break;
            case PacketType.FileResponseInfo:
                result = new PacketFileContentInfoResponse(whole.ToArray());
                break;
            case PacketType.FileResponseDetail:
                result = new PacketFileContentDetailResponse(whole.ToArray());
                break;
            case PacketType.FileRequest:
                result = new PacketFileContentDetailRequest(whole.ToArray());
                break;
            default:
                break;
        }

        if (result != null)
        {
            //Log.Information($"收到{result}");
        }
        return result;
    }
    public void SendPacket(Packet packet)
    {   
        Write(packet.GetBytes());
    }
    public bool Connect(string ip, int port)
    {
        try
        {
            _ip = ip;
            _port = port;
            _socket.Connect(IPAddress.Parse(ip), port);
            OnConnected();
            return true;
        }
        catch (Exception e)
        {
            OnSocketError(_id, _socket, e);
            Log.Error(e.Message);
            Log.Error(e.StackTrace);
            return false;
        }
    }

    public bool Reconnect()
    {
        try
        {
            _socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _socket.Connect(_ip, _port);
            OnConnected();
            return true;
        }
        catch (Exception e)
        {
            OnSocketError(_id, _socket, e);
            Log.Error(e.Message);
            Log.Error(e.StackTrace);
            return false;
        }
    }

    protected virtual void OnConnected() { }
}
