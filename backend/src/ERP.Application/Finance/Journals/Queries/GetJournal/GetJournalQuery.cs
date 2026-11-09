using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>One journal with its lines.</summary>
public sealed record GetJournalQuery(Guid Id) : IQuery<JournalDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.JournalsRead;
}
