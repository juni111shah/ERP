using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Drafts a change order on an open project, numbered CO-{project}-{nn}.</summary>
public sealed record CreateChangeOrderCommand(ChangeOrderInput Input) : ICommand<ChangeOrderDto>, IRequirePermission
{
    public string RequiredPermission => P.ChangeOrdersManage;
}
