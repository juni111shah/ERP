using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Printing;

/// <summary>Get print data for a journal entry.</summary>
public sealed record GetPrintJournalQuery(Guid Id) : IQuery<PrintJournalDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.JournalsRead;
}
