using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>The customer sign-offs of a project, or of every project the caller may reach, newest first.</summary>
public sealed record ListAcceptancesQuery(Guid? ProjectId) : IQuery<IReadOnlyList<AcceptanceDto>>, IRequirePermission
{
    public string RequiredPermission => P.MilestonesRead;
}
