using FileSyncClient;
using Serilog;
using System.Configuration;

namespace FileSyncClientCLI
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var ip = ConfigurationManager.AppSettings["ip"];
            var port = ConfigurationManager.AppSettings["port"];
            var localFolder = ConfigurationManager.AppSettings["localFolder"];
            var remoteFolder = ConfigurationManager.AppSettings["remoteFolder"];
            var interval = int.Parse(ConfigurationManager.AppSettings["interval"]);
            var password = (ConfigurationManager.AppSettings["password"]);
            Log.Logger = new LoggerConfiguration().WriteTo.Console().WriteTo.File("run.log", rollingInterval: RollingInterval.Day).CreateLogger();

            try
            {
                var client = new Client();
                client.Connect(ip, int.Parse(port), password);
                client.OnLogin += () =>
                {
                    client.Start(localFolder, remoteFolder, 0, 0, interval);
                };

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
