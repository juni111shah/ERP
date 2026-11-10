using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Reopens the latest closed year: reverses the closing entry on the same date and moves the books lock back. The reason is required.</summary>
public sealed record ReopenFiscalYearCommand(Guid LegalEntityId, int FiscalYear, string? Reason) : ICommand<FiscalYearCloseDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.YearEndReopen;
}
