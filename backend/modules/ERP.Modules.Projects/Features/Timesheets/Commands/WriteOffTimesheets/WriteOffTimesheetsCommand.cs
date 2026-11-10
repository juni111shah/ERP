using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Marks unbilled time as not to be charged; it leaves the unbilled figures and cannot be billed.</summary>
public sealed record WriteOffTimesheetsCommand(IReadOnlyList<Guid> Ids) : ICommand<int>, IRequirePermission
{
    public string RequiredPermission => P.BillingManage;
}
