using FileSyncClient;
using FileSyncCommon.Tools;
using Serilog;
using System.Configuration;

namespace FileSyncClientCLI
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var host = ConfigReader.GetString("host","");
            var port = ConfigReader.GetString("port", "");
            var localFolder = ConfigReader.GetString("localFolder", "");
            var remoteFolder = ConfigReader.GetString("remoteFolder", "");
            var interval = ConfigReader.GetInt("interval", 5);
            var password = ConfigReader.GetString("password", "");
            var certificate = ConfigReader.GetString("certificate", "");
            Log.Logger = new LoggerConfiguration().WriteTo.Console().WriteTo.File("run.log", rollingInterval: RollingInterval.Day).CreateLogger();

            try
            {
                var client = new Client();
                client.OnConnected += () =>
                {
                    client.Start(localFolder, remoteFolder, 0, 0, interval);
                };

                client.Connect(host, int.Parse(port),certificate, password);
                
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
                Log.Error(e.StackTrace);
            }

            Console.ReadKey();
        }
    }
}
