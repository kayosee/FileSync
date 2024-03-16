using FileSyncCommon;
using Force.Crc32;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon
{
    public class Client 
    {
        private string _host;
        private int _port;
        private int _clientId;
        private string _localFolder;
        private string _remoteFolder;
        private int _interval;
        private bool _encrypt;
        private byte _encryptKey;
        private string _password;
        private Timer _timer;
        private RequestCounter<long> _request = new RequestCounter<long>();
        private SocketSession _session;
        private Socket _socket;
        public string Host { get => _host; set => _host = value; }
        public int Port { get => _port; set => _port = value; }
        public int ClientId { get => _clientId; set => _clientId = value; }
        public string LocalFolder { get => _localFolder; set => _localFolder = value; }
        public int Interval { get => _interval; set => _interval = value; }
        public string Password { get => _password; set => _password = value; }
        public bool IsConnected
        {
            get
            {
                return _socket != null && _socket.Connected;
            }
        }
        public bool IsRunning { get { return _session != null && _session.IsRunning; } }
        public bool Encrypt { get => _encrypt; set => _encrypt = value; }
        public byte EncryptKey { get => _encryptKey; set => _encryptKey = value; }
        public string RemoteFolder { get => _remoteFolder; set => _remoteFolder = value; }

        public Client()
        {
        }
        public Client(string localFolder, string remoteFolder, int interval)
        {
            _localFolder = localFolder;
            _remoteFolder = remoteFolder;
            _interval = interval;
        }
        private void OnReceivePackage(Packet packet)
        {
            if (packet != null)
            {
                switch (packet.DataType)
                {
                    case PacketType.Handshake:
                        DoHandshake((PacketHandshake)packet);
                        break;
                    case PacketType.AuthenticateResponse:
                        DoAuthenticateResponse((PacketAuthenticateResponse)packet); 
                        break;
                    case PacketType.FileListInfoResponse:
                        DoFileListInfoResponse((PacketFileListInfoResponse)packet);
                        break;
                    case PacketType.FileListDetailResponse:
                        DoFileListDetailResponse((PacketFileListDetailResponse)packet);
                        break;
                    case PacketType.FileContentInfoResponse:
                        DoFileContentInfoResponse((PacketFileContentInfoResponse)packet);
                        break;
                    case PacketType.FileContentDetailResponse:
                        DoFileContentDetailResponse((PacketFileContentDetailResponse)packet);
                        break;
                }
            }
        }
        private void DoAuthenticateResponse(PacketAuthenticateResponse packet)
        {
            if(!packet.OK)
            {
                Log.Error("验证失败，连接断开");
                _session.StopMessageLoop();
            }
            else
            {
                Log.Information("验证成功");
            }
        }
        private void DoFileContentInfoResponse(PacketFileContentInfoResponse packet)
        {
            Log.Information($"发起请求文件信息: {packet.Path}");

            var response = new PacketFileContentDetailRequest(_clientId, DateTime.Now.Ticks, packet.LastPos, packet.Path);
            _request.Increase(response.RequestId, packet.TotalCount);
            _request.Decrease(packet.RequestId);

            _session.SendPacket(response);
        }
        private void DoFileListInfoResponse(PacketFileListInfoResponse packet)
        {
            _request.Increase(packet.RequestId, packet.FileCount);
        }
        private void DoFileContentDetailResponse(PacketFileContentDetailResponse fileResponse)
        {
            var path = System.IO.Path.Combine(_localFolder, fileResponse.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
            switch (fileResponse.ResponseType)
            {
                case FileResponseType.Empty:
                    {
                        File.Create(path).Close();
                        FileInfo fi = new FileInfo(path);
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
                                Log.Information($"{fileResponse.Path}已经传输完成。");
                            }
                            else //写入位置信息
                            {
                                FileOperator.WriteFile(path + ".sync", fileResponse.Pos, fileResponse.FileData, fileResponse.Pos + fileResponse.FileDataLength);
                                _session.SendPacket(new PacketFileContentDetailRequest(_clientId, fileResponse.RequestId, fileResponse.Pos + fileResponse.FileDataLength, fileResponse.Path));
                                _request.Decrease(fileResponse.RequestId);
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error(e.Message);
                            Log.Error(e.StackTrace);
                            _session.SendPacket(new PacketFileContentDetailRequest(_clientId, fileResponse.RequestId, fileResponse.Pos, fileResponse.Path));
                        }
                        break;
                    }
                case FileResponseType.FileReadError:
                    {
                        Log.Error($"远程文件读取失败:{fileResponse.Path}");
                        _request.Remove(fileResponse.RequestId);
                        break;
                    }
                default:
                    break;
            }
        }
        private void DoFileListDetailResponse(PacketFileListDetailResponse fileInformation)
        {
            var file = System.IO.Path.Combine(_localFolder, fileInformation.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
            var request = new PacketFileContentInfoRequest(_clientId, DateTime.Now.Ticks, 0, 0, fileInformation.Path);

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
                        _session.SendPacket(request);
                        _request.Increase(request.RequestId);
                        Log.Information($"正在检验续传文件:{fileInformation.Path}");
                    }
                    catch (Exception ex)
                    {
                        _session.SendPacket(request);
                        _request.Increase(request.RequestId);
                        Log.Information($"文件校验失败:{fileInformation.Path}，重新下载。");
                    }
                }
                else
                {
                    _session.SendPacket(request);
                    _request.Increase(request.RequestId);
                    Log.Information($"文件不存在:{fileInformation.Path}，发起下载。");
                }
            }
            else
            {
                if (localFileInfo.Length == fileInformation.FileLength && localFileInfo.LastWriteTime.Ticks == fileInformation.LastWriteTime)//请求CHECKSUM，看看是不是一样
                {
                    Log.Information($"{fileInformation.Path}文件一致，无须更新");

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
                    _session.SendPacket(request);
                    _request.Increase(request.RequestId);
                    Log.Information($"{fileInformation.Path}文件不一致，需要更新");
                }
            }
            _request.Decrease(fileInformation.RequestId);
        }
        private void DoHandshake(PacketHandshake handshake)
        {
            this._clientId = handshake.ClientId;
            if (_timer == null)
            {
                _timer = new Timer((e) =>
                {
                    if (_request.IsEmpty)
                    {
                        var packet = new PacketFileListRequest(_clientId, DateTime.Now.Ticks, _remoteFolder);
                        _request.Increase(packet.RequestId, 0);
                        _session.SendPacket(packet);
                    }
                });
                _timer.Change(TimeSpan.FromMinutes(_interval), TimeSpan.FromMinutes(_interval));
            }
        }
        protected void OnSocketError(int id, Exception e)
        {
            while (!_socket.Connected)
            {
                Connect(_host, _port, _encrypt, _encryptKey, _password);
            }
            _request.Clear();
        }
        protected void OnConnected()
        {
            _session = new SocketSession(_clientId, _socket, _encrypt, _encryptKey);
            _session.OnReceivePackage += OnReceivePackage;
            _session.OnSocketError += OnSocketError;
            _session.StartMessageLoop();
            var packet = new PacketAuthenticateRequest(0, 0, _password);
            _session.SendPacket(packet);
        }
        public bool Connect()
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
                Log.Error(e.Message);
                Log.Error(e.StackTrace);
                return false;
            }
        }
        public void Disconnect()
        {
            if(IsConnected) {
                _session.Disconnect();
            }
        }
    }
}
