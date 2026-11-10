using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>The change orders of a project, or of every project the caller may reach, newest first.</summary>
public sealed record ListChangeOrdersQuery(Guid? ProjectId) : IQuery<IReadOnlyList<ChangeOrderDto>>, IRequirePermission
{
    public string RequiredPermission => P.ChangeOrdersRead;
}
