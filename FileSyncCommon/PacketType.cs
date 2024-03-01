using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon;

public enum PacketType
{
    FileNotification = 0,
    FileInquire = 1,
    FileInformation = 2,
    FileRequest = 3,
    FileResponse = 4,
}
