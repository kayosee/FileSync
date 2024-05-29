using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Tools;
namespace FileSyncCommon.Messages
{
    public class Packet : ISerialization
    {
        public static readonly byte[] Flag = new byte[] { 75, 65, 89, 79 };

        private long _totalLength;
        private int _sequence;
        private short _sliceLength;
        private byte[] _sliceData;
        public const ushort MaxLength = ushort.MaxValue;
        public long TotalLength { get => _totalLength; set => _totalLength = value; }
        public short SliceLength { get => _sliceLength; set => _sliceLength = value; }
        public byte[] SliceData { get => _sliceData; set => _sliceData = value; }
        public int Sequence { get => _sequence; set => _sequence = value; }

        public Packet() { }
        public Packet(long totalLength, int sequence, short sliceLength, byte[] data)
        {
            _totalLength = totalLength;
            _sequence = sequence;
            _sliceLength = sliceLength;
            _sliceData = data;
        }
        public Packet(byte[] buffer)
        {
            using (var stream = new ByteArrayStream(buffer))
            {
                var _ = new byte[Flag.Length];
                stream.Read(_, 0, _.Length);

                _totalLength = stream.ReadInt64();
                _sequence = stream.ReadInt32();
                _sliceLength = stream.ReadInt16();
                _sliceData = new byte[_sliceLength];
                stream.Read(_sliceData, 0, _sliceLength);
            }
        }
        public byte[] Serialize()
        {
            using (var stream = new ByteArrayStream())
            {
                stream.Write(Flag, 0, Flag.Length);
                stream.Write(_totalLength);
                stream.Write(_sequence);
                stream.Write(_sliceLength);
                stream.Write(_sliceData, 0, _sliceData.Length);
                return stream.GetBuffer();
            }
        }
    }
}
