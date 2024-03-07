using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon
{
    public class RequestCounter<TKey>
    {
        private ConcurrentDictionary<TKey, long> _counter = new ConcurrentDictionary<TKey, long>();
        public void Increase(TKey key, long value = 1)
        {
            _counter.AddOrUpdate(key, value, (key, oldValue) => oldValue + value);
        }
        public void Decrease(TKey key, long value = 1)
        {
            if (_counter.ContainsKey(key))
            {
                if ((_counter[key] -= (long)value) <= 0)
                    _counter.TryRemove(key, out var _);
            }
        }
        public void Remove(TKey key)
        {
            _counter.TryRemove(key,out var _);
        }
        public long this[TKey key]
        {
            get { return _counter.GetValueOrDefault(key, 0); }
        }
        public bool IsEmpty { get { return _counter.Count == 0; } }
    }
}
