using FileSyncCommon.Messages;
using FileSyncCommon.Tools;
using Force.Crc32;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using FileSyncCommon.Tools;
using System;
namespace FileSyncCommon
{
    public sealed class SocketSession
    {
        private int QueueSize = Environment.ProcessorCount;
        private volatile bool _disposed;
        private bool _encrypt;
        private byte _encryptKey;
        private Thread _producer;
        private Thread _consumer;
        private Socket _socket;
        private FixedLengthQueue<Messages.Message> _packetQueue;
        private static ConcurrentDictionary<int, ConstructorInfo> _constructors = new ConcurrentDictionary<int, ConstructorInfo>();
        public bool Encrypt { get => _encrypt; set => _encrypt = value; }
        public byte EncryptKey { get => _encryptKey; set => _encryptKey = value; }
        public Socket Socket { get => _socket; set => _socket = value; }
        public delegate void ReceivePackageHandler(Message packet);
        public delegate void SendPackageHandler(Message packet);
        public delegate void SocketErrorHandler(SocketSession socketSession, Exception e);
        public delegate void DataErrorHandler(SocketSession socketSession, string message);
        public event ReceivePackageHandler? OnReceivePackage;
        public event SendPackageHandler? OnSendPackage;
        public event SocketErrorHandler? OnSocketError;
        public event DataErrorHandler? OnDataError;
        public SocketSession(Socket socket, bool encrypt, byte encryptKey)
        {
            _socket = socket;
            _socket.ReceiveBufferSize = (int)Math.Pow(1024, 3);//接收缓存区太小，会产生ZEROWINDOW，导致后面的Send阻塞
            _encrypt = encrypt;
            _encryptKey = encryptKey;

            _packetQueue = new FixedLengthQueue<Message>(Environment.ProcessorCount);
            _producer = new Thread((s) =>
                {
                    while (!_disposed)
                    {
                        var packet = ReadMessage();
                        if (packet != null)
                        {
                            _packetQueue.Enqueue(packet);
                        }
                    }
                });
            _producer.Name = "producer-" + socket.Handle;
            _producer.Start();

            _consumer = new Thread((s) =>
            {
                while (!_disposed)
                {
                    if (_packetQueue.Dequeue(out var packet))
                    {
                        OnReceivePackage?.Invoke(packet);
                    }
                }
            });
            _consumer.Name = "consumer-" + socket.Handle;
            _consumer.Start();
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
                _packetQueue.Dispose();
                _socket.Close();
            }
            catch (Exception)
            {
            }
        }
        public void SendMessage(Message message)
        {
            try
            {
                foreach (var packet in message.ToPackets())
                {
                    Write(packet.Serialize());
                };

                if (OnSendPackage != null)
                    OnSendPackage(message);
            }
            catch (Exception e)
            {
                DoSocketError(this, e);
            }
        }
        private byte[] Read(int length, int maxWaitSeconds = -1)
        {
            if (maxWaitSeconds > 0)
                _socket.ReceiveTimeout = (int)TimeSpan.FromSeconds(maxWaitSeconds).TotalMilliseconds;
            else
                _socket.ReceiveTimeout = -1;

            byte[] buffer = new byte[length];
            _socket.Receive(buffer, length, SocketFlags.None);
            if (_encrypt)
                buffer.ForEach<byte>(f => f ^= _encryptKey);

            return buffer;
        }
        private Packet? ReadPacket(int maxWaitSeconds = -1)
        {
            try
            {
                Packet packet = new Packet();

                var buffer = Read(Packet.Flag.Length, maxWaitSeconds);
                if (Encoding.UTF8.GetString(buffer) != Encoding.UTF8.GetString(Packet.Flag))
                    throw new InvalidDataException("包头标志不正确");

                buffer = Read(sizeof(long), maxWaitSeconds);
                packet.TotalLength = BitConverter.ToInt64(buffer);

                buffer = Read(sizeof(int), maxWaitSeconds);
                packet.Sequence = BitConverter.ToInt32(buffer);
                if (packet.Sequence < 0)
                    throw new InvalidDataException(nameof(packet.Sequence) + "序号无效");

                buffer = Read(sizeof(short), maxWaitSeconds);
                packet.SliceLength = BitConverter.ToInt16(buffer);
                if (packet.SliceLength <= 0)
                    throw new InvalidDataException(nameof(packet.SliceLength) + "长度无效");

                packet.SliceData = Read(packet.SliceLength, maxWaitSeconds);                
                return packet;

            }
            catch (Exception e)
            {
                OnSocketError?.Invoke(this, e);
                return null;
            }
        }
        private int Write(byte[] buffer)
        {
            try
            {
                /*
                Array.Resize(ref buffer, buffer.Length + sizeof(uint));
                Crc32Algorithm.ComputeAndWriteToEnd(buffer);
                */
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
        private Message? ReadMessage()
        {
            long totalLength = 0;
            var packets = new List<Packet>();
            do
            {
                var packet = ReadPacket(-1);
                if (packet == null)
                    return null;

                totalLength += packet.SliceLength;
                packets.Add(packet);
                if (totalLength >= packet.TotalLength)
                    break;
            } while (true);

            return Message.FromPackets(packets.ToArray());
        }
        ~SocketSession()
        {
            Disconnect();
        }
    }
}
