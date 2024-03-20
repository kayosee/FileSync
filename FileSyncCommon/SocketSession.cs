using Force.Crc32;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Reflection;

namespace FileSyncCommon;

public sealed class SocketSession
{
    private bool _encrypt;
    private byte _encryptKey;
    private Thread _producer;
    private Thread _consumer;
    private Socket _socket;
    private SemaphoreEx _pushSemaphore;
    private SemaphoreEx _pullSemaphore;
    private volatile Boolean _disposed;
    private ConcurrentQueue<Packet> _packetQueue;
    private ConcurrentDictionary<int, ConstructorInfo> _constructors;
    public bool IsRunning
    {
        get
        {
            return !_disposed;
        }
    }
    public bool Encrypt { get => _encrypt; set => _encrypt = value; }
    public byte EncryptKey { get => _encryptKey; set => _encryptKey = value; }
    public Socket Socket { get => _socket; set => _socket = value; }
    public delegate void ReceivePackageHandler(Packet packet);
    public delegate void SocketErrorHandler(SocketSession socketSession, Exception e);
    public delegate void DataErrorHandler(SocketSession socketSession, string message);
    public event ReceivePackageHandler? OnReceivePackage;
    public event SocketErrorHandler? OnSocketError;
    public event DataErrorHandler? OnDataError;
    public SocketSession(Socket socket, bool encrypt, byte encryptKey)
    {
        _socket = socket;
        _encrypt = encrypt;
        _encryptKey = encryptKey;

        _disposed = false;
        _constructors = new ConcurrentDictionary<int, ConstructorInfo>();
        _packetQueue = new ConcurrentQueue<Packet>();
        _pushSemaphore = new SemaphoreEx(64, 64);
        _pullSemaphore = new SemaphoreEx(0, 64);
        _producer = new Thread((s) =>
        {
            while (_socket.Connected)
            {
                var packet = ReadPacket();                
                if (packet != null && _pushSemaphore.Wait())
                {                    
                    _packetQueue.Enqueue(packet);//1.顺序不能乱
                    _pullSemaphore.Release();//2.一定要先入队列再唤醒处理线程！
                }
            }
        });
        _producer.Name = "producer-" + socket.Handle;
        _producer.Start();

        _consumer = new Thread((s) =>
        {
            while (_socket.Connected)
            {
                if (_pullSemaphore.Wait() && _packetQueue.TryDequeue(out var packet))
                {
                    _pushSemaphore.Release();
                    if (OnReceivePackage != null)
                        OnReceivePackage(packet);
                }
            }
        });
        _consumer.Name = "consumer-" + socket.Handle;
        _consumer.Start();

    }
    public void Disconnect()
    {
        try
        {            
            //_pullSemaphore.ReleaseAll();
            //_pushSemaphore.ReleaseAll();
            _disposed = true;
            _socket.Close();
        }
        catch (Exception e)
        {
        }
    }
    public void SendPacket(Packet packet, TimeSpan? timeout = null)
    {
        try
        {
            var temp = _socket.SendTimeout;
            if (timeout != null)
                _socket.SendTimeout = (int)timeout.Value.TotalMilliseconds;

            Write(packet.GetBytes());
            _socket.SendTimeout = temp;

        }
        catch (Exception e)
        {
            if (_disposed)
                return;

            if (OnSocketError != null)
                OnSocketError(this, e);
        }
    }
    public Packet? ReceivePacket(TimeSpan? timeout = null)
    {
        try
        {
            var temp = _socket.ReceiveTimeout;
            if (timeout != null)
                _socket.ReceiveTimeout = (int)timeout.Value.TotalMilliseconds;
            var packet = ReadPacket();
            _socket.ReceiveTimeout = temp;
            return packet;
        }
        catch (Exception e)
        {
            if (!_disposed && OnSocketError != null)
                OnSocketError(this, e);
            return null;
        }
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
        catch (Exception e)
        {
            if (!_disposed && OnSocketError != null)
                OnSocketError(this, e);
            return false;
        }
    }
    private bool ReadAppend(int length, out byte[] buffer, ref List<byte> appender)
    {
        var ok = (Read(length, out buffer));
        if (ok)
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
        catch (Exception e)
        {
            if (_disposed)
                return 0;

            if (OnSocketError != null)
                OnSocketError(this, e);
            return 0;
        }
    }
    private Packet? ReadPacket()
    {
        var whole = new List<byte>();

        if (!ReadAppend(1, out var buffer, ref whole))
            return null;

        byte dataType = buffer[0];
        if (!Enum.IsDefined(typeof(PacketType), (int)dataType))
        {
            if (OnDataError != null)
                OnDataError.Invoke(this, "数据错误，无效数据包类型: " + BitConverter.ToString(buffer));
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
            if (OnDataError != null)
                OnDataError.Invoke(this, "Crc检验出错");
            return null;
        }

        var result = ConvertPacket(dataType, whole.ToArray());
        if (result != null)
        {
            return (Packet)result;
        }
        return null;
    }
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
    ~SocketSession()
    {
        Disconnect();
    }
}
