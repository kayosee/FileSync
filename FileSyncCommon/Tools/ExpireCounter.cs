using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon.Tools
{
    public class ExpireCounter
    {
        private volatile int _counter = 0;
        private TimeSpan _expiration;
        private DateTime _expire;
        public ExpireCounter(TimeSpan expiration)
        {
            _expiration = expiration;
            _expire = DateTime.Now + expiration;
        }
        public void Increase(int value = 1)
        {
            _counter += value;
            _expire += _expiration;
        }
        public int Decrease(int value = 1)
        {
            _counter -= value;
            _expire += _expiration;
            return _counter;
        }
        public void Reset()
        {
            _counter = 0;
            _expire += _expiration;
        }
        public bool IsZeroOrExpired
        {
            get
            {
                return _counter == 0 || _expire < DateTime.Now;
            }
        }

        public int Counter { get => _counter; }
        public DateTime Expire { get => _expire; }
    }
}
