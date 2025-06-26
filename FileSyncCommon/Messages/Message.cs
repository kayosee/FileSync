using System.Collections.Concurrent;
using System.Reflection;
using FileSyncCommon.Tools;
using Serilog;

namespace FileSyncCommon.Messages
{
    public abstract class Message : ISerialization
    {
        private byte _messageType;
        private int _clientId;
        private static ConcurrentDictionary<int, ConstructorInfo> _constructors = new ConcurrentDictionary<int, ConstructorInfo>();

        public MessageType MessageType { get => (MessageType)_messageType; set => _messageType = (byte)value; }
        public int ClientId { get => _clientId; set => _clientId = value; }

        public Message(ByteArrayStream stream)
        {
            _messageType = stream.ReadByte();
            _clientId = stream.ReadInt();
        }
        public Message(MessageType messageType, int clientId) { _messageType = (byte)messageType; _clientId = clientId; }
        protected virtual ByteArrayStream GetStream()
        {
            using (var stream = new ByteArrayStream())
            {
                stream.Write(_messageType);
                stream.Write(_clientId);
                return stream;
            }
        }
        public override string ToString()
        {
            return GetType().Name;
        }

        public byte[] Serialize()
        {
            return GetStream().GetBuffer();
        }
        public static Message? FromStream(ByteArrayStream stream)
        {
            if (stream == null)
            {
                Log.Error("字节流ByteArrayStream为空");
                return null;
            }

            var buffer = new byte[1];
            stream.Peek(buffer, 0, buffer.Length);
            var messageType = buffer[0];
            if (!Enum.IsDefined(typeof(MessageType), (int)messageType))
            {
                Log.Error("包类型不正确");
                return null;
            }

            Message? sessionData = (Message?)ConvertMessage(messageType, stream);
            return sessionData;
        }

        private static object? ConvertMessage(int messageType, ByteArrayStream stream)
        {
            ConstructorInfo constructor = null;
            if (_constructors.ContainsKey(messageType))
            {
                constructor = _constructors[messageType];
            }
            else
            {
                var type = typeof(Message).Assembly.GetTypes().First(f => f.Name == Enum.GetName((MessageType)messageType));
                constructor = type.GetConstructors().First(f => f.GetParameters().Any(f => f.ParameterType == typeof(ByteArrayStream)));
                if (constructor != null)
                {
                    _constructors.AddOrUpdate(messageType, constructor, (key, value) => value);
                }
            }

            if (constructor != null)
                return constructor.Invoke(new object[] { stream });

            return null;
        }
        public static Message? FromPackets(Packet[] packets)
        {
            Message? data = null;
            using (var stream = new ByteArrayStream())
            {
                foreach (var packet in packets.OrderBy(f => f.Sequence))
                {
                    stream.Write(packet.SliceData, 0, packet.SliceLength);
                }

                data = FromStream(stream);
                return data;
            }
        }
        public Packet[] ToPackets()
        {
            var stream = GetStream();
            var total = stream.Length;
            if (total < Packet.MaxLength)
            {
                return new Packet[1]
                {
                    new Packet((ulong)total,0,(ushort)total,stream.GetBuffer())
                };
            }

            var num = Math.Ceiling((double)stream.Length / Packet.MaxLength);
            var result = new Packet[(int)num];
            var remains = (ulong)stream.Length;
            var i = new AtomicInteger();
            while (remains > 0)
            {
                var currentSize = Math.Min(Packet.MaxLength, remains);
                result[i.Value] = new Packet();
                result[i.Value].TotalLength = (ulong)stream.Length;
                result[i.Value].Sequence = (uint)i.Value;
                result[i.Value].SliceData = new byte[currentSize];
                result[i.Value].SliceLength = (ushort)currentSize;
                stream.Read(result[i.Value].SliceData, 0, (int)currentSize);
                i.Increment();
                remains -= currentSize;
            }
            return result;
        }
    }
}
