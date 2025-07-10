using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon.Tools
{
    public class AtomicInteger
    {
        private ulong _count = 0;
        public AtomicInteger(ulong initial = 0)
        {
            _count = initial;
        }
        // 原子递增
        public void Increment()
        {
            Interlocked.Increment(ref _count);
        }
        public ulong Value
        {
            get { return Interlocked.Read(ref _count); }
        }
        // 原子比较更新
        public void SetIfGreater(ulong newValue)
        {
            ulong initialValue;
            do
            {
                initialValue = _count;
                if (newValue <= initialValue) return;
            }
            while (Interlocked.CompareExchange(ref _count, newValue, initialValue) != initialValue);
        }
    }
}
