using ERP.Application.Common;
using ERP.Modules.Purchasing.Repositories;

namespace ERP.Modules.Purchasing.Features;

internal sealed class GetPurchaseReturnHandler(IPurchaseReturnRepository returns) : IQueryHandler<GetPurchaseReturnQuery, PurchaseReturnDto>
{
    public async Task<PurchaseReturnDto> Handle(GetPurchaseReturnQuery q, CancellationToken ct) => PurchaseReturnDto.From(await returns.GetWithLinesAndSharesForUpdateAsync(q.Id, ct));
}
