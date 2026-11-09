using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>The risks and issues of a project, or of every project the caller may reach: open first, most severe first.</summary>
public sealed record ListRisksQuery(Guid? ProjectId) : IQuery<IReadOnlyList<RiskDto>>, IRequirePermission
{
    public string RequiredPermission => P.RisksRead;
}
