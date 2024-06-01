using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon.Tools
{
    public class RequestCounters
    {
        public ConcurrentDictionary<long, ExpireCounter> dict = new ConcurrentDictionary<long, ExpireCounter>();
        public int Get(long id)
        {
            return dict.ContainsKey(id) ? 0 : dict[id].Counter;
        }
        public void Increase(long id, int value = 1)
        {
            dict.AddOrUpdate(id, new ExpireCounter(30), (s, t) => t);
            dict[id].Increase(value);
        }
        public void Decrease(long id, int value = 1)
        {
            if (dict.TryGetValue(id, out var counter))
                if (counter.Decrease(value) <= 0)
                    dict.TryRemove(id, out var _);
        }
        public bool IsEmpty()
        {
            var keys = dict.Keys;
            foreach (var key in keys)
            {
                if (dict[key].IsZeroOrExpired)
                    dict.TryRemove(key, out var _);
            }
            return dict.IsEmpty;
        }
    }
}
