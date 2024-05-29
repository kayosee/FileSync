using FileSyncCommon;
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
                Thread.Sleep(TimeSpan.FromSeconds(150));
                if (!IsAuthenticated && _session.Socket.Connected)
                {
                    Log.Information("验证超时关闭");
                    _session.Disconnect();
                }
            }).Start();

        }
        protected void OnReceivePackage(Message messages)
        {
            if (messages != null)
            {
                switch (messages.MessageType)
                {
                    case MessageType.AuthenticateRequest:
                        DoAuthenticateRequest((AuthenticateRequest)messages);
                        break;
                    case MessageType.FileListRequest:
                        DoFileListRequest((FileListRequest)messages);
                        break;
                    case MessageType.FileContentInfoRequest:
                        DoFileContentInfoRequest((FileContentInfoRequest)messages);
                        break;
                    case MessageType.FileContentDetailRequest:
                        DoFileContentDetailRequest((FileContentDetailRequest)messages);
                        break;
                    case MessageType.FolderListRequest:
                        DoFolderListRequest((FolderListRequest)messages);
                        break;
                    default: break;
                }
            }
        }
        private void DoFolderListRequest(FolderListRequest messages)
        {
            var localPath = System.IO.Path.Combine(_folder, messages.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
            if (Path.Exists(localPath))
            {
                DirectoryInfo di = new DirectoryInfo(localPath);
                var query = from r in di.GetDirectories()
                            let s = r.Name
                            where !string.IsNullOrEmpty(s)
                            select s;
                _session.SendMessage(new FolderListResponse(messages.ClientId, messages.RequestId, messages.Path, query.ToArray()));
            }
            else
                _session.SendMessage(new FolderListResponse(messages.ClientId, messages.RequestId, messages.Path, new string[0]));
        }
        private void DoAuthenticateRequest(AuthenticateRequest messages)
        {
            if (messages.Password == _password)
            {
                Log.Information("验证成功");
                SocketSession.SendMessage(new AuthenticateResponse(_id, messages.RequestId, true));
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
        private void DoFileContentInfoRequest(FileContentInfoRequest messages)
        {
            Log.Information($"收到读取文件信息请求:{messages.Path}");

            var localPath = System.IO.Path.Combine(_folder, messages.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
            var fileInfo = new FileInfo(localPath);

            var totalCount = (long)((fileInfo.Length - messages.LastPos) / FileContentDetailResponse.MaxDataSize);
            var totalSize = fileInfo.Length - messages.LastPos;
            var lastPos = messages.LastPos;
            uint checksum = 0;
            if (messages.Checksum != 0 && messages.LastPos > 0)
            {
                try
                {
                    checksum = FileOperator.GetCrc32(localPath, messages.LastPos);
                    if (checksum != messages.Checksum)//校验不一致，重新传输
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
            var response = new FileContentInfoResponse(messages.ClientId, messages.RequestId, lastPos, checksum, totalCount, totalSize, messages.Path);
            _session.SendMessage(response);
        }
        private void DoFileContentDetailRequest(FileContentDetailRequest messages)
        {
            Log.Information($"收到读取文件内容请求:{messages.Path}");
            try
            {
                var localPath = System.IO.Path.Combine(_folder, messages.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
                var fileInfo = new FileInfo(localPath);
                if (!fileInfo.Exists)
                    _session.SendMessage(new FileContentDetailResponse(messages.ClientId, messages.RequestId, FileResponseType.FileDeleted, messages.Path, false));
                else
                {
                    using (var stream = File.OpenRead(localPath))
                    {
                        if (messages.StartPos > stream.Length)
                        {
                            Log.Error($"请求的位置{messages.StartPos}超出该文件'{localPath}'的大小{stream.Length}");
                            _session.SendMessage(new FileContentDetailResponse(messages.ClientId, messages.RequestId, FileResponseType.FileReadError, messages.Path, true));
                        }
                        if (stream.Length == 0)
                        {
                            var lastWriteTime = fileInfo.LastWriteTime.Ticks;
                            var response = new FileContentDetailResponse(messages.ClientId, messages.RequestId, FileResponseType.Empty, messages.Path, true);
                            response.LastWriteTime = lastWriteTime;
                            _session.SendMessage(response);
                        }
                        else
                        {
                            stream.Seek(messages.StartPos, SeekOrigin.Begin);

                            var lastWriteTime = fileInfo.LastWriteTime.Ticks;
                            var buffer = new byte[FileContentDetailResponse.MaxDataSize];
                            var response = new FileContentDetailResponse(messages.ClientId, messages.RequestId, FileResponseType.Content, messages.Path, false);
                            response.Latest = response.Pos + response.FileDataLength >= response.FileDataTotal;
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
                _session.SendMessage(new FileContentDetailResponse(messages.ClientId, messages.RequestId, FileResponseType.FileReadError, messages.Path, false));
            }
        }
        private void DoFileListRequest(FileListRequest messages)
        {
            Log.Information("收到读取文件列表请求");

            var localPath = System.IO.Path.Combine(_folder, messages.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
            var output = new List<FileListDetailResponse>();
            GetFiles(messages.ClientId, messages.RequestId, new DirectoryInfo(localPath), messages.DaysBefore <= 0 ? null : DateTime.Now.AddDays(0 - messages.DaysBefore), ref output);

            var fileListInfoResponse = new FileListInfoResponse(messages.ClientId, messages.RequestId, output.LongCount(), output.Sum(f => f.FileLength), true);
            _session.SendMessage(fileListInfoResponse);

            output[output.Count - 1].Latest = true;
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
                                select new FileListDetailResponse(clientId, requestId, r.CreationTime.Ticks, r.LastAccessTime.Ticks, r.LastWriteTime.Ticks, r.Length, 0, r.FullName, false);

                    result.AddRange(query.Distinct());

                    var subDirs = directory.GetDirectories();
                    foreach (var subDir in subDirs)
                    {
                        GetFiles(clientId, requestId, subDir, createBefore, ref result);

                        query = from r in subDir.GetFiles("*.*")
                                where r.Extension != ".sync" && ((createBefore != null && r.CreationTime >= createBefore) || createBefore == null)
                                select new FileListDetailResponse(clientId, requestId, r.CreationTime.Ticks, r.LastAccessTime.Ticks, r.LastWriteTime.Ticks, r.Length, 0, r.FullName, false);

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
