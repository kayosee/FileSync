using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon
{
    public class RestrictQueue<T> : IDisposable
    {
        private int _max;
        private Queue<T> _queue;
        private SemaphoreSlim _push;
        private SemaphoreSlim _pull;
        public RestrictQueue(int max)
        {
            _max = max;
            _queue = new Queue<T>();
            _push = new SemaphoreSlim(max, max);
            _pull = new SemaphoreSlim(0, max);
        }
        public bool Dequeue(out T? item)
        {
            try
            {
                _pull.Wait();
                item = _queue.Dequeue();
                _push.Release();
                return true;
            }
            catch (Exception ex)
            {
                item = default(T);
                return false;
            }
        }

        public void Dispose()
        {
            if (_pull.CurrentCount != _max)
                _pull.Release(_max - _pull.CurrentCount);
            _pull.Dispose();

            if (_push.CurrentCount != _max)
                _push.Release(_max - _push.CurrentCount);
            _push.Dispose();
        }

        public bool Enqueue(T item)
        {
            try
            {
                _push.Wait();
                _queue.Enqueue(item);
                _pull.Release();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

    }
}
