using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>
/// Closes a fiscal year: moves every income and expense balance to the retained earnings account in one journal dated the last day of the
/// year and locks the books to that date. Running it again for a closed year changes nothing and returns the existing close.
/// Earlier years that were never closed are swept into the same entry, so the first close of a long-running workspace needs no replay.
/// </summary>
public sealed record CloseFiscalYearCommand(Guid LegalEntityId, int FiscalYear) : ICommand<FiscalYearCloseDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.YearEndClose;
}
