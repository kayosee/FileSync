using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon;

public enum PacketType
{
    Handshake ,
    FileListRequest ,
    FileListInfoResponse,
    FileListDetailResponse ,
    FileContentInfoRequest ,
    FileContentInfoResponse ,
    FileContentDetailRequest ,
    FileContentDetailResponse,
}
