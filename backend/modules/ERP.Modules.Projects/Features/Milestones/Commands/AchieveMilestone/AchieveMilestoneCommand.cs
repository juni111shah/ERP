using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Marks a milestone as achieved (today unless a past date is given), so it can be billed.</summary>
public sealed record AchieveMilestoneCommand(Guid Id, DateOnly? AchievedDate = null) : ICommand<MilestoneDto>, IRequirePermission
{
    public string RequiredPermission => P.MilestonesManage;
}
