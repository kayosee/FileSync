using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Tools;

namespace FileSyncCommon.Messages
{
    public abstract class Request : Message
    {
        protected long _requestId;
        /// <summary>
        /// 请求ID
        /// </summary>
        public long RequestId { get { return _requestId; } set { _requestId = value; } }
        protected Request(MessageType messageType, int clientId, long requestId) : base(messageType, clientId)
        {
            _requestId = requestId;
        }
        protected Request(ByteArrayStream stream) : base(stream)
        {
            _requestId = stream.ReadInt64();
        }
        protected override ByteArrayStream GetStream()
        {
            var stream = base.GetStream();
            stream.Write(_requestId);
            return stream;
        }
    }
}
