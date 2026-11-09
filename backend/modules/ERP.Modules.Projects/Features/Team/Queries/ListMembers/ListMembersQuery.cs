using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>The team of a project, by name.</summary>
public sealed record ListMembersQuery(Guid ProjectId) : IQuery<IReadOnlyList<MemberDto>>, IRequirePermission
{
    public string RequiredPermission => P.ProjectsRead;
}
