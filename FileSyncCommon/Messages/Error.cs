using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon.Messages
{
    public enum Error
    {
        None = 0,
        FileNotExists,
        FileReadError,
        FileWriteError,
        OutOfRange,
        AuthenticateError,
        FileCheckError,
    }
}
