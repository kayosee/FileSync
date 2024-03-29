using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncClientUI
{
    public class LimitQueue<T>:ObservableCollection<T>
    {
        private readonly int _capacity;
        public LimitQueue(int max)
        {
            _capacity = max;
        }
        public new void Add(T item)
        {
            if(Count>=_capacity)
            {
                RemoveAt(0);
            }
            base.Add(item);
        }
    }
}
