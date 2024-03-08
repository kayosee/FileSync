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
            var interval = int.Parse(ConfigurationManager.AppSettings["interval"]);
            var encrypt = bool.Parse(ConfigurationManager.AppSettings["encrypt"]);
            var encryptKey = byte.Parse(ConfigurationManager.AppSettings["encryptKey"]);
            var password = (ConfigurationManager.AppSettings["password"]);
            Log.Logger = new LoggerConfiguration().WriteTo.Console().WriteTo.File("run.log", rollingInterval: RollingInterval.Day).CreateLogger();

            try
            {
                var client = new Client(ip, int.Parse(port), folder, interval, encrypt, encryptKey, password);
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
