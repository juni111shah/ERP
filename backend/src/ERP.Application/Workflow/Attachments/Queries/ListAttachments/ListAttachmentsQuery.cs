using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Workflow;

/// <summary>The attachments of one record the caller may read.</summary>
public sealed record ListAttachmentsQuery(string OwnerType, Guid OwnerId) : IQuery<IReadOnlyList<AttachmentDto>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AttachmentsRead;
}
