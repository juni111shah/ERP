using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Workflow;

/// <summary>Soft delete: the stored file is kept for audit.</summary>
public sealed record DeleteAttachmentCommand(Guid Id) : ICommand, IRequirePermission
{
    public string RequiredPermission => CorePermissions.AttachmentsWrite;
}
