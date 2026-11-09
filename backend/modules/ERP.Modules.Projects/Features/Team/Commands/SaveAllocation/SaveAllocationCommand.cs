using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Plans (no id) or changes the hours per week a person works on an open project between two dates.</summary>
public sealed record SaveAllocationCommand(Guid? Id, AllocationInput Input) : ICommand<AllocationDto>, IRequirePermission
{
    public string RequiredPermission => P.TeamManage;
}
