using System;
using System.Threading;
using System.Threading.Tasks;

namespace FileSyncCommon.Tools
{
    public sealed class UnquantifiedSignal : IDisposable
    {
        private int _count;
        private readonly ManualResetEvent _signal = new ManualResetEvent(false);
        private readonly object _syncRoot = new object();
        private bool _disposed = false;

        public UnquantifiedSignal(int initialCount = 0)
        {
            SetCountInternal(initialCount);
        }

        public bool State
        {
            get
            {
                lock (_syncRoot) return _count > 0;
            }
        }

        public int Count
        {
            get
            {
                lock (_syncRoot) return _count;
            }
        }

        /// <summary>
        /// 等待信号可用（不消耗信号）
        /// </summary>
        public bool Wait(TimeSpan? timeout = null)
        {
            lock (_syncRoot)
            {
                if (_disposed) return false;

                // 直接在有信号时返回
                if (_count > 0)
                {
                    return true;
                }
                // 无可用信号时等待
                if (timeout.HasValue)
                {
                    return _signal.WaitOne(timeout.Value);
                }

                _signal.WaitOne();
                return true;
            }
        }

        /// <summary>
        /// 尝试获取信号（消耗一个信号）
        /// </summary>
        /// <returns>成功获取返回true，否则false</returns>
        public bool Acquire()
        {
            lock (_syncRoot)
            {
                if (_disposed) return false;

                if (_count > 0)
                {
                    _count--;
                    UpdateSignalState();
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 释放信号（增加一个信号）
        /// </summary>
        public void Release()
        {
            lock (_syncRoot)
            {
                if (_disposed) return;

                _count++;
                UpdateSignalState();
            }
        }

        private void SetCountInternal(int count)
        {
            lock (_syncRoot)
            {
                if (_disposed) return;

                _count = Math.Max(0, count);
                UpdateSignalState();

                // 更新信号状态
                if (_count > 0)
                {
                    _signal.Set();
                }
                else
                {
                    _signal.Reset();
                }
            }
        }

        private void UpdateSignalState()
        {
            if (_count > 0)
            {
                if (!_signal.WaitOne(0)) _signal.Set();
            }
            else
            {
                if (_signal.WaitOne(0)) _signal.Reset();
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed) return;
                _disposed = true;
                _signal.Set(); // 唤醒所有等待者
                _signal.Close();
            }
        }
    }
}