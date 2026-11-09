using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>The rate cards that apply to a project (its own and the workspace-wide ones), or all of them.</summary>
public sealed record ListRatesQuery(Guid? ProjectId) : IQuery<IReadOnlyList<RateDto>>, IRequirePermission
{
    public string RequiredPermission => P.RatesRead;
}
