using FileSyncCommon;
using FileSyncCommon.Messages;
using FileSyncCommon.Tools;
using Serilog;
using System.Net.Security;

namespace FileSyncServer
{
    public class ServerSession
    {
        private string _folder;
        private int _id;
        private StreamPacker _packer;
        private Dictionary<string, FileInfo> _files;
        public delegate void DisconnectHandler(ServerSession session);
        public event DisconnectHandler OnDisconnect;
        public int Id { get => _id; set => _id = value; }
        public ServerSession(int id, string folder, SslStream stream)
        {
            _id = id;
            _folder = folder;
            _packer = new StreamPacker(stream);
            _packer.OnStreamError += OnStreamError;
            _packer.OnReceivePackage += OnReceivePackage;
        }
        protected void OnReceivePackage(Message message)
        {
            if (message != null)
            {
                switch (message.MessageType)
                {
                    case MessageType.FileListTotalRequest:
                        DoFileListTotalRequest((FileListTotalRequest)message);
                        break;
                    case MessageType.FileListDetailRequest:
                        DoFileListDetailRequest((FileListDetailRequest)message);
                        break;
                    case MessageType.FileInfoRequest:
                        DoFileInfoRequest((FileInfoRequest)message);
                        break;
                    case MessageType.FileContentRequest:
                        DoFileContentRequest((FileContentRequest)message);
                        break;
                    case MessageType.FolderListRequest:
                        DoFolderListRequest((FolderListRequest)message);
                        break;
                    default: break;
                }
            }
        }
        private void DoFolderListRequest(FolderListRequest message)
        {
            var localPath = System.IO.Path.Combine(_folder, message.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
            if (Path.Exists(localPath))
            {
                DirectoryInfo di = new DirectoryInfo(localPath);
                var query = from r in di.GetDirectories()
                            let s = r.Name
                            where !string.IsNullOrEmpty(s)
                            select s;
                _packer.SendMessage(new FolderListResponse(message.ClientId, message.RequestId, message.Path, query.ToArray()));
            }
            else
                _packer.SendMessage(new FolderListResponse(message.ClientId, message.RequestId, message.Path, new string[0]));
        }
        private void DoFileInfoRequest(FileInfoRequest message)
        {
            Log.Information($"收到读取文件信息请求:{message.Path}");
            FileInfoResponse response = null;
            var localPath = System.IO.Path.Combine(_folder, message.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
            var fileInfo = new FileInfo(localPath);
            if (!fileInfo.Exists)
            {
                response = new FileInfoResponse(message.ClientId, message.RequestId, message.Path, Error.FileNotExists);
                Log.Error($"文件{fileInfo.FullName}不存在。");
            }
            else
            {
                var totalCount = (long)((fileInfo.Length - message.LastPos) / FileContentResponse.MaxDataSize);
                var totalSize = fileInfo.Length - message.LastPos;
                var lastPos = message.LastPos;
                uint checksum = 0;
                if (message.Checksum != 0 && message.LastPos > 0)
                {
                    try
                    {
                        checksum = FileOperator.GetCrc32(localPath, message.LastPos);
                        if (checksum != message.Checksum)//校验不一致，重新传输
                        {
                            totalCount = (long)((fileInfo.Length) / FileContentResponse.MaxDataSize);
                            totalSize = fileInfo.Length;
                            lastPos = 0;
                        }
                        response = new FileInfoResponse(message.ClientId, message.RequestId, message.Path, lastPos, checksum, totalCount, totalSize);
                    }
                    catch (Exception ex)
                    {
                        response = new FileInfoResponse(message.ClientId, message.RequestId, message.Path, Error.FileCheckError);
                        Log.Error("文件检验失败:" + ex.Message);
                    }
                }
                else
                    response = new FileInfoResponse(message.ClientId, message.RequestId, message.Path, lastPos, checksum, totalCount, totalSize);
            }
            _packer.SendMessage(response);
        }
        private void DoFileContentRequest(FileContentRequest message)
        {
            Log.Information($"收到读取文件内容请求:{message.Path}");
            try
            {
                var localPath = System.IO.Path.Combine(_folder, message.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
                var fileInfo = new FileInfo(localPath);
                if (!fileInfo.Exists)
                    _packer.SendMessage(new FileContentResponse(message.ClientId, message.RequestId, FileResponseType.FileDeleted, message.Path, false));
                else
                {
                    using (var stream = File.OpenRead(localPath))
                    {
                        if (message.StartPos > stream.Length)
                        {
                            Log.Error($"请求的位置{message.StartPos}超出该文件'{localPath}'的大小{stream.Length}");
                            _packer.SendMessage(new FileContentResponse(message.ClientId, message.RequestId, FileResponseType.FileReadError, message.Path, true));
                        }
                        if (stream.Length == 0)
                        {
                            var lastWriteTime = fileInfo.LastWriteTime.Ticks;
                            var response = new FileContentResponse(message.ClientId, message.RequestId, FileResponseType.Empty, message.Path, true);
                            response.LastWriteTime = lastWriteTime;
                            _packer.SendMessage(response);
                        }
                        else
                        {
                            stream.Seek(message.StartPos, SeekOrigin.Begin);

                            var lastWriteTime = fileInfo.LastWriteTime.Ticks;
                            var buffer = new byte[FileContentResponse.MaxDataSize];
                            var response = new FileContentResponse(message.ClientId, message.RequestId, FileResponseType.Content, message.Path, false);
                            response.Latest = response.Pos + response.FileDataLength >= response.FileDataTotal;
                            response.Pos = stream.Position;
                            response.FileDataLength = stream.Read(buffer);
                            response.FileData = buffer.Take(response.FileDataLength).ToArray();
                            response.FileDataTotal = stream.Length;
                            response.LastWriteTime = lastWriteTime;
                            _packer.SendMessage(response);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                _packer.SendMessage(new FileContentResponse(message.ClientId, message.RequestId, FileResponseType.FileReadError, message.Path, false));
            }
        }
        private void DoFileListTotalRequest(FileListTotalRequest message)
        {
            Log.Information("收到读取文件列表信息请求");

            var localPath = System.IO.Path.Combine(_folder, message.Path.TrimStart(System.IO.Path.DirectorySeparatorChar));
            _files = new();
            GetFiles(message.ClientId, message.RequestId, new DirectoryInfo(localPath), message.DaysBefore <= 0 ? null : DateTime.Now.AddDays(0 - message.DaysBefore), ref _files);

            var fileListInfoResponse = new FileListTotalResponse(message.ClientId, message.RequestId, message.Path, _files.Count, 0, true);
            _packer.SendMessage(fileListInfoResponse);
        }
        private void DoFileListDetailRequest(FileListDetailRequest message)
        {
            Log.Information("收到读取文件列表详情请求");

            var output = from r in _files.Values
                         select new FileListDetail(r.FullName.Replace(_folder, ""), r.CreationTime.Ticks, r.LastAccessTime.Ticks, r.LastWriteTime.Ticks, r.Length, 0);

            var response = new FileListDetailResponse(message.ClientId, message.RequestId, message.Path, output.ToList());
            _packer.SendMessage(response);
        }
        protected void OnStreamError(StreamPacker tcpSession, Exception e)
        {
            Log.Information($"客户ID（{_id}）已经断开连接");
            _packer.Dispose();

            if (OnDisconnect != null)
                OnDisconnect(this);
        }
        private void GetFiles(int clientId, long requestId, DirectoryInfo directory, DateTime? createBefore, ref Dictionary<string, FileInfo> result)
        {
            try
            {
                if (directory.Exists)
                {
                    var query = from r in directory.GetFiles("*.*")
                                where r.Extension != ".sync" && ((createBefore != null && r.CreationTime >= createBefore) || createBefore == null)
                                select r;

                    foreach (var file in query)
                    {
                        result.TryAdd(file.FullName, file);
                    }

                    var subDirs = directory.GetDirectories();
                    foreach (var subDir in subDirs)
                    {
                        GetFiles(clientId, requestId, subDir, createBefore, ref result);

                        query = from r in subDir.GetFiles("*.*")
                                where r.Extension != ".sync" && ((createBefore != null && r.CreationTime >= createBefore) || createBefore == null)
                                select r;

                        foreach (var file in query)
                        {
                            result.TryAdd(file.FullName, file);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
            }
        }
    }
}
