using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Records that the customer was chased about a bounced cheque (who/what is kept in the note); stops the follow-up reminder.</summary>
public sealed record LogChequeFollowUpCommand(Guid Id, DateOnly? Date, string Note) : ICommand<ChequeDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ChequesWrite;
}
