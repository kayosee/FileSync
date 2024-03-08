﻿using FileSyncCommon;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncServer
{
    public class ServerSession : SocketSession
    {
        private string _folder;
        private int _id;
        private bool _disconnected;
        private int _daysBefore;
        private int _total;
        public ServerSession(int id, string folder, int daysBefore, Socket socket, bool encrypt, byte encryptKey) : base(id, socket, encrypt, encryptKey)
        {
            _id = id;
            _folder = folder;
            _daysBefore = daysBefore;
        }

        protected override void OnReceivePackage(Packet packet)
        {
            if (packet != null)
            {
                switch (packet.DataType)
                {
                    case PacketType.FileListRequest:
                        DoFileListRequest((PacketFileListRequest)packet);
                        break;
                    case PacketType.FileContentInfoRequest:
                        DoFileContentInfoRequest((PacketFileContentInfoRequest)packet);
                        break;
                    case PacketType.FileContentDetailRequest:
                        DoFileContentDetailRequest((PacketFileContentDetailRequest)packet);
                        break;
                    default: break;
                }
            }
        }

        private void DoFileContentInfoRequest(PacketFileContentInfoRequest packet)
        {
            Log.Information($"收到读取文件信息请求:{packet.Path}");

            var localPath = System.IO.Path.Combine(_folder, packet.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
            var fileInfo = new FileInfo(localPath);

            var totalCount = (long)((fileInfo.Length - packet.LastPos) / PacketFileContentDetailResponse.MaxDataSize);
            var totalSize = fileInfo.Length - packet.LastPos;
            var lastPos = packet.LastPos;
            uint checksum = 0;
            if (packet.Checksum != 0 && packet.LastPos > 0)
            {
                try
                {
                    checksum = FileOperator.GetCrc32(localPath, packet.LastPos);
                    if (checksum != packet.Checksum)//校验不一致，重新传输
                    {
                        totalCount = (long)((fileInfo.Length) / PacketFileContentDetailResponse.MaxDataSize);
                        totalSize = fileInfo.Length;
                        lastPos = 0;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("文件检验失败:" + ex.Message);
                }
            }
            var response = new PacketFileContentInfoResponse(packet.ClientId, packet.RequestId, lastPos, checksum, totalCount, totalSize, packet.Path);
            SendPacket(response);
        }

        private void DoFileContentDetailRequest(PacketFileContentDetailRequest packet)
        {
            Log.Information($"收到读取文件内容请求:{packet.Path}");
            try
            {
                var localPath = System.IO.Path.Combine(_folder, packet.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
                var fileInfo = new FileInfo(localPath);
                if (!fileInfo.Exists)
                    SendPacket(new PacketFileContentDetailResponse(packet.ClientId, packet.RequestId, FileResponseType.FileDeleted, packet.Path));
                else
                {
                    using (var stream = File.OpenRead(localPath))
                    {
                        if (packet.StartPos > stream.Length)
                        {
                            Log.Error($"请求的位置{packet.StartPos}超出该文件'{localPath}'的大小{stream.Length}");
                            SendPacket(new PacketFileContentDetailResponse(packet.ClientId, packet.RequestId, FileResponseType.FileReadError, packet.Path));
                        }
                        if (stream.Length == 0)
                        {
                            var lastWriteTime = fileInfo.LastWriteTime.Ticks;
                            var response = new PacketFileContentDetailResponse(packet.ClientId, packet.RequestId, FileResponseType.Empty, packet.Path);
                            response.LastWriteTime = lastWriteTime;
                            SendPacket(response);
                        }
                        else
                        {
                            stream.Seek(packet.StartPos, SeekOrigin.Begin);

                            var lastWriteTime = fileInfo.LastWriteTime.Ticks;
                            var buffer = new byte[PacketFileContentDetailResponse.MaxDataSize];
                            var response = new PacketFileContentDetailResponse(packet.ClientId, packet.RequestId, FileResponseType.Content, packet.Path);
                            response.Pos = stream.Position;
                            response.FileDataLength = stream.Read(buffer);
                            response.FileData = buffer.Take(response.FileDataLength).ToArray();
                            response.FileDataTotal = stream.Length;
                            response.LastWriteTime = lastWriteTime;
                            SendPacket(response);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                SendPacket(new PacketFileContentDetailResponse(packet.ClientId, packet.RequestId, FileResponseType.FileReadError, packet.Path));
            }
        }
        private void DoFileListRequest(PacketFileListRequest packet)
        {
            Log.Information("收到读取文件列表请求");

            var path = _folder;
            var output = new List<PacketFileListDetailResponse>();
            GetFiles(packet.ClientId, packet.RequestId, new DirectoryInfo(path), DateTime.Now.AddDays(0 - _daysBefore), ref output);

            var fileListInfoResponse = new PacketFileListInfoResponse(packet.ClientId, packet.RequestId, output.LongCount(), output.Sum(f => f.FileLength));
            SendPacket(fileListInfoResponse);

            foreach (var file in output)
            {
                file.Path = file.Path.Replace(_folder, "");
                SendPacket(file);
            }
        }

        protected override void OnSocketError(int id, Socket socket, Exception e)
        {
            if (!IsConnected)
            {
                Log.Information($"客户ID（{id}）已经断开连接");
            }
        }

        private void GetFiles(int clientId, long requestId, DirectoryInfo directory, DateTime createBefore, ref List<PacketFileListDetailResponse> result)
        {
            try
            {
                if (directory.Exists)
                {
                    var query = from r in directory.GetFiles("*.*")
                                where r.Extension != ".sync" && r.CreationTime >= createBefore
                                select new PacketFileListDetailResponse(clientId, requestId, r.CreationTime.Ticks, r.LastAccessTime.Ticks, r.LastWriteTime.Ticks, r.Length, 0, r.FullName);

                    result.AddRange(query.Distinct());

                    var subDirs = directory.GetDirectories();
                    foreach (var subDir in subDirs)
                    {
                        GetFiles(clientId, requestId, subDir, createBefore, ref result);

                        query = from r in subDir.GetFiles("*.*")
                                where r.Extension != ".sync"
                                select new PacketFileListDetailResponse(clientId, requestId, r.CreationTime.Ticks, r.LastAccessTime.Ticks, r.LastWriteTime.Ticks, r.Length, 0, r.FullName);

                        result.AddRange(query.Distinct());
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
