using FileSyncCommon.Messages;
using FileSyncCommon.Tools;
using Force.Crc32;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using FileSyncCommon.Tools;
using System;
using System.Diagnostics;
using Serilog;
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
            foreach (var packet in message.ToPackets())
            {
                Write(packet.Serialize());
            };

            if (OnSendPackage != null)
                OnSendPackage(message);
        }
        private bool Read(int length, out byte[] buffer, ref List<byte> whole, int maxWaitSeconds = -1)
        {
            buffer = new byte[length];
            try
            {
                if (maxWaitSeconds > 0)
                    _socket.ReceiveTimeout = (int)TimeSpan.FromSeconds(maxWaitSeconds).TotalMilliseconds;
                else
                    _socket.ReceiveTimeout = -1;

                _socket.Receive(buffer, length, SocketFlags.None);
                whole.AddRange(buffer);
                if (_encrypt)
                    buffer.Xor(_encryptKey);

                return true;
            }
            catch (Exception e)
            {
                DoSocketError(this, e);
                return false;
            }
        }
        private Packet? ReadPacket(int maxWaitSeconds = -1)
        {
            try
            {
                Packet packet = new Packet();
                var whole = new List<byte>();

                if (!Read(sizeof(ulong), out var buffer, ref whole, maxWaitSeconds))
                    return null;

                packet.TotalLength = BitConverter.ToUInt64(buffer);

                if (!Read(sizeof(uint), out buffer, ref whole, maxWaitSeconds))
                    return null;

                packet.Sequence = BitConverter.ToUInt32(buffer);

                if (!Read(sizeof(ushort), out buffer, ref whole, maxWaitSeconds))
                    return null;

                packet.SliceLength = BitConverter.ToUInt16(buffer);
                if (packet.SliceLength <= 0)
                {
                    Log.Error($"长度无效:{packet.SliceLength}");
                    return null;
                }

                if (!Read(packet.SliceLength, out buffer, ref whole, maxWaitSeconds))
                    return null;

                packet.SliceData = buffer;

                if (!Read(sizeof(uint), out buffer, ref whole, maxWaitSeconds))
                    return null;

                if (!Crc32Algorithm.IsValidWithCrcAtEnd(whole.ToArray()))
                {
                    Log.Error("CRC检验错误！");
                    return null;
                }

                return packet;

            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
                return null;
            }
        }
        private int Write(byte[] buffer)
        {
            try
            {
                Array.Resize(ref buffer, buffer.Length + sizeof(uint));
                Crc32Algorithm.ComputeAndWriteToEnd(buffer);
                if (_encrypt)
                    buffer.Xor(_encryptKey);
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
            ulong totalLength = 0;
            var packets = new List<Packet>();
            do
            {
                var packet = ReadPacket();
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
