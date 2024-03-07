using Force.Crc32;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon;

public abstract class SocketSession
{
    private Thread _reader { get; set; }
    private Thread _writer { get; set; }
    private byte _encryptKey;
    private Socket _socket;
    private bool _encrypt;
    private string _ip;
    private int _port;
    private int _id;
    private Dictionary<int, ConstructorInfo> _constructors;
    private Semaphore _semaphore;
    private ConcurrentQueue<Packet> _packetQueue;
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
        _constructors = new Dictionary<int, ConstructorInfo>();
        _packetQueue= new ConcurrentQueue<Packet>();
        _semaphore = new Semaphore(32,32);
        _reader = new Thread((s) =>
        {
            while (true)
            {
                if (!_socket.Connected)
                    continue;

                var packet = ReadPacket();
                if (packet != null && _semaphore.WaitOne())
                {
                    _packetQueue.Enqueue(packet);
                }
            }
        });
        _reader.Name = "reader";
        _reader.Start();

        _writer = new Thread((s) =>
        {
            while (true)
            {
                if (!_socket.Connected)
                    continue;
                if (_packetQueue.TryDequeue(out var packet))
                {
                    OnReceivePackage(packet);
                    _semaphore.Release();
                }
            }
        });
        _writer.Name = "writer";
        _writer.Start();
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

            int sent = _socket.Send(buffer);
            return sent;
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

        var result = ConvertPacket(dataType, whole.ToArray());
        if (result != null)
        {
            return (Packet)result;
        }
        return null;
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
            Log.Error(e.Message);
            Log.Error(e.StackTrace);
            return false;
        }
    }

    protected virtual void OnConnected() { }

    private object? ConvertPacket(int dataType, byte[] data)
    {
        ConstructorInfo constructor = null;
        if (_constructors.ContainsKey(dataType))
        {
            constructor = _constructors[dataType];
        }
        else
        {
            var name = "Packet" + Enum.GetName(typeof(PacketType), (int)dataType);
            var type = typeof(Packet).Assembly.GetTypes().First(f => f.Name == name);
            constructor = type.GetConstructors().First(f => f.GetParameters().Length == 1 && f.GetParameters().Any(s => s.Name == "bytes"));
            if (constructor != null)
            {
                _constructors.Add(dataType, constructor);
            }
        }

        if (constructor != null)
            return constructor.Invoke(new object[] { data });

        return null;
    }
}
