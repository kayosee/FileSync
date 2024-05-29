﻿using FileSyncCommon;
using FileSyncCommon.Messages;
using FileSyncCommon.Messages;
using FileSyncCommon.Tools;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace FileSyncServer
{
    public class ServerSession
    {
        private string _folder;
        private int _id;
        private SocketSession _session;
        private string _password;
        public delegate void AuthenticateHandler(bool success, ServerSession session);
        public delegate void DisconnectHandler(ServerSession session);
        public event DisconnectHandler OnDisconnect;
        public event AuthenticateHandler OnAuthenticate;
        public bool IsAuthenticated { get; set; } = false;
        public SocketSession SocketSession { get { return _session; } }
        public int Id { get => _id; set => _id = value; }
        public ServerSession(int id, string folder, string password, Socket socket, bool encrypt, byte encryptKey)
        {
            _id = id;
            _folder = folder;
            _password = password;
            _session = new SocketSession(socket, encrypt, encryptKey);
            _session.OnSocketError += OnSocketError;
            _session.OnReceivePackage += OnReceivePackage;
            /*清除超时连接*/
            new Thread(() =>
            {
                Thread.Sleep(TimeSpan.FromSeconds(15));
                if (!IsAuthenticated && _session.Socket.Connected)
                {
                    Log.Information("验证超时关闭");
                    _session.Disconnect();
                }
            }).Start();
            
        }
        protected void OnReceivePackage(Message packet)
        {
            if (packet != null)
            {
                switch (packet.MessageType)
                {
                    case MessageType.AuthenticateRequest:
                        DoAuthenticateRequest((AuthenticateRequest)packet);
                        break;
                    case MessageType.FileListRequest:
                        DoFileListRequest((FileListRequest)packet);
                        break;
                    case MessageType.FileContentInfoRequest:
                        DoFileContentInfoRequest((FileContentInfoRequest)packet);
                        break;
                    case MessageType.FileContentDetailRequest:
                        DoFileContentDetailRequest((FileContentDetailRequest)packet);
                        break;
                    case MessageType.FolderListRequest:
                        DoFolderListRequest((FolderListRequest)packet);
                        break;
                    default: break;
                }
            }
        }
        private void DoFolderListRequest(FolderListRequest packet)
        {
            var localPath = System.IO.Path.Combine(_folder, packet.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
            if (Path.Exists(localPath))
            {
                DirectoryInfo di = new DirectoryInfo(localPath);
                var query = from r in di.GetDirectories()
                            let s = r.Name
                            where !string.IsNullOrEmpty(s)
                            select s;
                _session.SendMessage(new FolderListResponse(packet.ClientId, packet.RequestId, packet.Path, query.ToArray()));
            }
            else
                _session.SendMessage(new FolderListResponse(packet.ClientId, packet.RequestId, packet.Path, new string[0]));
        }
        private void DoAuthenticateRequest(AuthenticateRequest packet)
        {
            if (packet.Password == _password)
            {
                Log.Information("验证成功");
                SocketSession.SendMessage(new AuthenticateResponse(_id, packet.RequestId, true));
                IsAuthenticated = true;
                if (OnAuthenticate != null)
                    OnAuthenticate(true, this);
            }
            else
            {
                SocketSession.SendMessage(new AuthenticateResponse(_id, 0, false));
                IsAuthenticated = false;
                if (OnAuthenticate != null)
                    OnAuthenticate(false, this);
            }
        }
        private void DoFileContentInfoRequest(FileContentInfoRequest packet)
        {
            Log.Information($"收到读取文件信息请求:{packet.Path}");

            var localPath = System.IO.Path.Combine(_folder, packet.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
            var fileInfo = new FileInfo(localPath);

            var totalCount = (long)((fileInfo.Length - packet.LastPos) / FileContentDetailResponse.MaxDataSize);
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
                        totalCount = (long)((fileInfo.Length) / FileContentDetailResponse.MaxDataSize);
                        totalSize = fileInfo.Length;
                        lastPos = 0;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("文件检验失败:" + ex.Message);
                }
            }
            var response = new FileContentInfoResponse(packet.ClientId, packet.RequestId, lastPos, checksum, totalCount, totalSize, packet.Path,false);
            _session.SendMessage(response);
        }
        private void DoFileContentDetailRequest(FileContentDetailRequest packet)
        {
            Log.Information($"收到读取文件内容请求:{packet.Path}");
            try
            {
                var localPath = System.IO.Path.Combine(_folder, packet.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
                var fileInfo = new FileInfo(localPath);
                if (!fileInfo.Exists)
                    _session.SendMessage(new FileContentDetailResponse(packet.ClientId, packet.RequestId, FileResponseType.FileDeleted, packet.Path,false));
                else
                {
                    using (var stream = File.OpenRead(localPath))
                    {
                        if (packet.StartPos > stream.Length)
                        {
                            Log.Error($"请求的位置{packet.StartPos}超出该文件'{localPath}'的大小{stream.Length}");
                            _session.SendMessage(new FileContentDetailResponse(packet.ClientId, packet.RequestId, FileResponseType.FileReadError, packet.Path, false));
                        }
                        if (stream.Length == 0)
                        {
                            var lastWriteTime = fileInfo.LastWriteTime.Ticks;
                            var response = new FileContentDetailResponse(packet.ClientId, packet.RequestId, FileResponseType.Empty, packet.Path, false);
                            response.LastWriteTime = lastWriteTime;
                            _session.SendMessage(response);
                        }
                        else
                        {
                            stream.Seek(packet.StartPos, SeekOrigin.Begin);

                            var lastWriteTime = fileInfo.LastWriteTime.Ticks;
                            var buffer = new byte[FileContentDetailResponse.MaxDataSize];
                            var response = new FileContentDetailResponse(packet.ClientId, packet.RequestId, FileResponseType.Content, packet.Path, false);
                            response.Pos = stream.Position;
                            response.FileDataLength = stream.Read(buffer);
                            response.FileData = buffer.Take(response.FileDataLength).ToArray();
                            response.FileDataTotal = stream.Length;
                            response.LastWriteTime = lastWriteTime;
                            _session.SendMessage(response);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                _session.SendMessage(new FileContentDetailResponse(packet.ClientId, packet.RequestId, FileResponseType.FileReadError, packet.Path, false));
            }
        }
        private void DoFileListRequest(FileListRequest packet)
        {
            Log.Information("收到读取文件列表请求");

            var localPath = System.IO.Path.Combine(_folder, packet.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
            var output = new List<FileListDetailResponse>();
            GetFiles(packet.ClientId, packet.RequestId, new DirectoryInfo(localPath), packet.DaysBefore <= 0 ? null : DateTime.Now.AddDays(0 - packet.DaysBefore), ref output);

            var fileListInfoResponse = new FileListInfoResponse(packet.ClientId, packet.RequestId, output.LongCount(), output.Sum(f => f.FileLength),true);
            _session.SendMessage(fileListInfoResponse);

            foreach (var file in output)
            {
                file.Path = file.Path.Replace(_folder, "");
                _session.SendMessage(file);
            }
        }
        protected void OnSocketError(SocketSession socketSession, Exception e)
        {
            if (!_session.Socket.Connected)
            {
                Log.Information($"客户ID（{_id}）已经断开连接");
                _session.Disconnect();

                if (OnDisconnect != null)
                    OnDisconnect(this);
            }
        }
        private void GetFiles(int clientId, long requestId, DirectoryInfo directory, DateTime? createBefore, ref List<FileListDetailResponse> result)
        {
            try
            {
                if (directory.Exists)
                {
                    var query = from r in directory.GetFiles("*.*")
                                where r.Extension != ".sync" && ((createBefore != null && r.CreationTime >= createBefore) || createBefore == null)
                                select new FileListDetailResponse(clientId, requestId, r.CreationTime.Ticks, r.LastAccessTime.Ticks, r.LastWriteTime.Ticks, r.Length, 0, r.FullName,true);

                    result.AddRange(query.Distinct());

                    var subDirs = directory.GetDirectories();
                    foreach (var subDir in subDirs)
                    {
                        GetFiles(clientId, requestId, subDir, createBefore, ref result);

                        query = from r in subDir.GetFiles("*.*")
                                where r.Extension != ".sync" && ((createBefore != null && r.CreationTime >= createBefore) || createBefore == null)
                                select new FileListDetailResponse(clientId, requestId, r.CreationTime.Ticks, r.LastAccessTime.Ticks, r.LastWriteTime.Ticks, r.Length, 0, r.FullName, true);

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
