using System.Security.Cryptography;
using System.Text;

namespace DiagnX.Api.Services.Security;

/// <summary>
/// AES-256-GCM encryption for write-only sensitive values (Aadhaar, bank account, PAN).
/// Output: "v1:" + base64(nonce | tag | ciphertext).
/// </summary>
public sealed class FieldEncryptor(SecretKeys keys)
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public string Encrypt(string plain)
    {
        var data = Encoding.UTF8.GetBytes(plain);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[data.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(keys.FieldEncryptionKey, TagSize);
        aes.Encrypt(nonce, data, cipher, tag);

        var payload = new byte[NonceSize + TagSize + cipher.Length];
        nonce.CopyTo(payload, 0);
        tag.CopyTo(payload, NonceSize);
        cipher.CopyTo(payload, NonceSize + TagSize);
        return "v1:" + Convert.ToBase64String(payload);
    }

    public string Decrypt(string encrypted)
    {
        if (!encrypted.StartsWith("v1:")) throw new CryptographicException("Unknown ciphertext version");
        var payload = Convert.FromBase64String(encrypted[3..]);
        var nonce = payload.AsSpan(0, NonceSize);
        var tag = payload.AsSpan(NonceSize, TagSize);
        var cipher = payload.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(keys.FieldEncryptionKey, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
