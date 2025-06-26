using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon.Tools
{
    public class DeferredLock
    {
        private readonly ConcurrentQueue<Action> _deferredActions = new ConcurrentQueue<Action>();
        private int _lockState = 0; // 0: unlocked, 1: locked

        /// <summary>
        /// 尝试获取锁并在释放时执行传入的代码
        /// </summary>
        /// <param name="onRelease">锁释放时要执行的代码</param>
        /// <returns>true: 立即执行了锁获取和任务添加; false: 锁已被占用，任务已加入队列等待执行</returns>
        public bool TryAcquire(Action onRelease)
        {
            // 尝试原子性地更改锁状态
            if (Interlocked.CompareExchange(ref _lockState, 1, 0) == 0)
            {
                // 锁获取成功，添加释放时执行的任务
                onRelease.Invoke();
                return true;
            }

            // 锁已被占用，加入队列等待
            _deferredActions.Enqueue(onRelease);
            return false;
        }

        /// <summary>
        /// 异步尝试获取锁（不阻塞调用线程）
        /// </summary>
        public async Task<bool> TryAcquireAsync(Action onRelease)
        {
            return await Task.Run(() => TryAcquire(onRelease));
        }

        public bool IsEmpty()
        {
            return _deferredActions.IsEmpty;
        }

        /// <summary>
        /// 释放锁并执行所有待处理的任务
        /// </summary>
        public void Release()
        {
            // 确保只有锁持有者可以释放
            if (Interlocked.CompareExchange(ref _lockState, 0, 1) != 1)
                throw new InvalidOperationException("Cannot release an unlocked lock");

            // 执行所有累积的操作
            if(_deferredActions.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    // 处理异常但不中断其他任务
                    Console.WriteLine($"Error executing deferred action: {ex.Message}");
                }
            }
        }
    }
}
