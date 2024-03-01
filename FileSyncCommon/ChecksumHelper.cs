using Force.Crc32;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
namespace FileSyncCommon;

public static class ChecksumHelper
{
    public static string GetChecksum(string path)
    {
        if (!File.Exists(path))
        {
            return "";
        }
        else
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(path))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }
    }
    public static string GetChecksum(byte[] data)
    {
        using (var md5 = MD5.Create())
        {
            using (var stream = new MemoryStream(data))
            {
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
    public static uint? GetCrc32(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        else
        {
            using (var stream = File.OpenRead(path))
            {
                uint initial = 0;
                var buffer = new byte[1024 * 1024];
                int nret = 0;
                while ((nret = stream.Read(buffer)) > 0)
                {
                    initial = Crc32Algorithm.Append(initial, buffer.Take(nret).ToArray());
                }
                return initial;
            }
        }
    }
    public static string GetCrc32String(string path)
    {
        var result = GetCrc32(path);
        if (result == null)
            return "";

        return result.Value.ToString();
    }
}
