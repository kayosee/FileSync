using FileSyncCommon;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using UICommon;

namespace FileSyncClientUI
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ClientModelView> Clients { get; set; }
        public MainWindowViewModel()
        {
            Clients = new ObservableCollection<ClientModelView>();
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
