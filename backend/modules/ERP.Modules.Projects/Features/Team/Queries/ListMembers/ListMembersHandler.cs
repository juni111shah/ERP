using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class ListMembersHandler(IProjectMemberRepository members, ProjectAccess access) : IQueryHandler<ListMembersQuery, IReadOnlyList<MemberDto>>
{
    public async Task<IReadOnlyList<MemberDto>> Handle(ListMembersQuery query, CancellationToken ct)
    {
        await access.EnsureProjectAsync(query.ProjectId, ct);
        return (await members.ListForProjectAsync(query.ProjectId, ct)).OrderBy(m => m.Name).Select(m => new MemberDto(m.Id, m.ProjectId, m.UserId, m.Name, m.Role)).ToList();
    }
}
