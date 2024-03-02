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
        private int _total;
        private ConcurrentDictionary<string, PacketFileRequest> _fileRequests;
        public Client(string ip, int port, string folder, bool encrypt, byte encryptKey) : base(0, new Socket(SocketType.Stream, ProtocolType.Tcp), encrypt, encryptKey)
        {
            _folder = folder;
            _port = port;
            _encrypt = encrypt;
            _encryptKey = encryptKey;
            _fileRequests = new ConcurrentDictionary<string, PacketFileRequest>();
            if (Connect(ip, port))
            {
                Log.Information($"已连接到服务器:{ip}:{port}");
            }
        }

        protected override void OnReceivePackage(Packet packet)
        {
            if (packet != null)
            {
                switch ((PacketType)packet.DataType)
                {
                    case PacketType.Handshake:
                        DoHandshake((PacketHandshake)packet);
                        break;
                    case PacketType.FileInformation:
                        DoFileInformation((PacketFileInfomation)packet);
                        break;
                    case PacketType.FileResponse:
                        DoFileResponse((PacketFileResponse)packet);
                        break;
                }
            }
        }
        private void DoFileResponse(PacketFileResponse fileResponse)
        {
            //Log.Information($"{++_total}");

            var path = System.IO.Path.Combine(_folder, fileResponse.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
            switch ((FileResponseType)fileResponse.ResponseType)
            {
                case FileResponseType.Empty:
                    {
                        File.Create(path).Close();
                        break;
                    }
                case FileResponseType.FileDeleted:
                    {
                        if (System.IO.Path.Exists(path))
                        {
                            File.Delete(path);
                        }
                        break;
                    }
                case FileResponseType.Content:
                    {
                        //Log.Information($"收到文件'{path}',位置:{fileResponse.Pos},长度:{fileResponse.FileData.Length},总长:{fileResponse.FileDataTotal}");
                        try
                        {
                            FileOperator.WriteFile(path + ".sync", fileResponse.Pos, fileResponse.FileData);

                            if (fileResponse.Pos + fileResponse.FileDataLength >= fileResponse.FileDataTotal) //文件已经传输完成
                            {
                                FileOperator.SetupFile(path, fileResponse.LastWriteTime);
                                _fileRequests.Remove(fileResponse.Path, out var _);
                                if(_fileRequests.Count <= 0)
                                {
                                    SendPacket(new PacketFileInquire(_clientId, _folder));
                                }
                            }
                            else //写入位置信息
                            {
                                FileOperator.AppendFile(path + ".sync", new FilePosition(fileResponse.Pos).GetBytes());
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error(e.Message);
                            Log.Error(e.StackTrace);
                        }
                        break;
                    }
                case FileResponseType.FileReadError:
                    {
                        Log.Error($"远程文件读取失败:{fileResponse.Path}");
                        break;
                    }
                default:
                    break;

            }
        }

        private void DoFileInformation(PacketFileInfomation fileInformation)
        {
            var file = System.IO.Path.Combine(_folder, fileInformation.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
            var request = new PacketFileRequest(_clientId, 0, fileInformation.Path);

            var localFileInfo = new System.IO.FileInfo(file);
            if (!localFileInfo.Exists)
            {
                if (File.Exists(file + ".sync"))
                {
                    var pos = FileOperator.GetLastPosition(file + ".sync");
                    request.StartPos = pos;
                    _fileRequests.TryAdd(fileInformation.Path, request);
                    SendPacket(request);
                }
                else
                {
                    _fileRequests.TryAdd(fileInformation.Path, request);
                    SendPacket(request);
                }
            }
            else
            {
                if (localFileInfo.Length == fileInformation.FileLength && localFileInfo.LastWriteTime.Ticks == fileInformation.LastWriteTime)//请求CHECKSUM，看看是不是一样
                {
                    var checksum = FileOperator.GetCrc32(localFileInfo.FullName);
                    if (checksum != fileInformation.Checksum)
                    {
                        _fileRequests.TryAdd(fileInformation.Path, request);
                        SendPacket(request);
                    }
                    else
                    {
                        //Log.Information($"{fileInformation.Path}文件一致，无须更新");
                    }
                }
                else
                {
                    _fileRequests.TryAdd(fileInformation.Path, request);
                    SendPacket(request);
                }
            }

        }

        private void DoHandshake(PacketHandshake handshake)
        {
            this._clientId = handshake.ClientId;
            SendPacket(new PacketFileInquire(_clientId, _folder));
        }

        protected override void OnSocketError(int id, Socket socket, Exception e)
        {
            throw new NotImplementedException();
        }

        protected override void OnConnected()
        {
        }
    }
}
