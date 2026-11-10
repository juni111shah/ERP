using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Posts a manual draft journal with its real number, or sends it for approval when a policy or the workspace setting asks for one.</summary>
public sealed record PostJournalCommand(Guid Id) : ICommand<JournalDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.JournalsPost;
}
