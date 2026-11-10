using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Sends draft or rejected time for approval; answers how many entries were submitted.</summary>
public sealed record SubmitTimesheetsCommand(IReadOnlyList<Guid> Ids) : ICommand<int>, IRequirePermission
{
    public string RequiredPermission => P.TimesheetsManage;
}
