using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon.Tools
{
    public class Signal
    {
        private ManualResetEvent _event;
        private bool _state;
        public Signal(bool initialState)
        {
            _state = initialState;
            _event = new ManualResetEvent(initialState);
        }

        public bool State { get { return _state; } }
        public void Prohibited()
        {
            _state = false;
            _event.Reset();
        }
        public void Promitted()
        {
            _state = true;
            _event.Set();
        }
        public bool Wait()
        {
            return _event.WaitOne();
        }
    }
}
