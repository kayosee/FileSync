using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

public class SelfSignedCertificateGenerator
{
    public static void Main(string[] args)
    {
        // 生成证书
        GenerateCertificates("RootCA", "filesync.server.com", "filesync.client.com", 10);
    }
    public static void GenerateCertificates(string rootName, string serverName, string clientName, int years)
    {
        // 1. 创建根CA证书 (用于签发其他证书)
        using (var rootCert = CreateRootCertificate(rootName, years))
        {
            // 2. 创建服务器证书
            var serverCert = CreateSignedCertificate(
                subjectName: serverName,
                issuerCert: rootCert,
                isCA: false,
                keyUsage: X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                enhancedUsages: new[] {
                    "1.3.6.1.5.5.7.3.1" // 服务器认证 OID
                }, years);

            // 3. 创建客户端证书
            var clientCert = CreateSignedCertificate(
                subjectName: clientName,
                issuerCert: rootCert,
                isCA: false,
                keyUsage: X509KeyUsageFlags.DigitalSignature,
                enhancedUsages: new[] {
                    "1.3.6.1.5.5.7.3.2" // 客户端认证 OID
                }, years);

            // 4. 导出证书文件
            ExportCertificate(rootCert, "RootCA.pfx", "root_password");
            ExportCertificate(serverCert, "Server.pfx", "server_password");
            ExportCertificate(clientCert, "Client.pfx", "client_password");

            Console.WriteLine("证书生成完成!");
        }
    }

    private static X509Certificate2 CreateRootCertificate(string subjectName, int years)
    {
        using (var rsa = RSA.Create(4096))
        {
            var request = new CertificateRequest(
                $"CN={subjectName}",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // 设置CA基本约束
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(true, true, 0, true));

            // 设置密钥用法
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                    true));

            // 自签名
            return request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1), // 有效期起始（昨天）
                DateTimeOffset.UtcNow.AddYears(years)  // 有效期结束
            );
        }
    }

    private static X509Certificate2 CreateSignedCertificate(
        string subjectName,
        X509Certificate2 issuerCert,
        bool isCA,
        X509KeyUsageFlags keyUsage,
        string[] enhancedUsages, int years)
    {
        using (var rsa = RSA.Create(2048))
        {
            var request = new CertificateRequest(
                $"CN={subjectName}",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // 设置基本约束（是否为CA）
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(isCA, false, 0, false));

            // 设置密钥用法
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(keyUsage, true));

            // 设置增强型密钥用法
            var oidCollection = new OidCollection();
            foreach (var oid in enhancedUsages)
                oidCollection.Add(new Oid(oid));

            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(oidCollection, true));

            // 使用根CA进行签名
            var serial = RandomNumberGenerator.GetBytes(16);
            return request.Create(
                issuerCert,
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(years),
                serial);
        }
    }

    private static void ExportCertificate(
        X509Certificate2 cert,
        string fileName,
        string password)
    {
        // 导出PFX格式（包含私钥）
        var pfxData = cert.Export(X509ContentType.Pfx, password);
        System.IO.File.WriteAllBytes(fileName, pfxData);

        // 可选：导出CRT格式（公钥）
        var cerData = cert.Export(X509ContentType.Cert);
        System.IO.File.WriteAllBytes($"{System.IO.Path.GetFileNameWithoutExtension(fileName)}.crt", cerData);
    }
}