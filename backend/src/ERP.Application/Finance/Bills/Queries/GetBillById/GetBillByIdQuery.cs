using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>One vendor bill with its lines.</summary>
public sealed record GetBillByIdQuery(Guid Id) : IQuery<BillDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BillsRead;
}
