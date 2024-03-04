using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon
{
    public class FileSeekException : Exception
    {
        public string FilePath { get; private set; }
        public long Position { get; private set; }
        public FileSeekException(string filePath, long position)
        {
            FilePath = filePath;
            Position = position;
        }
    }
}
