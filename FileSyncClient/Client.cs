﻿using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FileSyncCommon;
using FileSyncCommon.Messages;
using FileSyncCommon.Tools;

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
        private bool _authorized;
        private Timer? _timer;
        private SocketSession _session;
        private Socket _socket;
        private volatile bool _running;
        private int _deleteDaysBefore;
        private String _startTime;
        private String _endTime;

        private String _startDate;
        private String _endDate;
        private DeferredLock _requests = new DeferredLock();
        public delegate void FolderListResponseHandler(FolderListResponse response);
        public delegate void DisconnectedHandler();
        public delegate void LoginHandler();
        public event FolderListResponseHandler? OnFolderListResponse;
        public event DisconnectedHandler? OnDisconnected;
        public event LoginHandler? OnLogin;

        public string Host { get => _host; set => _host = value; }
        public int Port { get => _port; set => _port = value; }
        public int ClientId { get => _clientId; set => _clientId = value; }
        public string LocalFolder { get => _localFolder; set => _localFolder = value; }
        public int Interval { get => _interval; set => _interval = value; }
        public String StartTime { get => _startTime; set => _startTime = value; }
        public String EndTime { get => _endTime; set => _endTime = value; }
        public String StartDate { get => _startDate; set => _startDate = value; }
        public String EndDate { get => _endDate; set => _endDate = value; }
        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                Encrypt = !string.IsNullOrEmpty(_password);
                if (Encrypt)
                    EncryptKey = Encoding.UTF8.GetBytes(_password);
            }
        }
        public bool IsConnected
        {
            get
            {
                return _socket != null && _socket.Connected;
            }
        }
        public bool Encrypt { get; private set; }
        public byte[] EncryptKey { get; private set; }
        public string RemoteFolder { get => _remoteFolder; set => _remoteFolder = value; }
        public bool Running { get => _running; }
        public int SyncDaysBefore { get => _syncDaysBefore; set => _syncDaysBefore = value; }
        public int DeleteDaysBefore { get => _deleteDaysBefore; set => _deleteDaysBefore = value; }

        private void OnSendPackage(Message message)
        {
        }
        private void OnReceivePackage(Message message)
        {
            if (message != null)
            {
                if (message is FileResponse)
                {
                    var response = (FileResponse)message;
                    if (response.Error != Error.None)
                    {
                        LogError($"请求响应错误：{Enum.GetName(response.Error)}");
                    }
                }
                switch (message.MessageType)
                {
                    case MessageType.AuthenticateResponse:
                        DoAuthenticateResponse((AuthenticateResponse)message);
                        break;
                    case MessageType.FileListTotalResponse:
                        DoFileListTotalResponse((FileListTotalResponse)message);
                        break;
                    case MessageType.FileListDetailResponse:
                        DoFileListDetailResponse((FileListDetailResponse)message);
                        break;
                    case MessageType.FileInfoResponse:
                        DoFileInfoResponse((FileInfoResponse)message);
                        break;
                    case MessageType.FileContentResponse:
                        DoFileContentResponse((FileContentResponse)message);
                        break;
                    case MessageType.FolderListResponse:
                        DoFolderListResponse((FolderListResponse)message);
                        break;
                }
            }
        }
        private void DoFolderListResponse(FolderListResponse message)
        {
            if (OnFolderListResponse != null)
                OnFolderListResponse(message);
        }
        private void DoAuthenticateResponse(AuthenticateResponse message)
        {
            if (!message.OK)
            {
                LogError("验证失败，连接断开");
                _session.Disconnect();
            }
            else
            {
                LogInformation("验证成功");
                this._clientId = message.ClientId;
                this._authorized = true;
                OnLogin?.Invoke();
            }
        }
        private void DoFileInfoResponse(FileInfoResponse message)
        {
            if (message.Error != Error.None)
            {
                return;
            }

            LogInformation($"发起请求文件信息: {message.Path}");

            var response = new FileContentRequest(_clientId, message.RequestId, message.LastPos, message.Path);
            _session.SendMessage(response);
        }
        private void DoFileListTotalResponse(FileListTotalResponse message)
        {
            var request = new FileListDetailRequest(_clientId, DateTime.Now.Ticks, _syncDaysBefore, _remoteFolder);
            _session.SendMessage(request);
        }
        private void DoFileContentResponse(FileContentResponse fileResponse)
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
                                    _requests.Release();
                                }
                                else //写入位置信息
                                {
                                    FileOperator.WriteFile(path + ".sync", fileResponse.Pos, fileResponse.FileData, fileResponse.Pos + fileResponse.FileDataLength);
                                    _session.SendMessage(new FileContentRequest(_clientId, fileResponse.RequestId, fileResponse.Pos + fileResponse.FileDataLength, fileResponse.Path));
                                }
                            }
                            catch (Exception e)
                            {
                                LogError(e.Message, e);
                                _session.SendMessage(new FileContentRequest(_clientId, fileResponse.RequestId, fileResponse.Pos, fileResponse.Path));
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
        }
        private void DoFileListDetailResponse(FileListDetailResponse fileInformation)
        {
            var file = System.IO.Path.Combine(_localFolder, fileInformation.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
            var request = new FileInfoRequest(_clientId, fileInformation.RequestId, 0, 0, fileInformation.Path);

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
                        _requests.TryAcquire(() =>
                        {
                            _session.SendMessage(request);
                            LogInformation($"正在检验续传文件:{fileInformation.Path}");
                        });

                    }
                    catch (Exception)
                    {
                        _requests.TryAcquire(() =>
                        {
                            _session.SendMessage(request);
                            LogInformation($"文件校验失败:{fileInformation.Path}，重新下载。");
                        });
                    }
                }
                else
                {
                    _requests.TryAcquire(() =>
                    {
                        _session.SendMessage(request);
                        LogInformation($"文件不存在:{fileInformation.Path}，发起下载。");
                    });
                }
            }
            else
            {
                if (localFileInfo.Length == fileInformation.FileLength && localFileInfo.LastWriteTime.Ticks == fileInformation.LastWriteTime)//请求CHECKSUM，看看是不是一样
                {
                    LogInformation($"{fileInformation.Path}文件一致，无须更新");
                }
                else
                {
                    _requests.TryAcquire(() =>
                    {
                        _session.SendMessage(request);
                        LogInformation($"{fileInformation.Path}文件不一致，需要更新");
                    });
                }
            }
        }
        protected void OnSocketError(SocketSession socketSession, Exception e)
        {
            LogError(e.Message, e);
            Disconnect();
            while (!_socket.Connected)
            {
                Connect(_host, _port, _password);
            }
        }
        protected void OnConnected()
        {
            _session = new SocketSession(_socket, Encrypt, EncryptKey);
            _session.OnReceivePackage += OnReceivePackage;
            _session.OnSendPackage += OnSendPackage;
            _session.OnSocketError += OnSocketError;
            var message = new AuthenticateRequest(0, 0, _password);
            _session.SendMessage(message);
        }
        public bool Reconnect()
        {
            return Connect(_host, _port, _password);
        }
        public bool Connect(string host, int port, string password)
        {
            try
            {
                _host = host;
                _port = port;
                Password = password;

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

                if (IsConnected&&_requests.IsEmpty())
                {
                    LogInformation("开始新同步");
                    var request = new FileListTotalRequest(_clientId, DateTime.Now.Ticks, syncDaysBefore, _remoteFolder);
                    _session.SendMessage(request);
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
