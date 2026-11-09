using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Services.Features;

/// <summary>One milestone; an invoiced milestone whose invoice has been paid is marked paid on the way.</summary>
public record GetMilestoneQuery(Guid Id) : IQuery<MilestoneDto>, IRequirePermission
{
    public string RequiredPermission => Permissions.MilestonesRead;
}
