using FileSyncCommon.Tools;
using System.Net;

namespace FileSyncServer
{
    public static class FailCounter
    {
        class FailClient
        {
            public string Address { get; set; }
            public int Counter { get; set; }
            public DateTime LastTime { get; set; }
            public FailClient(string address)
            {
                Address = address;
                Counter = 1;
                LastTime = DateTime.Now;
            }
        }

        private static Dictionary<string, FailClient> clients = new Dictionary<string, FailClient>();
        public static void Clear() { clients.Clear(); }
        public static void Reset(IPEndPoint ip)
        {
            var id = ip.Address.ToString();
            if (clients.TryGetValue(id, out var client))
            {
                client.LastTime = DateTime.UtcNow;
                client.Counter = 0;
            }
        }
        public static int Increase(IPEndPoint ip)
        {
            var id = ip.Address.ToString();
            if (clients.TryGetValue(id, out var client))
            {
                client.LastTime = DateTime.UtcNow;
                client.Counter++;
            }
            else
            {
                client = new FailClient(id);
                clients.Add(id, client);
            }
            return client.Counter;
        }
        public static int Get(IPEndPoint ip)
        {
            var id = ip.Address.ToString();
            if (clients.TryGetValue(id, out var client))
            {
                if (DateTime.UtcNow - client.LastTime > TimeSpan.FromMinutes(ConfigReader.GetInt("failInterval", 30)))
                {
                    client.Counter = 0;
                    client.LastTime = DateTime.UtcNow;
                }
                return client.Counter;
            }
            else
            {
                return 0;
            }
        }
    }
}
