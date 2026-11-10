using ERP.Application.Common;
using ERP.Modules.Sales.Repositories;

namespace ERP.Modules.Sales.Features;

internal sealed class GetSalesReturnHandler(ISalesReturnRepository returns) : IQueryHandler<GetSalesReturnQuery, SalesReturnDto>
{
    public async Task<SalesReturnDto> Handle(GetSalesReturnQuery q, CancellationToken ct) => SalesReturnDto.From(await returns.GetWithLinesForUpdateAsync(q.Id, ct));
}
