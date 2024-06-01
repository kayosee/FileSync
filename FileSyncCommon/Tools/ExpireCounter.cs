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
        private int _minutesExpire;
        private DateTime _expire;
        public ExpireCounter(int minutesExpire)
        {
            _minutesExpire = minutesExpire;
            _expire = DateTime.Now.AddMinutes(minutesExpire);
        }
        public void Increase(int value = 1)
        {
            _counter += value;
            _expire.AddMinutes(_minutesExpire);
        }
        public int Decrease(int value = 1)
        {
            _counter -= value;
            _expire.AddMinutes(_minutesExpire);
            return _counter;
        }
        public void Reset()
        {
            _counter = 0;
            _expire.AddMinutes(_minutesExpire);
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
