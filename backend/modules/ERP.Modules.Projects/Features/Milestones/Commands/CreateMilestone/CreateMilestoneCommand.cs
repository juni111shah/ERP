using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Adds a milestone to an open project; on a fixed-fee project the milestones cannot add up to more than the fee.</summary>
public sealed record CreateMilestoneCommand(MilestoneInput Input) : ICommand<MilestoneDto>, IRequirePermission
{
    public string RequiredPermission => P.MilestonesManage;
}
