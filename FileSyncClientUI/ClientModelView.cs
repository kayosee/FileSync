using FileSyncCommon;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using UICommon;

namespace FileSyncClientUI
{
    public class ClientModelView : Client, INotifyPropertyChanged
    {
        private string _name;
        public string Name { get { return _name; } set { _name = value; OnPropertyChanged(nameof(Name)); } }
        private string host;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public ICommand Stop
        {
            get
            {
                return new SimpleCommand((f => true), f =>
                {
                    Disconnect();
                    OnPropertyChanged(nameof(IsConnected));
                    OnPropertyChanged(nameof(IsRunning));
                });
            }
        }
        public ICommand Start
        {
            get
            {
                return new SimpleCommand((f => true), f =>
                {
                    if (!IsConnected)
                    {
                        if (Connect())
                        {
                            OnPropertyChanged(nameof(IsConnected));
                            OnPropertyChanged(nameof(IsRunning));
                        }
                    }
                });
            }
        }
        public new string Host
        {
            get => host; set
            {
                host = value;
                OnPropertyChanged(nameof(Host));
            }
        }
    }
}
