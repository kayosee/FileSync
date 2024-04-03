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
            var encrypt = bool.Parse(ConfigurationManager.AppSettings["encrypt"]);
            var encryptKey = byte.Parse(ConfigurationManager.AppSettings["encryptKey"]);
            var password = (ConfigurationManager.AppSettings["password"]);

            Log.Logger = new LoggerConfiguration().WriteTo.Console().WriteTo.File("run.log", rollingInterval: RollingInterval.Day).CreateLogger();
            Server server = new Server(port, folder, encrypt, encryptKey,password);
            server.Start();
            Log.Information($"正运行在：{port}端口，监视目录：{folder}");
            Console.ReadKey();
        }
    }
}
