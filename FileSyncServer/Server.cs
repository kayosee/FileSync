using FileSyncCommon.Tools;
using Serilog;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices.JavaScript;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace FileSyncServer
{
    public class Server
    {
        private ConcurrentDictionary<int, ServerSession> _sessions = new ConcurrentDictionary<int, ServerSession>();
        private Thread? _acceptor;
        private int _port;
        private string _folder;
        private string _password;
        private string _serverCert;
        private string _clientCert;
        private TcpListener? _listener;
        public Server(int port, string folder, string serverCert, string clientCert, string client, string password)
        {
            _port = port;
            _folder = folder;
            _serverCert = serverCert;
            _clientCert = clientCert;
            _password = password;
        }
        public int Port { get => _port; set => _port = value; }
        public string Folder { get => _folder; set => _folder = value; }
        public void Start()
        {
            X509Certificate2 serverCert = new X509Certificate2(_serverCert, _password,
                                            X509KeyStorageFlags.MachineKeySet |
                                            X509KeyStorageFlags.PersistKeySet |
                                            X509KeyStorageFlags.Exportable);
            _listener = new TcpListener(IPAddress.Any, Port);
            _listener.Start();

            _acceptor = new Thread(() =>
            {
                while (true)
                {
                    var client = _listener.AcceptTcpClient();
                    var ip = client.Client.RemoteEndPoint as IPEndPoint;
                    if (FailCounter.Get(ip) > ConfigReader.GetInt("maxFailCount", 3))
                    {
                        Log.Warning($"黑名单IP{client.Client.RemoteEndPoint},禁止连接");
                        client.Close();
                        continue;
                    }

                    client.ReceiveBufferSize = (int)Math.Pow(1024, 3);
                    Log.Information("新连接加入");
                    // 创建 SSL 流
                    SslStream sslStream = new SslStream(client.GetStream(), false);
                    try
                    {
                        var options = new SslServerAuthenticationOptions
                        {
                            ServerCertificate = serverCert,
                            ClientCertificateRequired = true,
                            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                            RemoteCertificateValidationCallback = ValidateCallback
                        };

                        var events = new ManualResetEvent(false);
                        var success = false;
                        // 服务器身份验证
                        ThreadPool.QueueUserWorkItem((f) =>
                        {
                            try
                            {
                                sslStream.AuthenticateAsServer(options);
                                success = true;
                            }
                            catch
                            {
                                success = false;
                            }
                            events.Set();
                        });

                        if (!events.WaitOne(TimeSpan.FromSeconds(ConfigReader.GetInt("timeout", 15))) || !success)
                            throw new TimeoutException("SSL authentication timed out.");

                        var clientId = _sessions.Count;
                        var session = new ServerSession(clientId, _folder, sslStream);
                        session.OnDisconnect += Session_OnDisconnect;
                        _sessions.TryAdd(clientId, session);
                    }
                    catch (Exception ex)
                    {
                        FailCounter.Increase(ip);
                        Log.Error($"SSL认证失败: {ex.Message}");
                        sslStream.Close();
                        sslStream.Dispose();
                        client.Close();
                    }
                }
            });
            _acceptor.Name = "acceptor";
            _acceptor.IsBackground = true;
            _acceptor.Start();
        }

        /// 验证客户端证书
        private bool ValidateCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            if (certificate is X509Certificate2 cert2)
            {
                try
                {
                    var clientCert = new X509Certificate2(_clientCert);
                    return cert2.GetRawCertData().SequenceEqual(clientCert.GetRawCertData());//验证证书内容是否一致
                }
                catch (Exception e)
                {
                    Log.Error($"验证服务器证书失败: {e.Message}", e);
                }
            }
            return false;
        }

        private void Session_OnDisconnect(ServerSession session)
        {
            if (_sessions.ContainsKey(session.Id))
                _sessions.Remove(session.Id, out var _);
        }

        public void Stop()
        {
            _listener?.Stop();
        }
    }
}
