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
        private const int AuthenticateTimeout = 3;
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
                    var packet = session.ReceivePacket(TimeSpan.FromSeconds(AuthenticateTimeout));
                    if (packet != null)
                    {
                        if (packet.DataType == PacketType.AuthenticateRequest)
                        {
                            var request = (PacketAuthenticateRequest)packet;
                            if (request.Password == _password)
                            {
                                Log.Information("验证成功");
                                client.IOControl(IOControlCode.KeepAliveValues, KeepAlive(1, 3000, 1000), null);//设置Keep-Alive参数
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
                    _ = new Timer((e) =>
                    {
                        session.Disconnect();
                    }, null, TimeSpan.FromSeconds(AuthenticateTimeout), Timeout.InfiniteTimeSpan);//超时关闭验证失败连接,定时是为了发送结果出去
                }
            });
            _acceptor.Name = "acceptor";
            _acceptor.Start();

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
