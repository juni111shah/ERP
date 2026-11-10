using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Closes the books of a legal entity up to (and including) a date. Nothing can be posted on or before it.</summary>
public sealed record LockBooksCommand(Guid LegalEntityId, DateOnly? Through) : ICommand, IRequirePermission
{
    public string RequiredPermission => CorePermissions.EntitiesWrite;
}
