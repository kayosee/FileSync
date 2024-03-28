using DevExpress.Mvvm.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncClientUI
{
    public class TreeViewEventArgsConverter : IEventArgsConverter
    {
        public object Convert(object sender, object args)
        {
            var e = args as DevExpress.Xpf.Grid.SelectedItemChangedEventArgs;
            if (e != null)
            {
                return e.NewItem;
            }
            return null;
        }
    }
}
