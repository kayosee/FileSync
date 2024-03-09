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
    private Thread _producer;
    private Thread _consumer;
    private bool _connected;
    private byte _encryptKey;
    private Socket _socket;
    private bool _encrypt;
    private string _ip;
    private int _port;
    private int _id;
    private ConcurrentDictionary<int, ConstructorInfo> _constructors;
    private Semaphore _pushSemaphore;
    private Semaphore _pullSemaphore;
    private ManualResetEvent _conn;
    private ManualResetEvent _runn;
    private ConcurrentQueue<Packet> _packetQueue;
    public bool IsConnected { get { return _connected; } }
    protected abstract void OnReceivePackage(Packet packet);
    protected abstract void OnSocketError(int id, Socket socket, Exception e);
    public SocketSession(int id, Socket socket, bool encrypt, byte encryptKey)
    {
        _id = id;
        _socket = socket;
        _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

        _encrypt = encrypt;
        _encryptKey = encryptKey;
        _constructors = new ConcurrentDictionary<int, ConstructorInfo>();
        _packetQueue = new ConcurrentQueue<Packet>();
        _pushSemaphore = new Semaphore(32, 32);
        _pullSemaphore = new Semaphore(0, 32);
        _connected = socket.Connected;
        _conn = new ManualResetEvent(true);//手动控制连接
        _runn = new ManualResetEvent(false);//手动控制运行
        _producer = new Thread((s) =>
        {
            while (_conn.WaitOne() && _runn.WaitOne())
            {
                var packet = ReadPacket();
                if (packet != null && _pushSemaphore.WaitOne())
                {
                    _packetQueue.Enqueue(packet);
                    _pullSemaphore.Release();
                }
            }
        });
        _producer.Name = "producer";
        _producer.Start();

        _consumer = new Thread((s) =>
        {
            while (_conn.WaitOne() && _runn.WaitOne())
            {
                if (_pullSemaphore.WaitOne() && _packetQueue.TryDequeue(out var packet))
                {
                    OnReceivePackage(packet);
                    _pushSemaphore.Release();
                }
            }
        });
        _consumer.Name = "consumer";
        _consumer.Start();
    }
    public void StartMessageLoop()
    {
        _runn.Set();
    }
    public void StopMessageLoop()
    {
        _runn.Reset();
    }
    private bool Read(int length, out byte[] buffer)
    {
        buffer = new byte[length];
        try
        {
            var total = 0;
            while (total < length)
            {
                total += _socket.Receive(buffer, total, length - total, SocketFlags.None);
            }

            if (_encrypt)
            {
                buffer.ForEach<byte>(f => f ^= _encryptKey);
            }
            return true;
        }
        catch (SocketException e)
        {
            if (e.SocketErrorCode != SocketError.TimedOut)
            {
                _connected = false;
                _conn.Reset();
                OnSocketError(_id, _socket, e);
            }
            return false;
        }
    }

    private bool ReadAppend(int length, out byte[] buffer,ref List<byte> appender)
    {
        var ok = (Read(length, out buffer));
        if(ok)
            appender.AddRange(buffer);
        return ok;
    }
    private int Write(byte[] buffer)
    {
        try
        {
            Array.Resize(ref buffer, buffer.Length + sizeof(uint));
            Crc32Algorithm.ComputeAndWriteToEnd(buffer);

            if (_encrypt)
                buffer.ForEach<byte>(f => f ^= _encryptKey);
            int sent = _socket.Send(buffer);
            return sent;
        }
        catch (SocketException e)
        {
            _connected = false;
            _conn.Reset();
            OnSocketError(_id, _socket, e);
            return 0;
        }
    }
    private Packet? ReadPacket()
    {
        var whole = new List<byte>();

        if (!ReadAppend(1, out var buffer,ref whole))
            return null;

        byte dataType = buffer[0];
        if (!Enum.IsDefined(typeof(PacketType), (int)dataType))
        {
            Log.Error("数据错误，无效数据包类型: " + BitConverter.ToString(buffer));
            return null;
        }

        if (!ReadAppend(Packet.Int32Size, out buffer, ref whole))
            return null;

        var dataLength = BitConverter.ToInt32(buffer);

        if (!ReadAppend(Packet.Int32Size, out buffer, ref whole))
            return null;

        var clientId = BitConverter.ToInt32(buffer);

        if (!ReadAppend(dataLength, out buffer, ref whole))
            return null;

        if (!ReadAppend(sizeof(uint), out buffer, ref whole))//checksum
            return null;

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
    public Packet? ReceivePacket(TimeSpan timeout)
    {
        var temp = _socket.ReceiveTimeout;
        _socket.ReceiveTimeout = (int)timeout.TotalMilliseconds;
        var packet = ReadPacket();
        _socket.ReceiveTimeout = temp;
        return packet;
    }
    public bool Connect(string ip, int port)
    {
        try
        {
            _ip = ip;
            _port = port;
            _socket.Connect(IPAddress.Parse(ip), port);
            _connected = true;
            _conn.Set();
            OnConnected(_socket);
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
        _socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        return Connect(_ip, _port);
    }

    protected virtual void OnConnected(Socket socket) { }

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
                _constructors.AddOrUpdate(dataType, constructor, (key, value) => value);
            }
        }

        if (constructor != null)
            return constructor.Invoke(new object[] { data });

        return null;
    }
    public void Disconnect()
    {
        if (_connected)
        {
            _connected = false;
            _conn.Reset();
            _socket.Close();
        }
    }
}
