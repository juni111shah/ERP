using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Application.Workflow;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers.Workflow;

/// <summary>Files attached to records, and the document vault across them.</summary>
[Route("api/v1/attachments")]
public sealed class AttachmentsController(ISender mediator) : ApiControllerBase(mediator)
{
    [HttpGet("vault")]
    [RequirePermission(CorePermissions.AttachmentsRead)]
    public Task<PagedResult<VaultRow>> Vault([FromQuery] ListAttachmentVaultQuery query, CancellationToken ct)
        => Mediator.Send(query, ct);

    [HttpGet]
    [RequirePermission(CorePermissions.AttachmentsRead)]
    public Task<IReadOnlyList<AttachmentDto>> List(string ownerType, Guid ownerId, CancellationToken ct)
        => Mediator.Send(new ListAttachmentsQuery(ownerType, ownerId), ct);

    /// <summary>Multipart form: <c>file</c>, <c>ownerType</c>, <c>ownerId</c> and optional <c>category</c>, <c>description</c>, <c>expiresOn</c>.</summary>
    [HttpPost]
    [RequirePermission(CorePermissions.AttachmentsWrite)]
    public async Task<IActionResult> Upload(CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);
        var file = form.Files.FirstOrDefault() ?? throw new FieldValidationException("file", "A file is required.");
        if (!Guid.TryParse(form["ownerId"], out var ownerId)) throw new FieldValidationException("ownerId", "Owner type and id are required.");
        await using var content = file.OpenReadStream();
        var created = await Mediator.Send(new UploadAttachmentCommand(form["ownerType"].ToString(), ownerId, file.FileName, file.ContentType, file.Length, content,
            form["category"].ToString() is { Length: > 0 } cat ? cat : null, form["description"].ToString() is { Length: > 0 } desc ? desc : null,
            DateOnly.TryParse(form["expiresOn"], out var expires) ? expires : null), ct);
        return Created($"/api/v1/attachments/{created.Id}", created);
    }

    [HttpGet("{id:guid}/download")]
    [RequirePermission(CorePermissions.AttachmentsRead)]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var file = await Mediator.Send(new DownloadAttachmentQuery(id), ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(CorePermissions.AttachmentsWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteAttachmentCommand(id), ct);
        return NoContent();
    }
}
