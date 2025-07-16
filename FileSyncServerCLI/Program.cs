using FileSyncCommon.Tools;
using FileSyncServer;
using Serilog;
using System.Configuration;

namespace FileSyncServerCLI
{
    internal class Program
    {
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            var folder = ConfigReader.GetString("folder", "\\");
            var client = ConfigReader.GetString("client", "");
            var port = ConfigReader.GetInt("port", 2020);
            var password = ConfigReader.GetString("password", "");
            var certificate = ConfigReader.GetString("certificate", "");
            Log.Logger = new LoggerConfiguration().WriteTo.Console().WriteTo.File("run.log", rollingInterval: RollingInterval.Day).CreateLogger();
            Server server = new Server(port, folder, certificate,client, password);
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
