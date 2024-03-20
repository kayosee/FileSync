using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon
{
    public class SemaphoreEx
    {
        private readonly Semaphore _semaphore;
        private volatile int _current;
        public SemaphoreEx(int initial, int total)
        {
            _semaphore = new Semaphore(initial, total);
            _current = initial;
        }
        public int Current { get { return _current; } }
        public int Release()
        {
            int n = _semaphore.Release();
            _current++;
            return n;
        }
        public bool Wait()
        {
            bool success = _semaphore.WaitOne();
            _current--;
            return success;
        }
    }
}
