using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon;

public enum PacketType
{
    Handshake = 0,
    FileListRequest = 1,
    FileListInfoResponse= 2,
    FileListDetailResponse = 3,
    FileContentInfoRequest = 4,
    FileContentInfoResponse = 5,
    FileContentDetailRequest = 6,
    FileContentDetailResponse= 7,
}
