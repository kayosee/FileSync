using FileSyncCommon;
using Newtonsoft.Json;
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
    public class ClientModelView : Client, INotifyPropertyChanged
    {      
        private string _name;
        private ObservableCollection<string> _logs;
        [JsonProperty]
        public string Name { get { return _name; } set { _name = value; OnPropertyChanged(nameof(Name)); } }
        public event PropertyChangedEventHandler? PropertyChanged;
        public ClientModelView()
        {
            _logs = new ObservableCollection<string>();
            OnError += OnLogError;
            OnInformation += OnLogInformation;
        }

        private void OnLogInformation(string message)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _logs.Add(message);
                OnPropertyChanged(nameof(Logs));
            });
        }

        private void OnLogError(string message, Exception e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _logs.Add(message);
                OnPropertyChanged(nameof(Logs));
            });
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        [JsonIgnore]
        public ICommand DoStart
        {
            get
            {
                return new SimpleCommand((f => IsConnected && !Running), f =>
                {
                    Start();
                    OnPropertyChanged(nameof(Running));
                });
            }
        }
        [JsonIgnore]
        public ICommand DoPause
        {
            get
            {
                return new SimpleCommand((f => IsConnected && Running), f =>
                {
                    Pause();
                    OnPropertyChanged(nameof(Running));
                });
            }
        }
        [JsonIgnore]
        public ICommand DoDisconnect
        {
            get
            {
                return new SimpleCommand((f => true), f =>
                {
                    Disconnect();
                    OnPropertyChanged(nameof(IsConnected));
                });
            }
        }
        [JsonIgnore]
        public ICommand DoConnect
        {
            get
            {
                return new SimpleCommand((f => true), f =>
                {
                    if (!IsConnected)
                    {
                        _logs.Clear();
                        OnPropertyChanged(nameof(Logs));
                        if (Connect())
                        {
                            OnPropertyChanged(nameof(IsConnected));
                        }
                    }
                });
            }
        }
        [JsonProperty]
        public new string Host
        {
            get => base.Host; set
            {
                base.Host = value;
                OnPropertyChanged(nameof(Host));
            }
        }
        [JsonProperty]
        public new int Port
        {
            get => base.Port; set
            {
                base.Port = value;
                OnPropertyChanged(nameof(Port));
            }
        }
        [JsonProperty]
        public new string LocalFolder
        {
            get => base.LocalFolder;
            set
            {
                base.LocalFolder = value;
                OnPropertyChanged(nameof(LocalFolder));
            }
        }
        [JsonProperty]
        public new string RemoteFolder
        {
            get => base.RemoteFolder;
            set
            {
                base.RemoteFolder = value;
                OnPropertyChanged(nameof(RemoteFolder));
            }
        }
        [JsonProperty]
        public new bool Encrypt
        {
            get => base.Encrypt;
            set
            {
                base.Encrypt = value;
                OnPropertyChanged(nameof(Encrypt));
            }
        }
        [JsonProperty]
        public new byte EncryptKey
        {
            get => base.EncryptKey;
            set
            {
                base.EncryptKey = value;
                OnPropertyChanged(nameof(EncryptKey));
            }
        }
        [JsonProperty]
        public new string Password
        {
            get => base.Password;
            set
            {
                base.Password = value;
                OnPropertyChanged(nameof(Password));
            }
        }
        [JsonProperty]
        public new int Interval
        {
            get => base.Interval;
            set
            {
                base.Interval = value;
                OnPropertyChanged(nameof(Interval));
            }
        }
        [JsonIgnore]
        public ObservableCollection<string> Logs { get => _logs; set => _logs = value; }

        [JsonIgnore]
        public ICommand SelectLocalFolder
        {
            get
            {
                return new SimpleCommand(f => true, f =>
                {
                    FolderBrowserDialog dialog = new FolderBrowserDialog();
                    if (dialog.ShowDialog() == DialogResult.OK)
                        LocalFolder = dialog.SelectedPath;
                });
            }
        }
    }
}
