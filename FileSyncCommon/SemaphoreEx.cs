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
        private readonly int _max;
        public SemaphoreEx(int initial, int max)
        {
            _semaphore = new Semaphore(initial, max);
            _current = initial;
            _max = max;
        }
        public int Current { get { return _current; } }
        public int Release()
        {
            if (_current == _max)
                return 0;

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

        public void ReleaseAll()
        {
            while (_current < _max)
            {
                try
                {
                    Release();
                }
                catch (Exception ex)
                {
                    break;
                }
            }
        }
    }
}
