using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class GetPaymentByIdHandler(ISettlementRepository settlements, PaymentDtoBuilder dtos) : IQueryHandler<GetPaymentByIdQuery, PaymentDto>
{
    public async Task<PaymentDto> Handle(GetPaymentByIdQuery query, CancellationToken ct)
        => await dtos.BuildAsync(await settlements.GetPaymentWithAllocationsAsync(query.Id, ct), ct);
}
