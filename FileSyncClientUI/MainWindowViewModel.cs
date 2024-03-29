using DevExpress.Mvvm.Native;
using FileSyncCommon;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
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
            var config = System.IO.Path.Combine(Environment.CurrentDirectory, "config.json");
            if (File.Exists(config))
            {
                try
                {
                    var data = JsonConvert.DeserializeObject<ClientModelView[]>(File.ReadAllText(config));
                    if (data != null)
                    {
                        data.ForEach<ClientModelView>(f => f.PropertyChanged += ClientPropertyChanged);
                        Clients = new ObservableCollection<ClientModelView>(data);
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
                    var client = new ClientModelView() { Name = "新建" };
                    client.PropertyChanged += ClientPropertyChanged;
                    Clients.Add(client);
                });
            }
        }

        private void ClientPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            var config = System.IO.Path.Combine(Environment.CurrentDirectory, "config.json");
            var data = JsonConvert.SerializeObject(Clients);
            File.WriteAllText(config, data);
        }

        public ICommand Show
        {
            get
            {
                return new SimpleCommand(f => true, f =>
                {
                    System.Windows.Application.Current.MainWindow.Show();
                    SystemCommands.RestoreWindow(System.Windows.Application.Current.MainWindow);
                });
            }
        }

        public ICommand Remove
        {
            get
            {
                return new SimpleCommand(f => true, f =>
                {
                    Clients.Remove(f as ClientModelView);
                    OnPropertyChanged(nameof(Clients));
                    ClientPropertyChanged(null, null);
                });
            }
        }
    }
}
