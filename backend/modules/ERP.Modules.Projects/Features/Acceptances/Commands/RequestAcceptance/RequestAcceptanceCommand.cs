using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Asks the project's customer to sign off the project or one of its milestones in the portal.</summary>
public sealed record RequestAcceptanceCommand(RequestAcceptanceInput Input) : ICommand<AcceptanceDto>, IRequirePermission
{
    public string RequiredPermission => P.MilestonesManage;
}
