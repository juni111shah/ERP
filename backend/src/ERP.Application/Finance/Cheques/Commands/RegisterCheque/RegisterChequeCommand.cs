using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Records a cheque received from a customer: Dr cheques in hand, Cr receivables, and settles the invoices it pays.</summary>
public sealed record RegisterChequeCommand(ChequeInput Input) : ICommand<ChequeDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ChequesWrite;
}
