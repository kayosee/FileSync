using Serilog;
using System.Configuration;

namespace FileSyncServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var folder = ConfigurationManager.AppSettings["folder"];
            var port = int.Parse(ConfigurationManager.AppSettings["port"]);
            var interval = uint.Parse(ConfigurationManager.AppSettings["interval"]);
            var encrypt = bool.Parse(ConfigurationManager.AppSettings["encrypt"]);
            var encryptKey = byte.Parse(ConfigurationManager.AppSettings["encryptKey"]);
            var daysBefore = int.Parse(ConfigurationManager.AppSettings["daysBefore"]);

            Log.Logger = new LoggerConfiguration().WriteTo.Console().WriteTo.File("run.log", rollingInterval: RollingInterval.Day).CreateLogger();
            Server server = new Server(port, folder, interval, encrypt, encryptKey, daysBefore);
            server.Start();
            Log.Information($"正运行在：{port}端口，监视目录：{folder}");
            Console.ReadKey();
        }
    }
}
