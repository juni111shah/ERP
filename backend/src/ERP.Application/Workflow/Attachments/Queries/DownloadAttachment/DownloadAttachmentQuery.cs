using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Workflow;

/// <summary>The stored file of an attachment whose record the caller may read.</summary>
public sealed record DownloadAttachmentQuery(Guid Id) : IQuery<AttachmentDownload>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AttachmentsRead;
}
