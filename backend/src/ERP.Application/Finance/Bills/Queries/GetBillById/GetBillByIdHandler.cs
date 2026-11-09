using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class GetBillByIdHandler(IBillRepository bills) : IQueryHandler<GetBillByIdQuery, BillDto>
{
    public async Task<BillDto> Handle(GetBillByIdQuery query, CancellationToken ct)
        => BillDto.From(await bills.GetWithLinesAsync(query.Id, ct));
}
