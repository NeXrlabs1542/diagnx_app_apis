using System.Security.Cryptography;
using DiagnX.Api.Data;
using DiagnX.Api.Data.Entities;
using DiagnX.Api.Services.Security;
using Microsoft.EntityFrameworkCore;

namespace DiagnX.Api.Services.Storage;

public sealed record StoredFileInfo(Guid Id, string FileName, string ContentType, int SizeBytes, string Sha256);

public sealed record FileContent(string FileName, string ContentType, byte[] Bytes);

/// <summary>
/// Private file storage. Files are never public: clients get short-lived signed URLs
/// (<see cref="SignedUrl"/>) served by FilesController. Swap the implementation for S3 / R2
/// later — callers only use this interface.
/// </summary>
public interface IFileStorage
{
    Task<StoredFileInfo> SaveAsync(Stream content, string fileName, string contentType, string ownerType, Guid ownerId, CancellationToken ct = default);
    Task<FileContent?> ReadAsync(Guid fileId, CancellationToken ct = default);
    Task DeleteAsync(Guid fileId, CancellationToken ct = default);
}

public sealed class DbFileStorage(AppDbContext db) : IFileStorage
{
    public async Task<StoredFileInfo> SaveAsync(Stream content, string fileName, string contentType, string ownerType, Guid ownerId, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var file = new StoredFile
        {
            Id = Guid.NewGuid(),
            FileName = fileName.Length > 200 ? fileName[^200..] : fileName,
            ContentType = contentType,
            SizeBytes = bytes.Length,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            Content = bytes,
            OwnerType = ownerType,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow,
        };
        db.StoredFiles.Add(file);
        await db.SaveChangesAsync(ct);
        return new StoredFileInfo(file.Id, file.FileName, file.ContentType, file.SizeBytes, file.Sha256);
    }

    public async Task<FileContent?> ReadAsync(Guid fileId, CancellationToken ct = default) =>
        await db.StoredFiles.AsNoTracking().Where(f => f.Id == fileId)
            .Select(f => new FileContent(f.FileName, f.ContentType, f.Content)).FirstOrDefaultAsync(ct);

    public async Task DeleteAsync(Guid fileId, CancellationToken ct = default) =>
        await db.StoredFiles.Where(f => f.Id == fileId).ExecuteDeleteAsync(ct);
}

/// <summary>Creates and checks HMAC-signed, expiring download URLs for stored files.</summary>
public sealed class SignedUrl(SecretKeys keys, IHttpContextAccessor http)
{
    public const int DefaultTtlSeconds = 600;

    public string Create(Guid fileId, int ttlSeconds = DefaultTtlSeconds)
    {
        var exp = DateTimeOffset.UtcNow.AddSeconds(ttlSeconds).ToUnixTimeSeconds();
        var sig = Sign(fileId, exp);
        var req = http.HttpContext?.Request;
        var origin = req == null ? "" : $"{req.Scheme}://{req.Host}";
        return $"{origin}/api/v1/files/{fileId:N}?exp={exp}&sig={sig}";
    }

    public bool IsValid(Guid fileId, long exp, string? sig) =>
        sig != null && exp >= DateTimeOffset.UtcNow.ToUnixTimeSeconds() &&
        CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(Sign(fileId, exp)), System.Text.Encoding.ASCII.GetBytes(sig));

    private string Sign(Guid fileId, long exp) => SecretKeys.Hmac(keys.FileUrlKey, $"{fileId:N}.{exp}")[..32];
}
