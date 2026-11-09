using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Sends draft or rejected expenses for approval; answers how many were submitted.</summary>
public sealed record SubmitExpensesCommand(IReadOnlyList<Guid> Ids) : ICommand<int>, IRequirePermission
{
    public string RequiredPermission => P.ExpensesManage;
}
