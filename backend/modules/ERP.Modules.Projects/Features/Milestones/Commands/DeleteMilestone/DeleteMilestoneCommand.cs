using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Deletes a milestone that is not invoiced.</summary>
public sealed record DeleteMilestoneCommand(Guid Id) : ICommand, IRequirePermission
{
    public string RequiredPermission => P.MilestonesManage;
}
