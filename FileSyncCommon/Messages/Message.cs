using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Messages;
using FileSyncCommon.Tools;

namespace FileSyncCommon.Messages
{
    public abstract class Message : ISerialization
    {
        private byte _messageType;
        private int _clientId;

        public MessageType MessageType { get => (MessageType)_messageType; set => _messageType = (byte)value; }
        public int ClientId { get => _clientId; set => _clientId = value; }
        public Message(ByteArrayStream stream)
        {
            _messageType = stream.ReadByte();
            _clientId = stream.ReadInt32();
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
                throw new ArgumentNullException("字节流ByteArrayStream为空");

            var buffer = new byte[1];
            stream.Peek(buffer, 0, buffer.Length);
            var messageType = buffer[0];
            if (!Enum.IsDefined(typeof(MessageType), (int)messageType))
                throw new InvalidDataException("包类型不正确");

            var type = typeof(Message).Assembly.GetTypes().First(f => f.Name == Enum.GetName((DataType)messageType));
            Debug.Assert(type != null);
            var constructor = type.GetConstructors().First(f => f.GetParameters().Any(f => f.ParameterType == typeof(ByteArrayStream)));
            Message? sessionData = constructor.Invoke(new object[] { stream }) as Message;
            return sessionData;
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
                    new Packet(total,0,(short)total,stream.GetBuffer())
                };
            }

            var num = Math.Ceiling((double)stream.Length / Packet.MaxLength);
            var size = (short)Math.Floor(stream.Length / num);
            var result = new Packet[(int)num];
            for (var i = 0; i < num; i++)
            {
                result[i].TotalLength = stream.Length;
                result[i].Sequence = i;
                result[i].SliceData = new byte[size];
                result[i].SliceLength = size;
                stream.Read(result[i].SliceData, 0, size);
            }
            return result;
        }
    }
}
