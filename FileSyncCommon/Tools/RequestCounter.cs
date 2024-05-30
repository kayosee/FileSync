using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon.Tools
{
    public class RequestCounter<TKey>
    {
        private class Pair
        {
            public long Value { get; set; }
            public DateTime Expire { get; set; }
            public Pair(long value, DateTime expire) { Value = value; Expire = expire; }
        }
        private ConcurrentDictionary<TKey, Pair> _counter = new();
        private TimeSpan _expiration;
        public RequestCounter(TimeSpan expiration)
        {
            _expiration = expiration;
        }
        public void Increase(TKey key, long value = 1)
        {
            _counter.AddOrUpdate(key, new Pair(value, DateTime.Now + _expiration), (key, oldValue) =>
            {
                oldValue.Value += value;
                oldValue.Expire = oldValue.Expire + _expiration;
                return oldValue;
            });
        }
        public void Decrease(TKey key, long value = 1)
        {
            if (_counter.ContainsKey(key))
            {
                if ((_counter[key].Value -= value) <= 0)
                    _counter.TryRemove(key, out var _);
                else
                    _counter[key].Expire += _expiration;
            }
        }
        public void Remove(TKey key)
        {
            _counter.TryRemove(key, out var _);
        }
        public long this[TKey key]
        {
            get { return _counter.GetValueOrDefault(key, new Pair(0, DateTime.Now + _expiration)).Value; }
        }
        public bool IsEmpty
        {
            get
            {
                var list = _counter.Where(f => f.Value.Expire >= DateTime.Now).ToList();
                foreach (var pair in list)
                {
                    _counter.TryRemove(pair);
                }
                return _counter.Count == 0;
            }
        }
        public void Clear()
        {
            _counter.Clear();
        }
    }
}
