using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class ListCommentsHandler(IProjectCommentRepository comments, IProjectTaskRepository tasks, ProjectAccess access) : IQueryHandler<ListCommentsQuery, IReadOnlyList<CommentDto>>
{
    public async Task<IReadOnlyList<CommentDto>> Handle(ListCommentsQuery query, CancellationToken ct)
    {
        await access.EnsureProjectAsync(query.OwnerType == "task" ? (await tasks.GetAsync(query.OwnerId, ct)).ProjectId : query.OwnerId, ct);
        var rows = await comments.ListForOwnerAsync(query.OwnerType, query.OwnerId, ct);
        return rows.OrderBy(c => c.CreatedAt).Select(CommentDto.From).ToList();
    }
}
