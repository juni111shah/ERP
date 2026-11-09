using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>One customer receipt with its allocations.</summary>
public sealed record GetReceiptByIdQuery(Guid Id) : IQuery<ReceiptDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReceiptsRead;
}
