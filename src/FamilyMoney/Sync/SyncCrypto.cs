using FamilyMoney.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace FamilyMoney.Sync;

/// <summary>
/// AES-GCM для объектов синхронизации. Ключ выводится из парольной фразы
/// через PBKDF2-SHA256 и хранится на устройстве в base64.
/// </summary>
public sealed class SyncCrypto
{
    public const string TextPrefix = "FMENC1:";

    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int Iterations = 210_000;
    private const string KeyCheckMarker = "familymoney-key-check";
    private const string KeyCheckAad = "key-check";

    private static readonly byte[] Magic = "FMENC1"u8.ToArray();

    private readonly byte[] _key;

    private SyncCrypto(byte[] key)
    {
        _key = key;
    }

    public static SyncCrypto? Create(SyncEncryptionConfiguration configuration)
    {
        if (!configuration.Enabled)
        {
            return null;
        }

        if (!TryDecodeKey(configuration.Key, out var key))
        {
            throw new SyncEncryptionException(
                "Включено шифрование, но на устройстве нет ключа. Введите парольную фразу.");
        }

        if (!string.IsNullOrWhiteSpace(configuration.KeyCheck)
            && !VerifyKeyCheck(key, configuration.KeyCheck))
        {
            throw new SyncEncryptionException(
                "Ключ шифрования не подходит к этой базе. Проверьте парольную фразу.");
        }

        return new SyncCrypto(key);
    }

    public static (string Salt, string Key, string KeyCheck) CreateKey(string passphrase)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = DeriveKey(passphrase, salt);
        return (
            Convert.ToBase64String(salt),
            Convert.ToBase64String(key),
            CreateKeyCheck(key));
    }

    public static byte[] DeriveKey(string passphrase, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            passphrase,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

    public static string CreateKeyCheck(byte[] key)
    {
        using var aes = new AesGcm(key, TagSize);
        var plain = Encoding.UTF8.GetBytes(KeyCheckMarker);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        aes.Encrypt(
            nonce,
            plain,
            cipher,
            tag,
            Encoding.UTF8.GetBytes(KeyCheckAad));

        var payload = new byte[NonceSize + cipher.Length + TagSize];
        nonce.CopyTo(payload, 0);
        cipher.CopyTo(payload, NonceSize);
        tag.CopyTo(payload, NonceSize + cipher.Length);
        return Convert.ToBase64String(payload);
    }

    public static bool VerifyKeyCheck(byte[] key, string keyCheck)
    {
        try
        {
            var payload = Convert.FromBase64String(keyCheck);
            if (payload.Length < NonceSize + TagSize)
            {
                return false;
            }

            using var aes = new AesGcm(key, TagSize);
            var nonce = payload.AsSpan(0, NonceSize);
            var cipher = payload.AsSpan(NonceSize, payload.Length - NonceSize - TagSize);
            var tag = payload.AsSpan(payload.Length - TagSize, TagSize);
            var plain = new byte[cipher.Length];
            aes.Decrypt(
                nonce,
                cipher,
                tag,
                plain,
                Encoding.UTF8.GetBytes(KeyCheckAad));

            return string.Equals(
                Encoding.UTF8.GetString(plain),
                KeyCheckMarker,
                StringComparison.Ordinal);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public string EncryptText(string plaintext, string aad) =>
        TextPrefix + Convert.ToBase64String(Encrypt(Encoding.UTF8.GetBytes(plaintext), aad));

    public string DecryptText(string content, string aad)
    {
        if (!content.StartsWith(TextPrefix, StringComparison.Ordinal))
        {
            // Совместимость с объектами, записанными до включения шифрования.
            return content;
        }

        try
        {
            var payload = Convert.FromBase64String(content[TextPrefix.Length..]);
            return Encoding.UTF8.GetString(Decrypt(payload, aad));
        }
        catch (FormatException)
        {
            throw new SyncEncryptionException("Повреждённый зашифрованный объект синхронизации.");
        }
        catch (CryptographicException)
        {
            throw new SyncEncryptionException(
                "Не удалось расшифровать объект синхронизации: неверный ключ или повреждённые данные.");
        }
    }

    public byte[] Encrypt(byte[] plaintext, string aad)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintext, cipher, tag, Encoding.UTF8.GetBytes(aad));

        var result = new byte[Magic.Length + NonceSize + cipher.Length + TagSize];
        Magic.CopyTo(result, 0);
        nonce.CopyTo(result, Magic.Length);
        cipher.CopyTo(result, Magic.Length + NonceSize);
        tag.CopyTo(result, Magic.Length + NonceSize + cipher.Length);
        return result;
    }

    public byte[] Decrypt(byte[] payload, string aad)
    {
        if (!payload.AsSpan().StartsWith(Magic))
        {
            // Старый незашифрованный объект (снапшот или картинка).
            return payload;
        }

        if (payload.Length < Magic.Length + NonceSize + TagSize)
        {
            throw new SyncEncryptionException("Повреждённый зашифрованный объект синхронизации.");
        }

        var nonce = payload.AsSpan(Magic.Length, NonceSize);
        var cipherLength = payload.Length - Magic.Length - NonceSize - TagSize;
        var cipher = payload.AsSpan(Magic.Length + NonceSize, cipherLength);
        var tag = payload.AsSpan(payload.Length - TagSize, TagSize);
        var plain = new byte[cipherLength];

        try
        {
            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain, Encoding.UTF8.GetBytes(aad));
            return plain;
        }
        catch (CryptographicException)
        {
            throw new SyncEncryptionException(
                "Не удалось расшифровать объект синхронизации: неверный ключ или повреждённые данные.");
        }
    }

    private static bool TryDecodeKey(string encoded, out byte[] key)
    {
        key = [];
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return false;
        }

        try
        {
            key = Convert.FromBase64String(encoded);
            return key.Length == KeySize;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
