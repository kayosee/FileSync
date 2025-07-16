using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Linq;
using System.Text;

public class SelfSignedCertificateGenerator
{
    public static void Main(string[] args)
    {
        GenerateCertificates(
            rootName: "RootCA",
            rootPassword: "P@ssw0rd",
            serverName: "server.filesync.com",
            serverPassword: "P@ssw0rd",
            clientName: "client.filesync.com",
            clientPassword: "P@ssw0rd",
            years: 10);
    }

    public static void GenerateCertificates(
        string rootName, string rootPassword,
        string serverName, string serverPassword,
        string clientName, string clientPassword,
        int years)
    {
        // 1. 创建根CA证书
        using (var rootCert = CreateRootCertificate(rootName, years))
        {
            ExportCertificate(rootCert, "RootCA.pfx", rootPassword);
            ExportPublicKey(rootCert, "RootCA.crt");

            // 2. 创建服务器证书
            using (var serverCert = CreateSignedCertificate(
                subjectName: serverName,
                issuerCert: rootCert,
                isCA: false,
                keyUsage: X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                enhancedUsages: new[] { "1.3.6.1.5.5.7.3.1" }, // 服务器认证
                sanNames: new[] { serverName, "localhost", "127.0.0.1" },
                years: years))
            {
                ExportCertificate(serverCert, "Server.pfx", serverPassword);
                ExportPublicKey(serverCert, "Server.crt");
            }

            // 3. 创建客户端证书
            using (var clientCert = CreateSignedCertificate(
                subjectName: clientName,
                issuerCert: rootCert,
                isCA: false,
                keyUsage: X509KeyUsageFlags.DigitalSignature,
                enhancedUsages: new[] { "1.3.6.1.5.5.7.3.2" }, // 客户端认证
                sanNames: null,
                years: years))
            {
                ExportCertificate(clientCert, "Client.pfx", clientPassword);
                ExportPublicKey(clientCert, "Client.crt");
            }
        }
    }

    private static X509Certificate2 CreateRootCertificate(string subjectName, int years)
    {
        // 使用RSA.Create()而不是在using块内创建
        var rsa = RSA.Create(4096);

        var request = new CertificateRequest(
            $"CN={subjectName}, O=FileSync Root CA",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // 添加扩展
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, true, 0, true));

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                true));

        var ski = new X509SubjectKeyIdentifierExtension(
            request.PublicKey,
            X509SubjectKeyIdentifierHashAlgorithm.Sha1,
            false);
        request.CertificateExtensions.Add(ski);

        // 创建自签名证书
        var rootCert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(years));

        // 确保包含私钥
        return new X509Certificate2(
            rootCert.Export(X509ContentType.Pfx, ""),
            "",
            X509KeyStorageFlags.Exportable |
            X509KeyStorageFlags.PersistKeySet);
    }

    private static X509Certificate2 CreateSignedCertificate(
        string subjectName,
        X509Certificate2 issuerCert,
        bool isCA,
        X509KeyUsageFlags keyUsage,
        string[] enhancedUsages,
        string[] sanNames,
        int years)
    {
        // 在方法作用域内创建RSA，不在using块内
        var rsa = RSA.Create(2048);

        var request = new CertificateRequest(
            $"CN={subjectName}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // 添加扩展
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(isCA, false, 0, false));

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(keyUsage, true));

        if (enhancedUsages != null && enhancedUsages.Length > 0)
        {
            var oidCollection = new OidCollection();
            foreach (var oid in enhancedUsages)
                oidCollection.Add(new Oid(oid));

            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(oidCollection, true));
        }

        if (sanNames != null && sanNames.Length > 0)
        {
            var sanBuilder = new SubjectAlternativeNameBuilder();
            foreach (var name in sanNames)
            {
                if (System.Net.IPAddress.TryParse(name, out var ip))
                    sanBuilder.AddIpAddress(ip);
                else
                    sanBuilder.AddDnsName(name);
            }
            request.CertificateExtensions.Add(sanBuilder.Build());
        }

        var ski = new X509SubjectKeyIdentifierExtension(
            request.PublicKey,
            X509SubjectKeyIdentifierHashAlgorithm.Sha1,
            false);
        request.CertificateExtensions.Add(ski);

        // 创建序列号
        var serial = new byte[16];
        RandomNumberGenerator.Fill(serial);

        // 使用根CA签名
        var signedCert = request.Create(
            issuerCert,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(years),
            serial);

        // 合并私钥并导出为PFX
        var certWithKey = signedCert.CopyWithPrivateKey(rsa);

        // 创建包含私钥的证书
        return new X509Certificate2(
            certWithKey.Export(X509ContentType.Pfx, ""),
            "",
            X509KeyStorageFlags.Exportable |
            X509KeyStorageFlags.PersistKeySet);
    }

    private static void ExportCertificate(
        X509Certificate2 cert,
        string fileName,
        string password)
    {
        // 检查是否包含私钥
        if (!cert.HasPrivateKey)
        {
            throw new InvalidOperationException($"证书 {fileName} 不包含私钥");
        }

        // 导出PFX格式
        var pfxData = cert.Export(
            X509ContentType.Pfx,
            password);

        File.WriteAllBytes(fileName, pfxData);
        Console.WriteLine($"已导出证书: {fileName}");
    }

    private static void ExportPublicKey(X509Certificate2 cert, string fileName)
    {
        // 导出公钥部分
        var publicKey = cert.Export(X509ContentType.Cert);
        File.WriteAllBytes(fileName, publicKey);

        // 导出PEM格式
        var pem = new StringBuilder();
        pem.AppendLine("-----BEGIN CERTIFICATE-----");
        pem.AppendLine(Convert.ToBase64String(
            cert.RawData,
            Base64FormattingOptions.InsertLineBreaks));
        pem.AppendLine("-----END CERTIFICATE-----");

        File.WriteAllText(
            Path.ChangeExtension(fileName, ".pem"),
            pem.ToString());
    }
}