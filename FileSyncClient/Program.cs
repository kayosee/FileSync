using HYFTPClient;
using Serilog;
using System.Configuration;

namespace FileSyncClient
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var ip = ConfigurationManager.AppSettings["ip"];
            var port = ConfigurationManager.AppSettings["port"];
            var folder = ConfigurationManager.AppSettings["folder"];
            var encrypt = bool.Parse(ConfigurationManager.AppSettings["encrypt"]);
            var encryptKey = byte.Parse(ConfigurationManager.AppSettings["encryptKey"]);
            Log.Logger = new LoggerConfiguration().WriteTo.Console().WriteTo.File("run.log", rollingInterval: RollingInterval.Day).CreateLogger();

            try
            {
                var client = new Client(ip, int.Parse(port), folder, encrypt, encryptKey);
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
