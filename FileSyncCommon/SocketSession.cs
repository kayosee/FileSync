﻿using Force.Crc32;
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

public sealed class SocketSession
{
    private int _id;
    private bool _encrypt;
    private byte _encryptKey;
    private Thread _producer;
    private Thread _consumer;
    private Socket _socket;
    private ConcurrentDictionary<int, ConstructorInfo> _constructors;
    private volatile Boolean _disposed;
    private Semaphore _pushSemaphore;
    private Semaphore _pullSemaphore;
    private Signal _running;
    private ConcurrentQueue<Packet> _packetQueue;

    public bool IsRunning
    {
        get
        {
            return _running.State;
        }
    }
    public bool Encrypt { get => _encrypt; set => _encrypt = value; }
    public int Id { get => _id; set => _id = value; }
    public byte EncryptKey { get => _encryptKey; set => _encryptKey = value; }
    public Socket Socket { get => _socket; set => _socket = value; }
    public delegate void ReceivePackage(Packet packet);
    public delegate void SocketError(int id, Exception e);
    public ReceivePackage OnReceivePackage { get; set; }
    public SocketError OnSocketError { get; set; }
    public void Initialize()
    {
        _disposed = false;
        _constructors = new ConcurrentDictionary<int, ConstructorInfo>();
        _packetQueue = new ConcurrentQueue<Packet>();
        _pushSemaphore = new Semaphore(32, 32);
        _pullSemaphore = new Semaphore(0, 32);
        _running = new Signal(false);//手动控制运行
        _producer = new Thread((s) =>
        {
            while (_running.Wait())
            {
                if(_disposed) 
                    break;

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
            while (_running.Wait())
            {
                if (_disposed)
                    break;

                if (_pullSemaphore.WaitOne() && _packetQueue.TryDequeue(out var packet))
                {
                    _pushSemaphore.Release();
                    if (OnReceivePackage != null)
                        OnReceivePackage.Invoke(packet);
                }
            }
        });
        _consumer.Name = "consumer";
        _consumer.Start();

    }
    public SocketSession(int id, Socket socket, bool encrypt, byte encryptKey)
    {
        Initialize();
        _id = id;
        _socket = socket; 
        _encrypt = encrypt;
        _encryptKey = encryptKey;
    }
    public void StartMessageLoop()
    {
        _running.Promitted();
    }
    public void StopMessageLoop()
    {
        _running.Prohibited();
    }
    public void Disconnect()
    {
        try
        {
            _disposed = true;
            _socket.Close();
        }
        catch (Exception ex)
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
                OnSocketError(_id, e);
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
                OnSocketError(_id, e);
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
        catch (SocketException e)
        {
            if (!_disposed && OnSocketError != null)
                OnSocketError(_id, e);
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
            if (_disposed)
                return 0;

            if (OnSocketError != null)
                OnSocketError(_id, e);
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
