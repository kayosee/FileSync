using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon;

public enum FileResponseType
{
    Empty = 0,
    Content = 1,
    FileDeleted = 2,
    FileReadError = 3
}
