using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Approves or rejects a draft change order; an approved one changes the project's budget and fee.</summary>
public sealed record DecideChangeOrderCommand(Guid Id, bool Approve, string? Note) : ICommand<ChangeOrderDto>, IRequirePermission
{
    public string RequiredPermission => P.ChangeOrdersManage;
}
