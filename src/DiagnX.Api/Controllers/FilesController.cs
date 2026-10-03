using DiagnX.Api.Common;
using DiagnX.Api.Services.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiagnX.Api.Controllers;

/// <summary>Serves a private file through a short-lived signed URL (thumbnails, report PDFs).</summary>
[Route("api/v1/files")]
[ApiExplorerSettings(GroupName = "master")]
public sealed class FilesController(IFileStorage storage, SignedUrl signer) : ApiControllerBase
{
    [HttpGet("{fileId:guid}")]
    [AllowAnonymous]
    [Produces("application/octet-stream")]
    public async Task<IActionResult> Get(Guid fileId, [FromQuery] long exp, [FromQuery] string? sig, CancellationToken ct)
    {
        if (!signer.IsValid(fileId, exp, sig)) throw ApiException.Forbidden("This link has expired. Please refresh and try again.");
        var file = await storage.ReadAsync(fileId, ct) ?? throw ApiException.NotFound();
        Response.Headers.CacheControl = "private, max-age=300";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Bytes, file.ContentType, file.FileName);
    }
}
