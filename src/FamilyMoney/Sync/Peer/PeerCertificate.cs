using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace FamilyMoney.Sync.Peer;

public static class PeerCertificate
{
    public static X509Certificate2 CreateSelfSigned()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=FamilyMoney-Peer",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: false));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection
                {
                    new("1.3.6.1.5.5.7.3.1"),
                },
                critical: false));

        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1));

        // Экспорт/импорт закрепляет приватный ключ за объектом и делает его
        // пригодным для SslStream на всех поддерживаемых платформах.
        // EphemeralKeySet поддерживается не на всех платформах (в частности,
        // SslStream на Windows может отказаться от такого ключа). DefaultKeySet
        // создаёт обычный пользовательский ключ и работает с SslStream.
        return X509CertificateLoader.LoadPkcs12(
            certificate.Export(X509ContentType.Pfx),
            password: null,
            X509KeyStorageFlags.DefaultKeySet);
    }

    public static string GetFingerprint(X509Certificate2 certificate) =>
        ToBase64Url(SHA256.HashData(certificate.RawData));

    public static bool Matches(X509Certificate certificate, string expectedFingerprint)
    {
        var actual = SHA256.HashData(certificate.GetRawCertData());
        var expected = ParseFingerprint(expectedFingerprint);
        return expected.Length == actual.Length
            && CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static byte[] ParseFingerprint(string fingerprint)
    {
        var text = fingerprint.Trim();
        if (text.Length == 64 && text.All(Uri.IsHexDigit))
        {
            return Convert.FromHexString(text);
        }

        var base64 = text.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2:
                base64 += "==";
                break;
            case 3:
                base64 += "=";
                break;
        }

        try
        {
            return Convert.FromBase64String(base64);
        }
        catch (FormatException ex)
        {
            throw new PeerSyncException(
                PeerErrorCodes.Protocol,
                "Некорректный отпечаток сертификата: " + ex.Message);
        }
    }

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
