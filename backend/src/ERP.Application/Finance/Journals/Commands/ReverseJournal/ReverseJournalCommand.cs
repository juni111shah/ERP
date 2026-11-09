using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Reverses a posted manual journal (today, or on the given date). Journals posted by documents or the system are undone where they came from.</summary>
public sealed record ReverseJournalCommand(Guid Id, DateOnly? Date, string? Memo) : ICommand<JournalDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.JournalsReverse;
}
