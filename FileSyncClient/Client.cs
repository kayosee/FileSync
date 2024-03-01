using FileSyncCommon;
using HYFTPCommon;
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
        public Client(string ip, int port, string folder, bool encrypt, byte encryptKey) : base(0, new Socket(SocketType.Stream, ProtocolType.Tcp), encrypt, encryptKey)
        {
            _folder = folder;
            _port = port;
            _encrypt = encrypt;
            _encryptKey = encryptKey;
            if (Connect(ip, port))
            {
                Start();
                Log.Information($"已连接到服务器:{ip}:{port}");
            }
        }

        protected override void OnReceivePackage(Packet packet)
        {
            if (packet != null)
            {
                var responses = packet.Process(_folder);
                if (responses != null)
                {
                    foreach (var item in responses)
                    {
                        SendPacket(item);
                    }
                }
            }
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
