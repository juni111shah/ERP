using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Reverses an opening entry (dated like the entry unless a date is given) and voids the documents it raised. Refused once any of them has been settled.</summary>
public sealed record ReverseOpeningBalanceCommand(Guid JournalId, string? Reason, DateOnly? Date = null) : ICommand<OpeningBalanceDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.JournalsReverse;
}
