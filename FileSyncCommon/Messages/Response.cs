using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileSyncCommon.Tools;

namespace FileSyncCommon.Messages
{
    public abstract class Response : Message
    {
        protected long _requestId;
        protected byte _lastest;
        /// <summary>
        /// 请求ID
        /// </summary>
        public long RequestId { get { return _requestId; } set { _requestId = value; } }
        public bool Latest { get { return _lastest > 0; } set { _lastest = (byte)(value ? 1 : 0); } }
        protected Response(MessageType messageType, int clientId, long requestId, bool latest) : base(messageType, clientId)
        {
            _requestId = requestId;
            _lastest = (byte)(latest ? 1 : 0);
        }

        protected Response(ByteArrayStream stream) : base(stream)
        {
            _requestId = stream.ReadLong();
            _lastest = stream.ReadByte();
        }
        protected override ByteArrayStream GetStream()
        {
            var stream = base.GetStream();
            stream.Write(_requestId);
            stream.Write(_lastest);
            return stream;
        }

    }
}
