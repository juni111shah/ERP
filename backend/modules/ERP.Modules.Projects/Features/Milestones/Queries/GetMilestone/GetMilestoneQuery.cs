using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>One milestone with its project name.</summary>
public sealed record GetMilestoneQuery(Guid Id) : IQuery<MilestoneDto>, IRequirePermission
{
    public string RequiredPermission => P.MilestonesRead;
}
