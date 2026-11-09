using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Edits a milestone that is not invoiced.</summary>
public sealed record UpdateMilestoneCommand(Guid Id, MilestoneInput Input) : ICommand<MilestoneDto>, IRequirePermission
{
    public string RequiredPermission => P.MilestonesManage;
}
