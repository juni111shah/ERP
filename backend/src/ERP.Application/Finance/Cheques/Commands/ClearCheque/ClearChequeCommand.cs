using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>The bank paid: Dr bank, Cr cheques in hand.</summary>
public sealed record ClearChequeCommand(Guid Id, DateOnly Date) : ICommand<ChequeDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ChequesApprove;
}
