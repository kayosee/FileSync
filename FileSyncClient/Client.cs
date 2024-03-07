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

namespace FileSyncClient
{
    public class Client : SocketSession
    {
        private int _clientId;
        private string _folder;
        private int _port;
        private bool _encrypt;
        private byte _encryptKey;

        private Timer _timer;
        private RequestCounter<long> _request = new RequestCounter<long>();
        public Client(string ip, int port, string folder, bool encrypt, byte encryptKey) : base(0, new Socket(SocketType.Stream, ProtocolType.Tcp), encrypt, encryptKey)
        {
            _folder = folder;
            _port = port;
            _encrypt = encrypt;
            _encryptKey = encryptKey;
            if (Connect(ip, port))
            {
                Log.Information($"已连接到服务器:{ip}:{port}");
            }
        }
        protected override void OnReceivePackage(Packet packet)
        {
            if (packet != null)
            {
                switch (packet.DataType)
                {
                    case PacketType.Handshake:
                        DoHandshake((PacketHandshake)packet);
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
        private void DoFileContentInfoResponse(PacketFileContentInfoResponse packet)
        {
            var response = new PacketFileContentDetailRequest(_clientId, DateTime.Now.Ticks, packet.LastPos, packet.Path);
            _request.Increase(response.RequestId, packet.TotalCount);
            _request.Decrease(packet.RequestId);

            SendPacket(response);
        }
        private void DoFileListInfoResponse(PacketFileListInfoResponse packet)
        {
            _request.Increase(packet.RequestId, packet.FileCount);
        }
        private void DoFileContentDetailResponse(PacketFileContentDetailResponse fileResponse)
        {
            var path = System.IO.Path.Combine(_folder, fileResponse.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
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
                                Log.Information($"{path}已经传输完成。");
                            }
                            else //写入位置信息
                            {
                                FileOperator.WriteFile(path + ".sync", fileResponse.Pos, fileResponse.FileData, fileResponse.Pos + fileResponse.FileDataLength);
                                SendPacket(new PacketFileContentDetailRequest(_clientId, fileResponse.RequestId, fileResponse.Pos + fileResponse.FileDataLength, fileResponse.Path));
                                _request.Decrease(fileResponse.RequestId);
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error(e.Message);
                            Log.Error(e.StackTrace);
                            SendPacket(new PacketFileContentDetailRequest(_clientId, fileResponse.RequestId, fileResponse.Pos, fileResponse.Path));
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
            var file = System.IO.Path.Combine(_folder, fileInformation.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
            var request = new PacketFileContentInfoRequest(_clientId, DateTime.Now.Ticks, 0, 0, fileInformation.Path);

            var localFileInfo = new System.IO.FileInfo(file);
            if (!localFileInfo.Exists)
            {
                if (File.Exists(file + ".sync"))//需要检验
                {
                    try
                    {
                        var pos = FileOperator.GetLastPosition(file + ".sync");
                        request.Checksum = FileOperator.GetCrc32(file + ".sync",pos).GetValueOrDefault();
                        request.LastPos = pos;
                        SendPacket(request);
                        _request.Increase(request.RequestId);
                        Log.Information($"正在检验续传文件:{fileInformation.Path}");
                    }
                    catch (Exception ex)
                    {
                        SendPacket(request);
                        _request.Increase(request.RequestId);
                        Log.Information($"文件校验失败:{fileInformation.Path}，重新下载。");
                    }
                }
                else
                {
                    SendPacket(request);
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
                    SendPacket(request);
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
                        var packet = new PacketFileListRequest(_clientId, DateTime.Now.Ticks, _folder);
                        _request.Increase(packet.RequestId, 0);
                        SendPacket(packet);
                    }
                });
                _timer.Change(TimeSpan.FromMinutes(0.3), TimeSpan.FromMinutes(0.3));
            }
        }
        protected override void OnSocketError(int id, Socket socket, Exception e)
        {
            while(!socket.Connected)
            {
                Reconnect();
            }
        }
        protected override void OnConnected()
        {
        }
    }
}
