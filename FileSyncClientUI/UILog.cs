using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncClientUI
{
    public class UILog
    {
        private int _count;
        private string _content;
        private readonly int _capacity;
        public UILog(int max)
        {
            _capacity = max;
            _count = 0;
            _content = string.Empty;
        }
        public void Add(string log)
        {
            if (_count >= _capacity)
            {
                int i = _content.IndexOf(Environment.NewLine);
                _content = _content.Substring(i + 1);
                _count--;
            }
            _content += log + Environment.NewLine;
            _count++;
        }
        public void Clear()
        {
            _content = string.Empty;
            _count = 0;
        }
        public override string ToString()
        {
            return _content;
        }
    }
}
