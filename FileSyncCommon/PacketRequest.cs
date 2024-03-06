using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon
{
    public abstract class PacketRequest:Packet
    {
        protected long _requestId;
        protected PacketRequest(byte[] bytes) : base(bytes)
        {
        }

        protected PacketRequest(PacketType dataType, int clientId, long requestId) : base(dataType, clientId)
        {
            _requestId = requestId;
        }
        /// <summary>
        /// 请求ID
        /// </summary>
        public virtual long RequestId { get { return _requestId; } set { _requestId = value; } }
    }
}
