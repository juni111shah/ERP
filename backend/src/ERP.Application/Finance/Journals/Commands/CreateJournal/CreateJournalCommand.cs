using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Saves a manual journal as a draft, in the legal entity's base currency. It gets its real number when posted.</summary>
public sealed record CreateJournalCommand(JournalInput Input) : ICommand<JournalDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.JournalsWrite;
}
