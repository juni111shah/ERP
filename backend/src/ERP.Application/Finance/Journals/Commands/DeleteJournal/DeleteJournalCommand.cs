using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Deletes a manual draft journal, withdrawing its pending approval.</summary>
public sealed record DeleteJournalCommand(Guid Id) : ICommand, IRequirePermission
{
    public string RequiredPermission => CorePermissions.JournalsWrite;
}
