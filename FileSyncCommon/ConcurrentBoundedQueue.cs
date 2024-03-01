using System;
using System.Collections.Concurrent;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon;

public class ConcurrentBoundedQueue<T> : IEnumerable<T>
{
    private readonly ConcurrentQueue<T> _queue;
    private readonly int _boundedCapacity;
    private volatile int _approximateCount;

    public ConcurrentBoundedQueue(int boundedCapacity)
    {
        if (boundedCapacity < 1)
            throw new ArgumentOutOfRangeException(nameof(boundedCapacity));
        _queue = new();
        _boundedCapacity = boundedCapacity;
        _approximateCount = 0;
    }

    public int BoundedCapacity => _boundedCapacity;
    public int Count => _queue.Count;

    public bool TryEnqueue(T item)
    {
        if (_approximateCount >= _boundedCapacity) return false;
        if (Interlocked.Increment(ref _approximateCount) > _boundedCapacity)
        {
            Interlocked.Decrement(ref _approximateCount);
            return false;
        }
        _queue.Enqueue(item);
        return true;
    }

    public bool TryDequeue(out T item)
    {
        bool success = _queue.TryDequeue(out item);
        if (success) Interlocked.Decrement(ref _approximateCount);
        return success;
    }

    public T[] ToArray() => _queue.ToArray();
    public IEnumerator<T> GetEnumerator() => _queue.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
