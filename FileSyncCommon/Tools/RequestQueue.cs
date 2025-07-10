using System;
using System.Collections.Concurrent;
using System.Threading;

namespace FileSyncCommon.Tools
{
    public class RequestQueue
    {
        private readonly ConcurrentQueue<Action> _requestQueue = new();
        private Thread _thread;
        private readonly ManualResetEvent _workSignal = new(true);
        private readonly object _releaseLock = new object();

        public RequestQueue()
        {
            _thread = new Thread(ProcessRequests)
            {
                IsBackground = true
            };
            _thread.Name = "RequestQueue";
            _thread.Start();
        }

        private void ProcessRequests()
        {
            while (true)
            {
                // 等待工作信号或停止信号
                if (_workSignal.WaitOne())
                {
                    // 确保每次只处理一个任务
                    if (_requestQueue.TryDequeue(out var request))
                    {
                        _workSignal.Reset();
                        try
                        {
                            request?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing request: {ex.Message}");
                        }
                    }
                }
            }
        }

        public void Enqueue(Action request)
        {
            _requestQueue.Enqueue(request);
        }

        public void Dequeue()
        {
            lock (_releaseLock)
            {
                // 每次Release只触发一个任务执行
                _workSignal.Set();
            }
        }

        public void Clear()
        {
            while (_requestQueue.TryDequeue(out _)) { }
        }

        public bool IsEmpty() => _requestQueue.IsEmpty;

    }
}