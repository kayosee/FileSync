using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon.Messages
{
    public interface ISerialization
    {
        byte[] Serialize();
    }
}
