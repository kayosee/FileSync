using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon
{
    public class SemaphoreEx : SemaphoreSlim
    {
        public SemaphoreEx(int initialCount, int maxCount) : base(initialCount, maxCount)
        {
        }

        public void ReleaseAll()
        {
            while (Release() > 1) ;
        }
    }
}
