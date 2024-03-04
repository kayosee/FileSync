using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon
{
    public class PacketFileContentInfoRequest : Packet
    {
        private long _inquireId;
        private long _requestId;
        private string _path;
        private int _pathLength;
        public PacketFileContentInfoRequest(byte[] bytes) : base(bytes)
        {
        }

        public PacketFileContentInfoRequest(int clientId) : base(PacketType.FileContentInfoRequest, clientId)
        {
        }

        public long InquireId { get => _inquireId; set => _inquireId = value; }
        public long RequestId { get => _requestId; set => _requestId = value; }
        public string Path { get => _path; set => _path = value; }

        protected override void Deserialize(byte[] bytes)
        {
            throw new NotImplementedException();
        }

        protected override byte[] Serialize()
        {
            throw new NotImplementedException();
        }
    }
}
