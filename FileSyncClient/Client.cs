using FileSyncCommon;
using FileSyncCommon.Messages;
using FileSyncCommon.Tools;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.ConstrainedExecution;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace FileSyncClient
{
    public class Client : Logging, IDisposable
    {
        private string _host;
        private int _port;
        private int _clientId;
        private string _localFolder;
        private string _remoteFolder;
        private int _syncDaysBefore;
        private int _interval;
        private string _password;
        private Timer? _timer;
        private StreamPacker _packer;
        private TcpClient _client;
        private volatile bool _running;
        private int _deleteDaysBefore;
        private string _startTime;
        private string _endTime;

        private string _startDate;
        private string _endDate;
        //private ConcurrentQueue<Message> _messageQueue = new ConcurrentQueue<Message>();
        private UnquantifiedSignal _semaphore = new UnquantifiedSignal(1);
        private RequestQueue _syncQueue = new RequestQueue();
        public delegate void FolderListResponseHandler(FolderListResponse response);
        public delegate void DisconnectedHandler();
        public delegate void ConnectedHandler();
        public event FolderListResponseHandler? OnFolderListResponse;
        public event DisconnectedHandler? OnDisconnected;
        public event ConnectedHandler? OnConnected;

        public string Host { get => _host; set => _host = value; }
        public int Port { get => _port; set => _port = value; }
        public int ClientId { get => _clientId; set => _clientId = value; }
        public string LocalFolder { get => _localFolder; set => _localFolder = value; }
        public int Interval { get => _interval; set => _interval = value; }
        public string StartTime { get => _startTime; set => _startTime = value; }
        public string EndTime { get => _endTime; set => _endTime = value; }
        public string StartDate { get => _startDate; set => _startDate = value; }
        public string EndDate { get => _endDate; set => _endDate = value; }
        private string _clientCert;
        private string _serverCert;
        public string ClientCert
        {
            get => _clientCert;
            set
            {
                _clientCert = value;
            }
        }
        public string ServerCert
        {
            get => _serverCert;
            set
            {
                _serverCert = value;
            }
        }
        public string Password
        {
            get => _password;
            set
            {
                _password = value;
            }
        }
        public bool IsConnected
        {
            get
            {
                return _client != null && _client.Connected;
            }
        }
        public string RemoteFolder { get => _remoteFolder; set => _remoteFolder = value; }
        public bool Running { get => _running; }
        public int SyncDaysBefore { get => _syncDaysBefore; set => _syncDaysBefore = value; }
        public int DeleteDaysBefore { get => _deleteDaysBefore; set => _deleteDaysBefore = value; }

        private void OnSendPackage(Message message)
        {
            //_messageQueue.Enqueue(message);
            _semaphore.Acquire();
        }
        private void OnReceivePackage(Message message)
        {
            if (message != null)
            {
                //_messageQueue.Enqueue(message);

                if (message is FileResponse fileResponse)
                {
                    if (fileResponse.Error != Error.None)
                    {
                        LogError($"请求响应错误：{Enum.GetName(fileResponse.Error)}");
                    }
                }
                IEnumerable<ClientMessage> requests = null;
                switch (message.MessageType)
                {
                    case MessageType.FileListTotalResponse:
                        requests = DoFileListTotalResponse((FileListTotalResponse)message);
                        break;
                    case MessageType.FileListDetailResponse:
                        requests = DoFileListDetailResponse((FileListDetailResponse)message);
                        break;
                    case MessageType.FileInfoResponse:
                        requests = DoFileInfoResponse((FileInfoResponse)message);
                        break;
                    case MessageType.FileContentResponse:
                        requests = DoFileContentResponse((FileContentResponse)message);
                        break;
                    case MessageType.FolderListResponse:
                        requests = DoFolderListResponse((FolderListResponse)message);
                        break;
                }

                _semaphore.Release();//先处理完请求

                if (requests != null)
                {
                    foreach (var request in requests)
                    {
                        if (request.Enqueue)//延迟发送，实现排队下载
                        {
                            _syncQueue.Enqueue(() =>
                            {
                                _packer.SendMessage(request.Message);
                            });
                        }
                        else
                            _packer.SendMessage(request.Message);
                    }
                }
            }
        }
        private IEnumerable<ClientMessage> DoFolderListResponse(FolderListResponse message)
        {
            if (OnFolderListResponse != null)
                OnFolderListResponse(message);

            return Array.Empty<ClientMessage>();
        }
        private IEnumerable<ClientMessage> DoFileInfoResponse(FileInfoResponse message)
        {
            if (message.Error != Error.None)
            {
                return Array.Empty<ClientMessage>();
            }

            LogInformation($"发起请求文件信息: {message.Path}");
            return [new ClientMessage(new FileContentRequest(_clientId, message.RequestId, message.LastPos, message.Path))];
        }
        private IEnumerable<ClientMessage> DoFileListTotalResponse(FileListTotalResponse message)
        {
            return [new ClientMessage(new FileListDetailRequest(_clientId, DateTime.Now.Ticks, _syncDaysBefore, _remoteFolder))];
        }
        private IEnumerable<ClientMessage> DoFileContentResponse(FileContentResponse fileResponse)
        {
            try
            {
                var path = System.IO.Path.Combine(_localFolder, fileResponse.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
                switch (fileResponse.ResponseType)
                {
                    case FileResponseType.Empty:
                        {
                            FileInfo fi = new FileInfo(path);
                            if (!fi.Directory.Exists)
                                fi.Directory.Create();

                            File.Create(path).Close();
                            fi.LastWriteTime = DateTime.FromBinary(fileResponse.LastWriteTime);
                            break;
                        }
                    case FileResponseType.FileDeleted:
                        {
                            if (System.IO.Path.Exists(path))
                                File.Delete(path);
                            break;
                        }
                    case FileResponseType.Content:
                        {
                            try
                            {
                                if (fileResponse.EndOfFile) //文件已经传输完成
                                {
                                    FileOperator.WriteFile(path + ".sync", fileResponse.Pos, fileResponse.FileData, null);
                                    FileOperator.SetupFile(path, fileResponse.LastWriteTime);
                                    LogInformation($"{fileResponse.Path}已经传输完成。");
                                    _syncQueue.Dequeue();
                                }
                                else //写入位置信息
                                {
                                    FileOperator.WriteFile(path + ".sync", fileResponse.Pos, fileResponse.FileData, fileResponse.Pos + fileResponse.FileDataLength);
                                    return [new ClientMessage(new FileContentRequest(_clientId, fileResponse.RequestId, fileResponse.Pos + fileResponse.FileDataLength, fileResponse.Path))];
                                }
                            }
                            catch (Exception e)
                            {
                                LogError(e.Message, e);
                                return [new ClientMessage(new FileContentRequest(_clientId, fileResponse.RequestId, fileResponse.Pos, fileResponse.Path))];
                            }
                            break;
                        }
                    case FileResponseType.FileReadError:
                        {
                            LogError($"远程文件读取失败:{fileResponse.Path}", null);
                            break;
                        }
                    default:
                        break;
                }
            }
            catch (Exception e)
            {
                LogError(e.Message, e);
            }
            return new ClientMessage[0];
        }
        private IEnumerable<ClientMessage> DoFileListDetailResponse(FileListDetailResponse fileInformation)
        {
            var list = new List<ClientMessage>();
            foreach (var fileInfo in fileInformation.List)
            {
                var file = Path.Combine(_localFolder, fileInfo.Path.TrimStart(Path.DirectorySeparatorChar));
                var request = new FileInfoRequest(_clientId, fileInformation.RequestId, 0, 0, fileInfo.Path);
                var localFileInfo = new FileInfo(file);
                if (!localFileInfo.Exists)
                {
                    if (File.Exists(file + ".sync"))//需要检验
                    {
                        try
                        {
                            LogInformation($"正在检验续传文件:{fileInfo.Path}");
                            var pos = FileOperator.GetLastPosition(file + ".sync");
                            request.Checksum = FileOperator.GetCrc32(file + ".sync", pos);
                            request.LastPos = pos;
                            list.Add(new ClientMessage(request, true));
                        }
                        catch (Exception)
                        {
                            LogInformation($"文件校验失败:{fileInfo.Path}，重新下载。");
                            list.Add(new ClientMessage(request, true));
                        }
                    }
                    else
                    {
                        LogInformation($"文件不存在:{fileInfo.Path}，发起下载。");
                        list.Add(new ClientMessage(request, true));
                    }
                }
                else
                {
                    if (localFileInfo.Length == fileInfo.FileLength && localFileInfo.LastWriteTime.Ticks == fileInfo.LastWriteTime)//请求CHECKSUM，看看是不是一样
                    {
                        LogInformation($"{fileInfo.Path}文件一致，无须更新");
                    }
                    else
                    {
                        LogInformation($"{fileInfo.Path}文件不一致，需要更新");
                        list.Add(new ClientMessage(request, true));
                    }
                }
            }
            return list;
        }
        protected void OnStreamError(StreamPacker session, Exception e)
        {
            LogError(e.Message, e);
            Disconnect();
            while (!_client.Connected)
            {
                Connect(_host, _port,_clientCert,_serverCert, _password);
            }
        }
        protected void Connected(SslStream stream)
        {
            _packer = new StreamPacker(stream);
            _packer.OnReceivePackage += OnReceivePackage;
            _packer.OnSendPackage += OnSendPackage;
            _packer.OnStreamError += OnStreamError;
            OnConnected?.Invoke();
        }
        public bool Reconnect()
        {
            return Connect(_host, _port, _clientCert, _serverCert, _password);
        }
        public bool Connect(string host, int port, string clientCert,string serverCert, string password)
        {
            try
            {
                _host = host;
                _port = port;
                _clientCert = clientCert;
                _serverCert = serverCert;
                _password = password;
                _syncQueue.Clear();

                _client = new TcpClient(host, port);
                _client.ReceiveBufferSize = (int)Math.Pow(1024, 3);
                SslStream sslStream = new(_client.GetStream(), false,
                                    new RemoteCertificateValidationCallback(ValidateServerCert), null);
                var options = new SslClientAuthenticationOptions
                {
                    TargetHost = host,
                    ClientCertificates = new X509CertificateCollection { new X509Certificate2(clientCert, password) },
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                };

                // 客户端身份验证
                sslStream.AuthenticateAsClient(options);
                Connected(sslStream);
                return true;
            }
            catch (Exception e)
            {
                LogError(e.Message, e);
                _client?.Close();               
                return false;
            }
        }
        /// 验证服务器证书
        private bool ValidateServerCert(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            if (certificate is X509Certificate2 cert2)
            {
                try
                {
                    var serverCert = new X509Certificate2(_serverCert);
                    return cert2.GetRawCertData().SequenceEqual(serverCert.GetRawCertData());//验证证书内容是否一致
                }
                catch (Exception e)
                {
                    LogError($"验证客户端证书失败: {e.Message}", e);
                }
            }
            return false;
        }

        public void Disconnect()
        {
            _packer.Dispose();
            _client.Close();
            OnDisconnected?.Invoke();
        }
        public void Start(string localFolder, string remoteFolder, int syncDaysBefore, int deleteDaysBefore, int interval)
        {
            if (string.IsNullOrEmpty(remoteFolder))
                throw new ArgumentNullException(nameof(remoteFolder));

            _running = true;
            _localFolder = localFolder;
            _remoteFolder = remoteFolder;
            _syncDaysBefore = syncDaysBefore;
            _deleteDaysBefore = deleteDaysBefore;
            _interval = interval;
            if (_timer != null)
                _timer.Dispose();

            _timer = new Timer((e) =>
            {
                var now = DateTime.Now;
                var start = now.Date;
                var end = now.Date;
                var timeSet = false;

                if (!string.IsNullOrEmpty(_startDate) && DateTime.TryParse(_startDate, out var startDate))
                {
                    start = startDate.Date;
                    timeSet = true;
                }
                if (!string.IsNullOrEmpty(_endDate) && DateTime.TryParse(_endDate, out var endDate))
                {
                    end = endDate.Date;
                    timeSet = true;
                }

                if (start > end)
                {
                    LogError($"开始日期{_startDate}大于结束日期{_endDate}，请检查配置");
                    return;
                }

                if (!string.IsNullOrEmpty(_startTime) && DateTime.TryParse(_startTime, out var startTime))
                {
                    start = start.Add(startTime.TimeOfDay);
                    timeSet = true;
                }

                if (!string.IsNullOrEmpty(_endTime) && DateTime.TryParse(_endTime, out var endTime))
                {
                    end = end.Add(endTime.TimeOfDay);
                    timeSet = true;
                }

                if (start.TimeOfDay > end.TimeOfDay)
                {
                    end = end.AddDays(1); //如果开始时间大于结束时间，说明跨天了
                }

                if (timeSet)
                    LogInformation($"同步时间范围: {start} - {end}");

                if (timeSet && (now < start || now > end))
                {
                    LogInformation($"当前时间{now}不在同步时间范围内({start} - {end})，跳过本次同步");
                    return;
                }

                if (!_running)
                    return;

                if (_deleteDaysBefore > 0)
                    FileOperator.DeleteOldFile(localFolder, DateTime.Now - TimeSpan.FromDays(_deleteDaysBefore));

                if (IsConnected && _syncQueue.IsEmpty() && _semaphore.Wait(TimeSpan.FromSeconds(15)))
                {
                    LogInformation("开始新同步");
                    var request = new FileListTotalRequest(_clientId, DateTime.Now.Ticks, syncDaysBefore, _remoteFolder);
                    _packer.SendMessage(request);
                }
            }, null, 0, (int)TimeSpan.FromMinutes(_interval).TotalMilliseconds);
        }
        public void Pause()
        {
            _running = false;
        }
        public void QueryFolders(string root)
        {
            if (IsConnected)
                _packer.SendMessage(new FolderListRequest(_clientId, DateTime.Now.Ticks, root));
        }
        public void Dispose()
        {
            if (IsConnected)
            {
                Disconnect();
                _packer.Dispose();
                if (_timer != null)
                    _timer.Dispose();
            }
        }
    }
}
