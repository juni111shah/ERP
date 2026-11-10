using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Workflow;

/// <summary>The HTTP layer hands over the file as a plain stream; size and type rules are enforced here.</summary>
public sealed record UploadAttachmentCommand(string OwnerType, Guid OwnerId, string FileName, string ContentType, long Length, Stream Content, string? Category = null, string? Description = null, DateOnly? ExpiresOn = null)
    : ICommand<AttachmentDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AttachmentsWrite;
}
