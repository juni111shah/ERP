using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class ListPlatformTeamHandler(IPlatformDirectory directory) : IQueryHandler<ListPlatformTeamQuery, IReadOnlyList<PlatformTeamMember>>
{
    public async Task<IReadOnlyList<PlatformTeamMember>> Handle(ListPlatformTeamQuery query, CancellationToken ct) => await directory.TeamAsync(ct);
}
