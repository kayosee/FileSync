using Force.Crc32;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Reflection;

namespace FileSyncCommon
{
    public sealed class SocketSession
    {
        private int QueueSize = Environment.ProcessorCount;
        private volatile bool _disposed;
        private bool _encrypt;
        private byte _encryptKey;
        private Thread _reader;
        private Thread _consumer;
        private Thread _writer;
        private Socket _socket;
        private FixedLengthQueue<Packet> _inQueue;
        private FixedLengthQueue<Packet> _outQueue;
        private static ConcurrentDictionary<int, ConstructorInfo> _constructors = new ConcurrentDictionary<int, ConstructorInfo>();
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
            _socket.ReceiveBufferSize = 1024 * 1024 * 1024;
            _encrypt = encrypt;
            _encryptKey = encryptKey;

            _inQueue = new FixedLengthQueue<Packet>(Environment.ProcessorCount * 32);
            _outQueue = new FixedLengthQueue<Packet>(Environment.ProcessorCount);
            _reader = new Thread((s) =>
            {
                while (!_disposed)
                {
                    var packet = ReadPacket();
                    if (packet != null)
                    {
                        _inQueue.Enqueue(packet);//1.顺序不能乱
                    }
                }
            });
            _reader.Name = "producer-" + socket.Handle;
            _reader.Start();

            _consumer = new Thread((s) =>
            {
                while (!_disposed)
                {
                    if (_inQueue.Dequeue(out var packet))
                    {
                        OnReceivePackage?.Invoke(packet);
                    }
                }
            });
            _consumer.Name = "consumer-" + socket.Handle;
            _consumer.Start();

            _writer = new Thread((s) =>
            {
                while (!_disposed)
                {
                    if (_outQueue.Dequeue(out var packet))
                    {
                        try
                        {
                            Write(packet.GetBytes());
                        }
                        catch (Exception e)
                        {
                            DoSocketError(this, e);
                        }
                    }
                }
            });
            _writer.Name = "writer-" + socket.Handle;
            _writer.Start();
        }
        private void DoSocketError(SocketSession socketSession, Exception e)
        {
            if (_disposed)
                return;

            Disconnect();
            OnSocketError?.Invoke(this, e);
        }
        public void Disconnect()
        {
            try
            {
                _disposed = true;
                _inQueue.Dispose();
                _outQueue.Dispose();
                _socket.Close();
            }
            catch (Exception e)
            {
            }
        }
        public void SendPacket(Packet packet)
        {
            _outQueue.Enqueue(packet);
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
                DoSocketError(this, e);
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
                _socket.Send(buffer);
                return buffer.Length;
            }
            catch (Exception e)
            {
                DoSocketError(this, e);
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
                OnDataError?.Invoke(this, "数据错误，无效数据包类型: " + BitConverter.ToString(buffer));
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
                OnDataError?.Invoke(this, "Crc检验出错");
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
}
