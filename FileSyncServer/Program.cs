using Serilog;
using System.Configuration;

namespace FileSyncServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            var folder = ConfigurationManager.AppSettings["folder"];
            var port = int.Parse(ConfigurationManager.AppSettings["port"]);
            var password = (ConfigurationManager.AppSettings["password"]);

            Log.Logger = new LoggerConfiguration().WriteTo.Console().WriteTo.File("run.log", rollingInterval: RollingInterval.Day).CreateLogger();
            Server server = new Server(port, folder, password);
            server.Start();
            Log.Information($"正运行在：{port}端口，监视目录：{folder}");
            Console.ReadKey();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Error($"Unhandled exception: {e.ToString()}");
            Log.Error(e.ExceptionObject.ToString());
        }
    }
}
