using System.Security.Cryptography;
using System.Text;

namespace OpenPlane.Storage.Services;

public sealed class EncryptionService
{
    private readonly string keyPath;

    public EncryptionService(string appName)
    {
        var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName);
        Directory.CreateDirectory(basePath);
        keyPath = Path.Combine(basePath, "history.key");
    }

    public async Task<string> EncryptAsync(string content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = await GetOrCreateKeyAsync(cancellationToken);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        var plainBytes = Encoding.UTF8.GetBytes(content);
        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        return $"{Convert.ToBase64String(aes.IV)}.{Convert.ToBase64String(cipherBytes)}";
    }

    public async Task<string> DecryptAsync(string payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var segments = payload.Split('.', 2);
        if (segments.Length != 2)
        {
            throw new InvalidOperationException("Encrypted payload is invalid.");
        }

        var key = await GetOrCreateKeyAsync(cancellationToken);
        var iv = Convert.FromBase64String(segments[0]);
        var cipherBytes = Convert.FromBase64String(segments[1]);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    private async Task<byte[]> GetOrCreateKeyAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(keyPath))
        {
            var existing = await File.ReadAllTextAsync(keyPath, cancellationToken);
            return Convert.FromBase64String(existing);
        }

        var key = RandomNumberGenerator.GetBytes(32);
        await File.WriteAllTextAsync(keyPath, Convert.ToBase64String(key), cancellationToken);
        return key;
    }

    public async Task RotateKeyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = RandomNumberGenerator.GetBytes(32);
        await File.WriteAllTextAsync(keyPath, Convert.ToBase64String(key), cancellationToken);
    }
}
