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
                switch ((PacketType)packet.DataType)
                {
                    case PacketType.FileInquire:
                        DoFileInquire((PacketFileInquire)packet);
                        break;
                    case PacketType.FileRequest:
                        DoFileRequest((PacketFileRequest)packet);
                        break;
                    default: break;
                }
            }
        }

        private void DoFileRequest(PacketFileRequest packet)
        {
            var localPath = System.IO.Path.Combine(_folder, packet.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
            if (!File.Exists(localPath))
                SendPacket(new PacketFileResponse(packet.ClientId, (byte)FileResponseType.FileDeleted, packet.Path));
            else
            {
                using (var stream = File.OpenRead(localPath))
                {
                    if (packet.StartPos > stream.Length)
                    {
                        Log.Error($"请求的位置{packet.StartPos}超出该文件'{localPath}'的大小{stream.Length}");
                        SendPacket(new PacketFileResponse(packet.ClientId, (byte)FileResponseType.FileReadError, packet.Path));
                    }
                    if (stream.Length == 0)
                    {
                        SendPacket(new PacketFileResponse(packet.ClientId, (byte)FileResponseType.Empty, packet.Path));
                    }

                    stream.Seek(packet.StartPos, SeekOrigin.Begin);

                    var lastWriteTime = new FileInfo(localPath).LastWriteTime.Ticks;
                    var buffer = new byte[PacketFileResponse.MaxDataSize];
                    while (stream.Position < stream.Length)
                    {
                        var response = new PacketFileResponse(packet.ClientId, (byte)FileResponseType.Content, packet.Path);
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
        public new void SendPacket(Packet packet)
        {
            //Log.Information($"{++_total}");
            base.SendPacket(packet);
        }
        private void DoFileInquire(PacketFileInquire packet)
        {
            var path = _folder;
            var output = new List<PacketFileInfomation>();
            GetFiles(packet.ClientId, new DirectoryInfo(path), DateTime.Now.AddDays(_daysBefore), ref output);

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

        private void GetFiles(int clientId, DirectoryInfo directory, DateTime createBefore, ref List<PacketFileInfomation> result)
        {
            try
            {
                if (directory.Exists)
                {
                    var query = from r in directory.GetFiles("*.*")
                                where r.Extension != ".sync" && r.CreationTime >= createBefore
                                select new PacketFileInfomation(clientId, r.CreationTime.Ticks, r.LastAccessTime.Ticks, r.LastWriteTime.Ticks, r.Length, FileOperator.GetCrc32(r.FullName).GetValueOrDefault(), r.FullName);

                    result.AddRange(query.Distinct());

                    var subDirs = directory.GetDirectories();
                    foreach (var subDir in subDirs)
                    {
                        GetFiles(clientId, subDir, createBefore, ref result);

                        query = from r in subDir.GetFiles("*.*")
                                where r.Extension != ".sync"
                                select new PacketFileInfomation(clientId, r.CreationTime.Ticks, r.LastAccessTime.Ticks, r.LastWriteTime.Ticks, r.Length, FileOperator.GetCrc32(r.FullName).GetValueOrDefault(), r.FullName);

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
