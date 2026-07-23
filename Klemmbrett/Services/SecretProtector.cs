using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using NLog;

namespace Klemmbrett.Services;

/// <summary>
/// Verschlüsselt einzelne Werte (nur als Secret erkannte Texteinträge) INLINE in der
/// history.json. Damit steht der Rest der Datei weiterhin im Klartext — AV sieht keinen
/// opaken Blob, nur einzelne Base64-Felder. Ziel: kein „Wiper/Ransomware"-Fehlalarm.
/// Format: <c>ENC1:&lt;base64&gt;</c>. Windows: DPAPI (per-User).
/// Linux/macOS: AES-256-GCM mit lokalem Master-Key (0600, im Config-Verzeichnis).
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string token);
    bool IsProtected(string? value);
}

public sealed class SecretProtector : ISecretProtector
{
    private const string Prefix = "ENC1:";
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly IPayloadCipher _cipher;

    public SecretProtector()
        : this(DefaultKeyFile()) { }

    public SecretProtector(string keyFilePath)
    {
        _cipher = OperatingSystem.IsWindows()
            ? new DpapiCipher()
            : new AesGcmCipher(keyFilePath);
    }

    public bool IsProtected(string? value) =>
        !string.IsNullOrEmpty(value) && value.StartsWith(Prefix, StringComparison.Ordinal);

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var cipher = _cipher.Encrypt(Encoding.UTF8.GetBytes(plaintext));
        return Prefix + Convert.ToBase64String(cipher);
    }

    public string Unprotect(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (!IsProtected(token))
            throw new FormatException("Token ist nicht im ENC1-Format.");
        var blob = Convert.FromBase64String(token.AsSpan(Prefix.Length).ToString());
        return Encoding.UTF8.GetString(_cipher.Decrypt(blob));
    }

    private static string DefaultKeyFile()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Klemmbrett");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "protect.key");
    }

    private interface IPayloadCipher
    {
        byte[] Encrypt(byte[] plain);
        byte[] Decrypt(byte[] cipher);
    }

    [SupportedOSPlatform("windows")]
    private sealed class DpapiCipher : IPayloadCipher
    {
        public byte[] Encrypt(byte[] plain) =>
            ProtectedData.Protect(plain, optionalEntropy: null, DataProtectionScope.CurrentUser);

        public byte[] Decrypt(byte[] cipher) =>
            ProtectedData.Unprotect(cipher, optionalEntropy: null, DataProtectionScope.CurrentUser);
    }

    /// <summary>
    /// Linux-Fallback: AES-256-GCM mit lokal generiertem Master-Key im Config-Verzeichnis.
    /// Format des Chiffrats: nonce(12) | tag(16) | cipher. Datei-Permissions 0600.
    /// libsecret wäre schöner, braucht aber P/Invoke — pragmatischer Startpunkt.
    /// </summary>
    private sealed class AesGcmCipher : IPayloadCipher
    {
        private const int NonceLen = 12, TagLen = 16, KeyLen = 32;
        private readonly byte[] _key;

        public AesGcmCipher(string keyFilePath)
        {
            _key = LoadOrCreateKey(keyFilePath);
        }

        public byte[] Encrypt(byte[] plain)
        {
            var nonce = RandomNumberGenerator.GetBytes(NonceLen);
            var cipher = new byte[plain.Length];
            var tag = new byte[TagLen];
            using (var aes = new AesGcm(_key, TagLen))
                aes.Encrypt(nonce, plain, cipher, tag);

            var outBuf = new byte[NonceLen + TagLen + cipher.Length];
            Buffer.BlockCopy(nonce, 0, outBuf, 0, NonceLen);
            Buffer.BlockCopy(tag, 0, outBuf, NonceLen, TagLen);
            Buffer.BlockCopy(cipher, 0, outBuf, NonceLen + TagLen, cipher.Length);
            return outBuf;
        }

        public byte[] Decrypt(byte[] blob)
        {
            if (blob.Length < NonceLen + TagLen)
                throw new CryptographicException("Chiffrat ist zu kurz.");
            var nonce = blob[..NonceLen];
            var tag = blob[NonceLen..(NonceLen + TagLen)];
            var cipher = blob[(NonceLen + TagLen)..];
            var plain = new byte[cipher.Length];
            using (var aes = new AesGcm(_key, TagLen))
                aes.Decrypt(nonce, cipher, tag, plain);
            return plain;
        }

        private static byte[] LoadOrCreateKey(string path)
        {
            if (File.Exists(path))
            {
                var existing = File.ReadAllBytes(path);
                if (existing.Length == KeyLen)
                    return existing;
                Log.Warn("Vorhandener Schutz-Schlüssel hat falsche Länge — erzeuge neuen (alte Secrets werden unlesbar)");
            }

            var key = RandomNumberGenerator.GetBytes(KeyLen);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, key);
            TrySetOwnerReadWriteOnly(path);
            Log.Info("Neuen Schutz-Schlüssel angelegt: {Path}", path);
            return key;
        }

        private static void TrySetOwnerReadWriteOnly(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;
            try
            {
                File.SetUnixFileMode(path,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch (Exception ex)
            {
                Log.Warn(ex, "Konnte Dateirechte des Schutz-Schlüssels nicht auf 0600 setzen");
            }
        }
    }
}
