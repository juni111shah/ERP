using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Workflow;

/// <summary>The document vault: tracked documents across owners, only of owner types whose records the caller may read.</summary>
public sealed record ListAttachmentVaultQuery : PagedQuery, IQuery<PagedResult<VaultRow>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AttachmentsRead;
}
