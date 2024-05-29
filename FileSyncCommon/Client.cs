using System.Net;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using FileSyncCommon.Messages;
using FileSyncCommon.Messages;
using FileSyncCommon.Tools;

namespace FileSyncCommon
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
        private bool _encrypt;
        private byte _encryptKey;
        private string _password;
        private bool _authorized;
        private Timer _timer;
        private RequestCounter<long> _request = new RequestCounter<long>();
        private SocketSession _session;
        private Socket _socket;
        private volatile bool _running;
        private int _deleteDaysBefore;

        public delegate void FolderListResponseHandler(FolderListResponse response);
        public delegate void DisconnectedHandler();
        public delegate void LoginHandler();

        public event FolderListResponseHandler OnFolderListResponse;
        public event DisconnectedHandler OnDisconnected;
        public event LoginHandler OnLogin;
        public string Host { get => _host; set => _host = value; }
        public int Port { get => _port; set => _port = value; }
        public int ClientId { get => _clientId; set => _clientId = value; }
        public string LocalFolder { get => _localFolder; set => _localFolder = value; }
        public int Interval { get => _interval; set => _interval = value; }
        public string Password
        {
            get => _password; set
            {
                _password = value;
                _encrypt = !string.IsNullOrEmpty(_password);
                var bytes = System.Text.Encoding.UTF8.GetBytes(_password);
                _encryptKey = bytes.Aggregate((s, t) => s ^= t);
            }
        }
        public bool IsConnected
        {
            get
            {
                return _socket != null && _socket.Connected;
            }
        }
        public bool Encrypt { get => _encrypt; set => _encrypt = value; }
        public byte EncryptKey { get => _encryptKey; set => _encryptKey = value; }
        public string RemoteFolder { get => _remoteFolder; set => _remoteFolder = value; }
        public bool Running { get => _running; }
        public int SyncDaysBefore { get => _syncDaysBefore; set => _syncDaysBefore = value; }
        public int DeleteDaysBefore { get => _deleteDaysBefore; set => _deleteDaysBefore = value; }
        private void OnSendPackage(Messages.Message packet)
        {
            if (packet is Request request)
                _request.Increase(request.RequestId);
        }
        private void OnReceivePackage(Messages.Message packet)
        {
            if (packet != null)
            {
                switch (packet.MessageType)
                {
                    case MessageType.AuthenticateResponse:
                        DoAuthenticateResponse((AuthenticateResponse)packet);
                        break;
                    case MessageType.FileListInfoResponse:
                        DoFileListInfoResponse((FileListInfoResponse)packet);
                        break;
                    case MessageType.FileListDetailResponse:
                        DoFileListDetailResponse((FileListDetailResponse)packet);
                        break;
                    case MessageType.FileContentInfoResponse:
                        DoFileContentInfoResponse((FileContentInfoResponse)packet);
                        break;
                    case MessageType.FileContentDetailResponse:
                        DoFileContentDetailResponse((FileContentDetailResponse)packet);
                        break;
                    case MessageType.FolderListResponse:
                        DoFolderListResponse((FolderListResponse)packet);
                        break;
                }
            }
        }
        private void DoFolderListResponse(FolderListResponse packet)
        {
            if (OnFolderListResponse != null)
                OnFolderListResponse(packet);
        }
        private void DoAuthenticateResponse(AuthenticateResponse packet)
        {
            if (!packet.OK)
            {
                LogError("验证失败，连接断开");
                _session.Disconnect();
            }
            else
            {
                LogInformation("验证成功");
                this._clientId = packet.ClientId;
                this._authorized = true;
                OnLogin?.Invoke();
            }
        }
        private void DoFileContentInfoResponse(FileContentInfoResponse packet)
        {
            LogInformation($"发起请求文件信息: {packet.Path}");

            var response = new FileContentDetailRequest(_clientId, DateTime.Now.Ticks, packet.LastPos, packet.Path);
            _request.Increase(response.RequestId, packet.TotalCount);
            _request.Decrease(packet.RequestId);

            _session.SendMessage(response);
        }
        private void DoFileListInfoResponse(FileListInfoResponse packet)
        {
            _request.Increase(packet.RequestId, packet.FileCount);
        }
        private void DoFileContentDetailResponse(FileContentDetailResponse fileResponse)
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
                        _request.Remove(fileResponse.RequestId);
                        break;
                    }
                case FileResponseType.FileDeleted:
                    {
                        if (System.IO.Path.Exists(path))
                            File.Delete(path);
                        _request.Remove(fileResponse.RequestId);
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
                                _request.Remove(fileResponse.RequestId);
                                LogInformation($"{fileResponse.Path}已经传输完成。");
                            }
                            else //写入位置信息
                            {
                                FileOperator.WriteFile(path + ".sync", fileResponse.Pos, fileResponse.FileData, fileResponse.Pos + fileResponse.FileDataLength);
                                _session.SendMessage(new FileContentDetailRequest(_clientId, fileResponse.RequestId, fileResponse.Pos + fileResponse.FileDataLength, fileResponse.Path));
                                _request.Decrease(fileResponse.RequestId);
                            }
                        }
                        catch (Exception e)
                        {
                            LogError(e.Message, e);
                            _session.SendMessage(new FileContentDetailRequest(_clientId, fileResponse.RequestId, fileResponse.Pos, fileResponse.Path));
                        }
                        break;
                    }
                case FileResponseType.FileReadError:
                    {
                        LogError($"远程文件读取失败:{fileResponse.Path}", null);
                        _request.Remove(fileResponse.RequestId);
                        break;
                    }
                default:
                    break;
            }
        }
        private void DoFileListDetailResponse(FileListDetailResponse fileInformation)
        {
            var file = System.IO.Path.Combine(_localFolder, fileInformation.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
            var request = new FileContentInfoRequest(_clientId, DateTime.Now.Ticks, 0, 0, fileInformation.Path);

            var localFileInfo = new System.IO.FileInfo(file);
            if (!localFileInfo.Exists)
            {
                if (File.Exists(file + ".sync"))//需要检验
                {
                    try
                    {
                        var pos = FileOperator.GetLastPosition(file + ".sync");
                        request.Checksum = FileOperator.GetCrc32(file + ".sync", pos);
                        request.LastPos = pos;
                        _session.SendMessage(request);
                        _request.Increase(request.RequestId);
                        LogInformation($"正在检验续传文件:{fileInformation.Path}");
                    }
                    catch (Exception ex)
                    {
                        _session.SendMessage(request);
                        _request.Increase(request.RequestId);
                        LogInformation($"文件校验失败:{fileInformation.Path}，重新下载。");
                    }
                }
                else
                {
                    _session.SendMessage(request);
                    _request.Increase(request.RequestId);
                    LogInformation($"文件不存在:{fileInformation.Path}，发起下载。");
                }
            }
            else
            {
                if (localFileInfo.Length == fileInformation.FileLength && localFileInfo.LastWriteTime.Ticks == fileInformation.LastWriteTime)//请求CHECKSUM，看看是不是一样
                {
                    LogInformation($"{fileInformation.Path}文件一致，无须更新");

                    /*
                    var checksum = FileOperator.GetCrc32(localFileInfo.FullName);
                    if (checksum != fileInformation.Checksum)
                    {
                        SendPacket(request);
                        _request.Increase(request.RequestId);
                    }
                    else
                    {
                        Log.Information($"{fileInformation.Path}文件一致，无须更新");
                    }
                    */
                }
                else
                {
                    _session.SendMessage(request);
                    _request.Increase(request.RequestId);
                    LogInformation($"{fileInformation.Path}文件不一致，需要更新");
                }
            }
            _request.Decrease(fileInformation.RequestId);
        }
        protected void OnSocketError(SocketSession socketSession, Exception e)
        {
            Disconnect();
            while (!_socket.Connected)
            {
                Connect(_host, _port, _encrypt, _encryptKey, _password);
            }
        }
        protected void OnConnected()
        {
            _session = new SocketSession(_socket, _encrypt, _encryptKey);
            _session.OnReceivePackage += OnReceivePackage;
            _session.OnSendPackage += OnSendPackage;
            _session.OnSocketError += OnSocketError;
            var packet = new AuthenticateRequest(0, 0, _password);
            _session.SendMessage(packet);
        }
        public bool Reconnect()
        {
            return Connect(_host, _port, _encrypt, _encryptKey, _password);
        }
        public bool Connect(string host, int port, bool encrypt, byte encryptKey, string password)
        {
            try
            {
                _host = host;
                _port = port;
                _encrypt = encrypt;
                _encryptKey = encryptKey;
                _password = password;
                _socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                _socket.Connect(IPAddress.Parse(host), port);
                OnConnected();
                return true;
            }
            catch (Exception e)
            {
                LogError(e.Message, e);
                return false;
            }
        }
        public void Disconnect()
        {
            _request.Clear();
            _session.Disconnect();
            _authorized = false;
            OnDisconnected?.Invoke();
        }
        public void Start(string localFolder, string remoteFolder, int syncDaysBefore, int deleteDaysBefore, int interval)
        {
            if (!_authorized)
                throw new UnauthorizedAccessException("尚未登录成功");

            if (string.IsNullOrEmpty(remoteFolder))
                throw new ArgumentNullException(nameof(remoteFolder));

            _running = true;
            _localFolder = localFolder;
            _remoteFolder = remoteFolder;
            _syncDaysBefore = syncDaysBefore;
            _deleteDaysBefore = deleteDaysBefore;
            _interval = interval;
            _request.Clear();
            if (_timer != null)
                _timer.Dispose();

            _timer = new Timer((e) =>
            {
                if (!_running)
                    return;

                if (_deleteDaysBefore > 0)
                    FileOperator.DeleteOldFile(localFolder, DateTime.Now - TimeSpan.FromDays(_deleteDaysBefore));

                if (_request.IsEmpty && IsConnected)
                {
                    var packet = new FileListRequest(_clientId, DateTime.Now.Ticks, syncDaysBefore, _remoteFolder);
                    _request.Increase(packet.RequestId, 0);
                    _session.SendMessage(packet);

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
                _session.SendMessage(new FolderListRequest(_clientId, DateTime.Now.Ticks, root));
        }
        public void Dispose()
        {
            if (IsConnected)
            {
                Disconnect();
                _session.Disconnect();
                if (_timer != null)
                    _timer.Dispose();
            }
        }

    }
}
