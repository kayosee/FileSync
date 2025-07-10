using System.Diagnostics;
using FileSyncCommon.Tools;
namespace FileSyncCommon.Messages
{
    public class Packet : ISerialization
    {
        private ulong _totalLength;
        private uint _sequence;
        private ushort _sliceLength;
        private byte[] _sliceData;
        public const ushort MaxLength = ushort.MaxValue;
        public ulong TotalLength { get => _totalLength; set => _totalLength = value; }
        public ushort SliceLength { get => _sliceLength; set => _sliceLength = value; }
        public byte[] SliceData { get => _sliceData; set => _sliceData = value; }
        public uint Sequence { get => _sequence; set => _sequence = value; }

        public Packet() { }
        public Packet(ulong totalLength, uint sequence, ushort sliceLength, byte[] data)
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
                _totalLength = stream.ReadULong();
                _sequence = stream.ReadUInt();
                _sliceLength = stream.ReadUShort();
                _sliceData = new byte[_sliceLength];
                stream.Read(_sliceData, 0, _sliceLength);
            }
        }
        public byte[] Serialize()
        {
            Debug.Assert(_sequence >= 0);
            Debug.Assert(_sliceLength > 0);
            using (var stream = new ByteArrayStream())
            {
                stream.Write(_totalLength);
                stream.Write(_sequence);
                stream.Write(_sliceLength);
                stream.Write(_sliceData, 0, _sliceData.Length);
                return stream.GetBuffer();
            }
        }
    }
}
