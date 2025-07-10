using FileSyncCommon.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncClient
{
    public class ClientMessage
    {
        public Message Message { get; set; }
        public bool Enqueue {  get; set; }

        public ClientMessage(Message message,bool enqueue=false) {
            Message = message;
            Enqueue = enqueue;
        }
    }
}
