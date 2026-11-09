using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Security;

internal sealed class ListUsersHandler(IUserDirectory directory) : IQueryHandler<ListUsersQuery, IReadOnlyList<UserSummary>>
{
    public Task<IReadOnlyList<UserSummary>> Handle(ListUsersQuery query, CancellationToken ct) => directory.ListMembersAsync(ct);
}
