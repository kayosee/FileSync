using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon.Exceptions
{
    public class FileChecksumException : Exception
    {
        public string FilePath { get; private set; }
        public long Position { get; private set; }
        public uint OldChecksum { get; private set; }
        public uint NewChecksum { get; private set; }
        public FileChecksumException(string filePath, long position, uint oldChecksum, uint newChecksum)
        {
            FilePath = filePath;
            Position = position;
            OldChecksum = oldChecksum;
            NewChecksum = newChecksum;
        }
    }
}
