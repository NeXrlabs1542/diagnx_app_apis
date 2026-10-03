using System.Security.Cryptography;
using System.Text;

namespace DiagnX.Api.Services.Security;

/// <summary>
/// Every server secret (JWT signing, field encryption, OTP hashing, signed file URLs) is
/// derived from ONE master key with HKDF, so deployment needs a single secret env var:
/// <c>Security__MasterKey</c>. Changing it invalidates tokens and makes previously
/// encrypted Aadhaar / bank numbers unreadable — treat it like a database password.
/// </summary>
public sealed class SecretKeys
{
    public byte[] JwtSigningKey { get; }
    public byte[] FieldEncryptionKey { get; }
    public byte[] OtpHashKey { get; }
    public byte[] FileUrlKey { get; }
    public byte[] TokenHashKey { get; }

    public SecretKeys(IConfiguration config, IHostEnvironment env)
    {
        var master = config["Security:MasterKey"];
        if (string.IsNullOrWhiteSpace(master) || master.Length < 32)
        {
            if (!env.IsDevelopment())
                throw new InvalidOperationException("Security:MasterKey must be set (min 32 chars) outside Development.");
            master = "dev-only-master-key-change-me-0123456789abcdef";
        }

        var ikm = Encoding.UTF8.GetBytes(master);
        JwtSigningKey = Derive(ikm, "diagnx-jwt");
        FieldEncryptionKey = Derive(ikm, "diagnx-field-encryption");
        OtpHashKey = Derive(ikm, "diagnx-otp");
        FileUrlKey = Derive(ikm, "diagnx-file-url");
        TokenHashKey = Derive(ikm, "diagnx-refresh-token");
    }

    private static byte[] Derive(byte[] ikm, string purpose) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 32, salt: Encoding.UTF8.GetBytes("diagnx-v1"), info: Encoding.UTF8.GetBytes(purpose));

    public static string Hmac(byte[] key, string value) =>
        Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
