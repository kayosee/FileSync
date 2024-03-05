using FileSyncCommon;
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
            var localPath = System.IO.Path.Combine(_folder, packet.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
            var fileInfo = new FileInfo(localPath);

            var totalCount = (long)((fileInfo.Length - packet.StartPos) / PacketFileContentDetailResponse.MaxDataSize);
            var totalSize = fileInfo.Length;
            var response = new PacketFileContentInfoResponse(packet.ClientId, packet.InquireId, packet.RequestId, packet.StartPos, totalCount, totalSize, packet.Path);
            SendPacket(response);
        }

        private void DoFileContentDetailRequest(PacketFileContentDetailRequest packet)
        {
            var localPath = System.IO.Path.Combine(_folder, packet.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
            if (!File.Exists(localPath))
                SendPacket(new PacketFileContentDetailResponse(packet.ClientId, packet.InquireId, packet.RequestId, FileResponseType.FileDeleted, packet.Path));
            else
            {
                using (var stream = File.OpenRead(localPath))
                {
                    if (packet.StartPos > stream.Length)
                    {
                        Log.Error($"请求的位置{packet.StartPos}超出该文件'{localPath}'的大小{stream.Length}");
                        SendPacket(new PacketFileContentDetailResponse(packet.ClientId, packet.InquireId, packet.RequestId, FileResponseType.FileReadError, packet.Path));
                    }
                    if (stream.Length == 0)
                    {
                        var lastWriteTime = new FileInfo(localPath).LastWriteTime.Ticks;
                        var response = new PacketFileContentDetailResponse(packet.ClientId, packet.InquireId, packet.RequestId, FileResponseType.Empty, packet.Path);
                        response.LastWriteTime = lastWriteTime;
                        SendPacket(response);
                    }
                    else
                    {
                        stream.Seek(packet.StartPos, SeekOrigin.Begin);

                        var lastWriteTime = new FileInfo(localPath).LastWriteTime.Ticks;
                        var buffer = new byte[PacketFileContentDetailResponse.MaxDataSize];
                        var response = new PacketFileContentDetailResponse(packet.ClientId, packet.InquireId, packet.RequestId, FileResponseType.Content, packet.Path);
                        response.Pos = stream.Position;
                        response.FileDataLength = stream.Read(buffer);
                        response.FileData = buffer.Take(response.FileDataLength).ToArray();
                        response.FileDataTotal = stream.Length;
                        response.LastWriteTime = lastWriteTime;
                        Log.Debug($"正在发送：{response.Path},位置:{response.Pos},长度:{response.FileData.Length},总共:{response.FileDataTotal}");
                        SendPacket(response);
                    }
                }
            }
        }
        private void DoFileListRequest(PacketFileListRequest packet)
        {
            var path = _folder;
            var output = new List<PacketFileListDetailResponse>();
            GetFiles(packet.ClientId, packet.InquireId, new DirectoryInfo(path), DateTime.Now.AddDays(0-_daysBefore), ref output);

            var fileListInfoResponse = new PacketFileListInfoResponse(packet.ClientId, packet.InquireId, output.LongCount(), output.Sum(f => f.FileLength));
            SendPacket(fileListInfoResponse);
            
            foreach (var file in output)
            {
                file.Path = file.Path.Replace(_folder, "");
                file.Total = output.Count;
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

        private void GetFiles(int clientId, long inquireId, DirectoryInfo directory, DateTime createBefore, ref List<PacketFileListDetailResponse> result)
        {
            try
            {
                if (directory.Exists)
                {
                    var query = from r in directory.GetFiles("*.*")
                                where r.Extension != ".sync" && r.CreationTime >= createBefore
                                select new PacketFileListDetailResponse(clientId, inquireId, r.CreationTime.Ticks, r.LastAccessTime.Ticks, r.LastWriteTime.Ticks, r.Length, FileOperator.GetCrc32(r.FullName).GetValueOrDefault(), r.FullName);

                    result.AddRange(query.Distinct());

                    var subDirs = directory.GetDirectories();
                    foreach (var subDir in subDirs)
                    {
                        GetFiles(clientId, inquireId, subDir, createBefore, ref result);

                        query = from r in subDir.GetFiles("*.*")
                                where r.Extension != ".sync"
                                select new PacketFileListDetailResponse(clientId, inquireId, r.CreationTime.Ticks, r.LastAccessTime.Ticks, r.LastWriteTime.Ticks, r.Length, FileOperator.GetCrc32(r.FullName).GetValueOrDefault(), r.FullName);

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
