using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon.Messages;

public enum MessageType
{
    AuthenticateRequest,
    AuthenticateResponse,
    [Obsolete]
    Handshake,
    FileInfoRequest,
    FileInfoResponse,
    FileListTotalRequest,
    FileListTotalResponse,
    FileListDetailRequest,
    FileListDetailResponse,
    FileContentRequest,
    FileContentResponse,
    FolderListRequest,
    FolderListResponse,
}
