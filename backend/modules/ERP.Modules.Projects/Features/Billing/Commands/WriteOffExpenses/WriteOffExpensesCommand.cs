using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Marks unbilled expenses as not to be charged.</summary>
public sealed record WriteOffExpensesCommand(IReadOnlyList<Guid> Ids) : ICommand<int>, IRequirePermission
{
    public string RequiredPermission => P.BillingManage;
}
