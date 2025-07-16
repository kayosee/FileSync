using FileSyncCommon.Messages;
using FileSyncCommon.Tools;
using Force.Crc32;
using Serilog;
using System.Net.Security;
namespace FileSyncCommon
{
    public sealed class StreamPacker
    {
        private readonly int QueueSize = Environment.ProcessorCount;
        private volatile bool _disposed;
        private Thread _producer;
        private Thread _consumer;
        private SslStream _sslStream;
        private FixedLengthQueue<Messages.Message> _queue;
        public delegate void ReceivePackageHandler(Message message);
        public delegate void SendPackageHandler(Message message);
        public delegate void StreamErrorHandler(StreamPacker packer, Exception e);
        public delegate void DataErrorHandler(StreamPacker packer, string message);
        public event ReceivePackageHandler? OnReceivePackage;
        public event SendPackageHandler? OnSendPackage;
        public event StreamErrorHandler? OnStreamError;
        public event DataErrorHandler? OnDataError;
        public StreamPacker(SslStream stream)
        {
            _sslStream = stream;
            //_client.ReceiveBufferSize = (int)Math.Pow(1024, 3);//接收缓存区太小，会产生ZEROWINDOW，导致后面的Send阻塞
            _queue = new FixedLengthQueue<Message>(Environment.ProcessorCount);
            _producer = new Thread((s) =>
                {
                    while (!_disposed)
                    {
                        var message = ReadMessage();
                        if (message != null)
                        {
                            _queue.Enqueue(message);
                        }
                    }
                });
            _producer.Name = "producer-" + 1;
            _producer.IsBackground = true;
            _producer.Start();

            _consumer = new Thread((s) =>
            {
                while (!_disposed)
                {
                    if (_queue.Dequeue(out var packet))
                    {
                        OnReceivePackage?.Invoke(packet);
                    }
                }
            });
            _consumer.Name = "consumer-" + 1;
            _consumer.IsBackground = true;
            _consumer.Start();
        }
        private void DoSocketError(StreamPacker streamManager, Exception e)
        {
            if (_disposed)
                return;

            Disconnect();
            OnStreamError?.Invoke(this, e);
        }
        public void Disconnect()
        {
            try
            {
                _disposed = true;
                _queue.Dispose();
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
            }

            if (OnSendPackage != null)
                OnSendPackage(message);
        }
        private bool Read(int length, out byte[] buffer, ref List<byte> whole, int maxWaitSeconds = -1)
        {

            buffer = new byte[length];
            try
            {
                var total = 0;
                do
                {
                    int ret = _sslStream.Read(buffer, total, length - total);
                    total += ret;
                } while (total < length);

                whole.AddRange(buffer);

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
                {
                    if (buffer.Length == 0)
                    {
                        Log.Error("发送数据为空");
                        return 0;
                    }
                    Array.Resize(ref buffer, buffer.Length + sizeof(uint));
                    Crc32Algorithm.ComputeAndWriteToEnd(buffer);
                    _sslStream.Write(buffer);
                    return buffer.Length;
                }
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
        ~StreamPacker()
        {
            Disconnect();
        }
    }
}
