using DevExpress.Xpf.Core.Native;
using FileSyncClient;
using FileSyncCommon;
using FileSyncCommon.Messages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FileSyncClientUICommon;

namespace FileSyncClientUI
{
    public class ClientViewModel : Client, INotifyPropertyChanged
    {
        private string _name;
        private PathNode _root;
        private PathNode _currentNode;
        private UILog _logs;
        [JsonProperty]
        public string Name { get { return _name; } set { _name = value; OnPropertyChanged(nameof(Name)); } }
        public event PropertyChangedEventHandler? PropertyChanged;
        public ClientViewModel()
        {
            _logs = new UILog(100);
            _root = new PathNode("");
            _currentNode = _root;
            OnError += OnLogError;
            OnInformation += OnLogInformation;
            OnFolderListResponse += OnClientFolderListResponse;
            OnLogin += OnClientLogin;
            OnDisconnected += OnClientDisconnected;
        }

        private void OnClientDisconnected()
        {
            OnPropertyChanged(nameof(Runable));
            OnPropertyChanged(nameof(Pauseable));
            OnPropertyChanged(nameof(Running));
            OnPropertyChanged(nameof(IsConnected));
        }

        private void OnClientLogin()
        {
            OnPropertyChanged(nameof(Runable));
            OnPropertyChanged(nameof(Pauseable));
            OnPropertyChanged(nameof(Running));
            OnPropertyChanged(nameof(IsConnected));
        }

        private void OnLogInformation(string message)
        {
            if (System.Windows.Application.Current != null)
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            {
                _logs.Add(message);
                OnPropertyChanged(nameof(Logs));

            });
        }

        private void OnLogError(string message, Exception e)
        {
            if (System.Windows.Application.Current != null)
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
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
                return new SimpleCommand((f => true), f =>
                {
                    try
                    {
                        Start(LocalFolder, RemoteFolder, SyncDaysBefore, DeleteDaysBefore, Interval);
                        OnPropertyChanged(nameof(Running));
                        OnPropertyChanged(nameof(Runable));
                        OnPropertyChanged(nameof(Pauseable));
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(ex.ToString());
                    }
                });
            }
        }
        [JsonIgnore]
        public ICommand DoPause
        {
            get
            {
                return new SimpleCommand((f => true), f =>
                {
                    Pause();
                    OnPropertyChanged(nameof(Running));
                    OnPropertyChanged(nameof(Runable));
                    OnPropertyChanged(nameof(Pauseable));
                });
            }
        }
        [JsonIgnore]
        public string? LastItem
        {
            get; set;
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
                    OnPropertyChanged(nameof(Runable));
                    OnPropertyChanged(nameof(Pauseable));
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
                        _root.Nodes.Clear();
                        if (Reconnect())
                        {
                            OnPropertyChanged(nameof(IsConnected));
                            QueryFolders(_root.Path);
                        }
                        OnPropertyChanged(nameof(Runable));
                        OnPropertyChanged(nameof(Pauseable));
                    }
                });
            }
        }
        private void OnClientFolderListResponse(FolderListResponse response)
        {
            var node = _root.FindChild(response.Path, 0);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (node != null)
                {
                    node.IsExpand = true;
                    foreach (var dir in response.FolderList)
                    {
                        var newNode = node.Append(dir);
                    }
                    OnPropertyChanged(nameof(Root));
                    OnPropertyChanged(nameof(CurrentNode));
                }
            });
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
                OnPropertyChanged(nameof(Runable));
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
        public string Logs => _logs.ToString();

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
        [JsonIgnore]
        public PathNode Root { get => _root; set => _root = value; }
        [JsonIgnore]
        public ICommand Select
        {
            get
            {
                return new SimpleCommand(f => true, f =>
                {
                    if (f is PathNode)
                    {
                        var node = (PathNode)f;
                        CurrentNode = node;
                        if (!node.IsExpand)
                        {
                            QueryFolders(node.Path);
                        }
                    }
                });
            }
        }
        [JsonIgnore]
        public PathNode CurrentNode
        {
            get { return _currentNode; }
            set
            {
                _currentNode = value;
                RemoteFolder = _currentNode.Path;
                OnPropertyChanged(nameof(RemoteFolder));
                OnPropertyChanged(nameof(CurrentNode));
            }
        }
        [JsonIgnore]
        public bool Runable
        {
            get
            {
                return IsConnected && !Running && !string.IsNullOrEmpty(RemoteFolder);
            }
        }
        [JsonIgnore]
        public bool Pauseable
        {
            get
            {
                return IsConnected && Running;
            }
        }
        [JsonIgnore]
        public new bool IsConnected
        {
            get { return base.IsConnected; }
        }
        [JsonIgnore]
        public new bool Running
        {
            get { return base.Running; }
        }

        [JsonIgnore]
        public ICommand GoUpper
        {
            get
            {
                return new SimpleCommand(f => true, f =>
                {
                    if (CurrentNode.Parent != null)
                    {
                        CurrentNode = CurrentNode.Parent;
                    }
                });
            }
        }
        [JsonIgnore]
        public ICommand GoHome
        {
            get
            {
                return new SimpleCommand(f => true, f =>
                {
                    CurrentNode = Root;
                });
            }
        }
        public new int SyncDaysBefore
        {
            get
            {
                return base.SyncDaysBefore;
            }
            set
            {
                base.SyncDaysBefore = value;
                OnPropertyChanged(nameof(SyncDaysBefore));
            }
        }
        public new int DeleteDaysBefore
        {
            get
            {
                return base.DeleteDaysBefore;
            }
            set
            {
                base.DeleteDaysBefore = value;
                OnPropertyChanged(nameof(DeleteDaysBefore));
            }
        }
        public new String StartDate
        {
            get
            {
                return base.StartDate;
            }
            set
            {
                base.StartDate = value;
                OnPropertyChanged(nameof(StartDate));
            }
        }
        public new String EndDate
        {
            get
            {
                return base.EndDate;
            }
            set
            {
                base.EndDate = value;
                OnPropertyChanged(nameof(EndDate));
            }
        }
        public new String StartTime
        {
            get
            {
                return base.StartTime;
            }
            set
            {
                base.StartTime = value;
                OnPropertyChanged(nameof(StartTime));
            }
        }
        public new String EndTime
        {
            get
            {
                return base.EndTime;
            }
            set
            {
                base.EndTime = value;
                OnPropertyChanged(nameof(EndTime));
            }
        }
    }
}
