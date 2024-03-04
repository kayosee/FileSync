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
        private ConcurrentDictionary<long, long> _inquire = new ConcurrentDictionary<long, long>();
        private ConcurrentDictionary<long, long> _request = new ConcurrentDictionary<long, long>();
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
                    case PacketType.FileTotalInfo:
                        DoFileTotalInfo((PacketFileListInfoResponse)packet);
                        break;
                    case PacketType.FileDetailInfo:
                        DoFileDetailInfo((PacketFileListDetailResponse)packet);
                        break;
                    case PacketType.FileResponseInfo:
                        DoFileResponseInfo((PacketFileContentInfoResponse)packet);
                        break;
                    case PacketType.FileResponseDetail:
                        DoFileResponseDetail((PacketFileContentDetailResponse)packet);
                        break;
                }
            }
        }

        private void DoFileResponseInfo(PacketFileContentInfoResponse packet)
        {
            _request.TryAdd(packet.RequestId, packet.TotalCount);
        }

        private void DoFileTotalInfo(PacketFileListInfoResponse packet)
        {
            _inquire.TryAdd(packet.InquireId, packet.FileCount);
        }

        private void RemoveFromQueue(long requestId)
        {
            if (_request.GetOrAdd(requestId, 0) > 0)
                _request[requestId]--;
        }
        private void DoFileResponseDetail(PacketFileContentDetailResponse fileResponse)
        {
            var path = System.IO.Path.Combine(_folder, fileResponse.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
            switch (fileResponse.ResponseType)
            {
                case FileResponseType.Empty:
                    {
                        File.Create(path).Close();
                        FileInfo fi = new FileInfo(path);
                        fi.LastWriteTime = DateTime.FromBinary(fileResponse.LastWriteTime);
                        RemoveFromQueue(fileResponse.RequestId);
                        break;
                    }
                case FileResponseType.FileDeleted:
                    {
                        if (System.IO.Path.Exists(path))
                        {
                            File.Delete(path);
                        }
                        RemoveFromQueue(fileResponse.RequestId);
                        break;
                    }
                case FileResponseType.Content:
                    {
                        //Log.Information($"收到文件'{path}',位置:{fileResponse.Pos},长度:{fileResponse.FileData.Length},总长:{fileResponse.FileDataTotal}");
                        try
                        {
                            FileOperator.WriteFile(path + ".sync", fileResponse.Pos, fileResponse.FileData);

                            if (fileResponse.EndOfFile) //文件已经传输完成
                            {
                                FileOperator.SetupFile(path, fileResponse.LastWriteTime);
                                RemoveFromQueue(fileResponse.RequestId);
                            }
                            else //写入位置信息
                            {
                                FileOperator.AppendFile(path + ".sync", new FilePosition(fileResponse.Pos).GetBytes());
                                SendPacket(new PacketFileContentDetailRequest(_clientId, fileResponse.InquireId, fileResponse.RequestId, fileResponse.Pos + fileResponse.FileDataLength, fileResponse.Path));
                            }
                        }
                        catch (FileChecksumException e)
                        {
                            SendPacket(new PacketFileContentDetailRequest(_clientId, fileResponse.InquireId, fileResponse.RequestId, fileResponse.Pos, fileResponse.Path));
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
                        RemoveFromQueue(fileResponse.RequestId);
                        break;
                    }
                default:
                    break;

            }
        }

        private void DoFileDetailInfo(PacketFileListDetailResponse fileInformation)
        {
            if (_inquire.GetOrAdd(fileInformation.InquireId, 0) > 0)
                _inquire[fileInformation.InquireId]--;

            var file = System.IO.Path.Combine(_folder, fileInformation.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
            var request = new PacketFileContentDetailRequest(_clientId, fileInformation.InquireId, DateTime.Now.Ticks, 0, fileInformation.Path);

            var localFileInfo = new System.IO.FileInfo(file);
            if (!localFileInfo.Exists)
            {
                if (File.Exists(file + ".sync"))
                {
                    var pos = FileOperator.GetLastPosition(file + ".sync");
                    request.StartPos = pos;
                    SendPacket(request);
                }
                else
                {
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
                        SendPacket(request);
                    }
                    else
                    {
                        //Log.Information($"{fileInformation.Path}文件一致，无须更新");
                    }
                }
                else
                {
                    SendPacket(request);
                }
            }
        }

        private void DoHandshake(PacketHandshake handshake)
        {
            this._clientId = handshake.ClientId;

            _timer = new Timer((e) =>
            {
                if (_inquire.Count == 0 && _request.Count == 0)
                    SendPacket(new PacketFileListRequest(_clientId, DateTime.Now.Ticks, _folder));
            });
            _timer.Change(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
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
