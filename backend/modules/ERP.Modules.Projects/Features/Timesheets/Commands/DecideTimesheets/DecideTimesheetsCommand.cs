using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Approves or rejects submitted time; answers how many entries were decided.</summary>
public sealed record DecideTimesheetsCommand(IReadOnlyList<Guid> Ids, bool Approve, string? Reason) : ICommand<int>, IRequirePermission
{
    public string RequiredPermission => P.TimesheetsApprove;
}
