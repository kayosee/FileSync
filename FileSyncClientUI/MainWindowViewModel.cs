using DevExpress.Mvvm.Native;
using FileSyncCommon;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using FileSyncClientUICommon;

namespace FileSyncClientUI
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ClientViewModel> Clients { get; set; }
        public MainWindowViewModel()
        {
            Clients = new ObservableCollection<ClientViewModel>();
            var config = System.IO.Path.Combine(Environment.CurrentDirectory, "config.json");
            if (File.Exists(config))
            {
                try
                {
                    var data = JsonConvert.DeserializeObject<ClientViewModel[]>(File.ReadAllText(config));
                    if (data != null)
                    {
                        data.ForEach<ClientViewModel>(f => f.PropertyChanged += ClientPropertyChanged);
                        Clients = new ObservableCollection<ClientViewModel>(data);
                        OnPropertyChanged(nameof(Clients));
                    }
                }
                catch (Exception e) { }
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public ICommand NewClient
        {
            get
            {
                return new SimpleCommand(f => true, f =>
                {
                    var client = new ClientViewModel() { Name = "新建" };
                    client.PropertyChanged += ClientPropertyChanged;
                    Clients.Add(client);
                });
            }
        }

        private void ClientPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender == null)
                return;

            Type type = sender.GetType();
            Attribute? ignore = type.GetProperty(e.PropertyName).GetCustomAttribute(typeof(JsonIgnoreAttribute));
            if (ignore == null)
            {
                var config = System.IO.Path.Combine(Environment.CurrentDirectory, "config.json");
                var data = JsonConvert.SerializeObject(Clients);
                File.WriteAllText(config, data);
            }
        }

        public ICommand Show
        {
            get
            {
                return new SimpleCommand(f => true, f =>
                {
                    System.Windows.Application.Current.MainWindow.Show();
                    SystemCommands.RestoreWindow(System.Windows.Application.Current.MainWindow);
                    System.Windows.Application.Current.MainWindow.Activate();
                });
            }
        }

        public ICommand Remove
        {
            get
            {
                return new SimpleCommand(f => true, f =>
                {
                    Clients.Remove(f as ClientViewModel);
                    OnPropertyChanged(nameof(Clients));
                    ClientPropertyChanged(this, new PropertyChangedEventArgs(nameof(Clients)));
                });
            }
        }
    }
}
