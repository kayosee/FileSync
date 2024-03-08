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
        private int _daysBefore;
        public Server(int port, string folder, bool encrypt, byte encryptKey, string password, int daysBefore)
        {
            _port = port;
            _folder = folder;
            _encrypt = encrypt;
            _encryptKey = encryptKey;
            _password = password;
            _daysBefore = daysBefore;
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
                    var session = new ServerSession(clientId, _folder, _daysBefore, client, _encrypt, _encryptKey);
                    var packet = session.ReceivePacket(TimeSpan.FromSeconds(5));
                    if (packet != null)
                    {
                        if (packet.DataType == PacketType.AuthenticateRequest)
                        {
                            var request = (PacketAuthenticateRequest)packet;
                            if (request.Password == _password)
                            {
                                Log.Information("验证成功");
                                session.SendPacket(new PacketAuthenticateResponse(clientId, request.RequestId, true));
                                session.StartMessageLoop();
                                if (_sessions.TryAdd(clientId, session))
                                {
                                    session.SendPacket(new PacketHandshake(clientId));
                                }
                                continue;
                            }
                        }
                    }

                    Log.Error("验证失败");
                    session.SendPacket(new PacketAuthenticateResponse(clientId, 0, false));
                    session.Disconnect();
                }
            });
            _acceptor.Name = "acceptor";
            _acceptor.Start();

        }
        public void Stop()
        {
            _socket.Close();
        }
    }
}
