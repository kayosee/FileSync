using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;
using FileSyncCommon;
using FileSyncCommon.Tools;

namespace FileSyncServer
{
    public class Server
    {
        private ConcurrentDictionary<int, ServerSession> _sessions = new ConcurrentDictionary<int, ServerSession>();
        private Socket _socket;
        private Thread _acceptor;
        private int _port;
        private string _folder;
        private bool _encrypt;
        private byte _encryptKey;
        private string _password;
        private const int AuthenticateTimeout = 5;
        public Server(int port, string folder, string password)
        {
            _port = port;
            _folder = folder;
            _password = password;
            _encrypt = !string.IsNullOrEmpty(password);
            if(_encrypt)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(password);
                _encryptKey = bytes.Aggregate((s, t) => s ^= t);
            }
                
        }
        public int Port { get => _port; set => _port = value; }
        public string Folder { get => _folder; set => _folder = value; }
        public void Start()
        {
            _socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _socket.Bind(new IPEndPoint(IPAddress.Any, Port));
            _socket.Listen();

            _acceptor = new Thread(() =>
            {
                while (true)
                {
                    var client = _socket.Accept();
                    Log.Information("新连接加入");

                    var clientId = _sessions.Count;
                    var session = new ServerSession(clientId, _folder,_password, client, _encrypt, _encryptKey);
                    session.OnAuthenticate += Session_OnAuthenticate;
                    session.OnDisconnect += Session_OnDisconnect;
                }
            });
            _acceptor.Name = "acceptor";
            _acceptor.Start();

        }

        private void Session_OnDisconnect(ServerSession session)
        {
            if (_sessions.ContainsKey(session.Id))
                _sessions.Remove(session.Id, out var _);
        }

        private void Session_OnAuthenticate(bool success, ServerSession session)
        {
            if (success)
            {
                _sessions.TryAdd(session.Id, session);
                session.SocketSession.Socket.IOControl(IOControlCode.KeepAliveValues, KeepAlive(1, 3000, 2000), null);//设置Keep-Alive参数
            }
            else
            {
                session.SocketSession.Disconnect();
            }
        }

        public void Stop()
        {
            _socket.Close();
        }

        private byte[] KeepAlive(int onOff, int keepAliveTime, int keepAliveInterval)
        {
            byte[] buffer = new byte[12];
            BitConverter.GetBytes(onOff).CopyTo(buffer, 0);
            BitConverter.GetBytes(keepAliveTime).CopyTo(buffer, 4);
            BitConverter.GetBytes(keepAliveInterval).CopyTo(buffer, 8);
            return buffer;
        }
    }
}
