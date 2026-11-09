using ERP.Application.Common;

namespace ERP.Application.Platform;

internal sealed class GetBrandConversationHandler(IBrandMessageRepository messages) : IQueryHandler<GetBrandConversationQuery, IReadOnlyList<BrandMessageRow>>
{
    public async Task<IReadOnlyList<BrandMessageRow>> Handle(GetBrandConversationQuery query, CancellationToken ct) => await messages.ListRowsAsync(query.Id, ct);
}
