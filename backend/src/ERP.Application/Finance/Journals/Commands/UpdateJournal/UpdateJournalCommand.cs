using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Replaces the header and lines of a manual draft journal. Its legal entity cannot change.</summary>
public sealed record UpdateJournalCommand(Guid Id, JournalInput Input) : ICommand<JournalDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.JournalsWrite;
}
