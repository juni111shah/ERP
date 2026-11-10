using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>One cheque of the register with the allocations of its receipt.</summary>
public sealed record GetChequeByIdQuery(Guid Id) : IQuery<ChequeDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ChequesRead;
}
