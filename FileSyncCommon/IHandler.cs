using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon
{
    public interface IHandler
    {
        object Process(Packet packet,SocketSession session);
    }
}
